using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Relay.Domain;

namespace Relay.Channels;

public sealed class NoOpCaseNotifier : ICaseNotifier
{
    public ValueTask NotifyAsync(CaseNotification notification, CancellationToken ct = default) => ValueTask.CompletedTask;
}

/// <summary>Queues notifications so sending email never slows down or fails a customer's request.</summary>
public sealed class QueuedCaseNotifier : ICaseNotifier
{
    internal System.Threading.Channels.Channel<CaseNotification> Queue { get; } = System.Threading.Channels.Channel.CreateUnbounded<CaseNotification>();

    public ValueTask NotifyAsync(CaseNotification notification, CancellationToken ct = default) =>
        Queue.Writer.WriteAsync(notification, ct);
}

public sealed class EmailNotificationWorker(
    QueuedCaseNotifier notifier, IServiceScopeFactory scopes, ILogger<EmailNotificationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in notifier.Queue.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<EmailSender>().SendAsync(notification, stoppingToken);
                    break;
                }
                catch (ConcurrencyConflictException) when (attempt == 1)
                {
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Email notification failed for case {CaseId}", notification.CaseId);
                    break;
                }
            }
        }
    }
}

public sealed class EmailSender(
    EmailClient email, ICrmStore crm, IOptions<CommunicationOptions> options, TimeProvider clock, ILogger<EmailSender> log)
{
    public async Task SendAsync(CaseNotification notification, CancellationToken ct)
    {
        if (await crm.GetCaseAsync(notification.CaseId, ct) is not { } c) return;
        var to = c.Customer.Email;
        if (!options.Value.CanEmail(to))
        {
            log.LogInformation("Skipped {Kind} email for {Case}: {Address} is not on the allowlist", notification.Kind, c.Reference, to);
            return;
        }

        var link = $"{options.Value.PortalBaseUrl?.TrimEnd('/')}/requests/{c.Id}";
        var latest = c.Messages.Where(m => !m.IsInternal && m.AuthorType != AuthorType.Customer).MaxBy(m => m.CreatedAt);
        var (subject, intro, body) = notification.Kind switch
        {
            NotificationKind.CaseOpened => ($"[{c.Reference}] We've received your request",
                $"Thanks for contacting Highland Energy, {c.Customer.FirstName}. We've received your request \"{c.Title}\" and our team will be in touch.",
                (string?)null),
            _ => ($"[{c.Reference}] New reply from Highland Energy",
                $"Hi {c.Customer.FirstName}, there's a new reply on your request \"{c.Title}\".",
                latest?.Body),
        };

        var text = $"{intro}\n\n{(body is null ? "" : body + "\n\n")}View your request: {link}\n\nReference: {c.Reference}";
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;color:#0f172a">
              <p style="font-weight:600;color:#047857">Highland Energy</p>
              <p>{WebUtility.HtmlEncode(intro)}</p>
              {(body is null ? "" : $"<blockquote style=\"border-left:3px solid #a7f3d0;margin:0;padding:8px 12px;white-space:pre-wrap\">{WebUtility.HtmlEncode(body)}</blockquote>")}
              <p><a href="{WebUtility.HtmlEncode(link)}" style="background:#059669;color:#fff;padding:10px 16px;border-radius:8px;text-decoration:none;display:inline-block">View your request</a></p>
              <p style="color:#64748b;font-size:12px">Reference {c.Reference}. Highland Energy is a fictional utility used to demo Relay.</p>
            </div>
            """;

        var message = new EmailMessage(options.Value.SenderAddress!, to, new EmailContent(subject) { PlainText = text, Html = html });
        await email.SendAsync(WaitUntil.Started, message, ct);

        c.AddMessage(AuthorType.System, "Relay Notifications", $"Emailed the customer: {subject}", isInternal: true, clock.GetUtcNow());
        await crm.SaveCaseAsync(c, ct);
        log.LogInformation("Sent {Kind} email for {Case}", notification.Kind, c.Reference);
    }
}
