using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relay.Agents;
using Relay.Crm.Sql;
using Relay.Domain;

namespace Relay.Api;

/// <summary>Keeps the public demo cheap and tidy: caps AI usage and reloads demo data nightly.</summary>
public static class DemoSafeguards
{
    public static IServiceCollection AddDemoSafeguards(this IServiceCollection services, IConfiguration configuration)
    {
        var perMinute = configuration.GetValue("Demo:AiRequestsPerMinute", 20);
        var perDay = configuration.GetValue("Demo:AiRequestsPerDay", 300);

        services.AddRateLimiter(o =>
        {
            o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                AiLimiter("ai-minute", perMinute, TimeSpan.FromMinutes(1)),
                AiLimiter("ai-day", perDay, TimeSpan.FromDays(1)));
            o.OnRejected = (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "The demo's AI limit has been reached.",
                    Detail = "This demo caps AI usage to keep costs down. Please try again in a minute.",
                }, ct));
            };
        });

        if (configuration.GetValue("Demo:DailyReset", false))
        {
            services.AddHostedService<DemoResetService>();
        }
        return services;
    }

    private static PartitionedRateLimiter<HttpContext> AiLimiter(string name, int permits, TimeSpan window) =>
        PartitionedRateLimiter.Create<HttpContext, string>(ctx => CostsAi(ctx.Request)
            ? RateLimitPartition.GetFixedWindowLimiter(name, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = window,
                QueueLimit = 0,
            })
            : RateLimitPartition.GetNoLimiter("free"));

    /// <summary>Requests that call a model: Ask Relay, AI actions, new portal cases (triage), and customer chat turns.</summary>
    private static bool CostsAi(HttpRequest r) =>
        HttpMethods.IsPost(r.Method) && r.Path.Value is { } path &&
        (path.StartsWith("/api/workspace/ask", StringComparison.OrdinalIgnoreCase) ||
         (path.StartsWith("/api/workspace/cases/", StringComparison.OrdinalIgnoreCase) && path.Contains("/ai/", StringComparison.OrdinalIgnoreCase)) ||
         (path.StartsWith("/api/portal/", StringComparison.OrdinalIgnoreCase) &&
          (path.EndsWith("/cases", StringComparison.OrdinalIgnoreCase) || path.Contains("/chats", StringComparison.OrdinalIgnoreCase))));
}

/// <summary>
/// Reloads demo data once per night (08:00 UTC). Checks on a timer and at startup, so it still
/// runs on the first request of the day when the app has scaled to zero overnight.
/// </summary>
public sealed class DemoResetService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<DemoResetService> log) : BackgroundService
{
    private static readonly TimeSpan ResetAtUtc = TimeSpan.FromHours(8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), clock);
        do
        {
            try
            {
                await ResetIfDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Demo reset check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ResetIfDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var todayBoundary = new DateTimeOffset(now.UtcDateTime.Date + ResetAtUtc, TimeSpan.Zero);
        var lastBoundary = now >= todayBoundary ? todayBoundary : todayBoundary.AddDays(-1);

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayDbContext>();
        var seededAt = await db.DemoState.Select(s => (DateTimeOffset?)s.SeededAt).MaxAsync(ct);
        if (seededAt >= lastBoundary) return;

        log.LogInformation("Resetting demo data (last seeded {SeededAt})", seededAt);
        await DemoData.ResetAsync(db, now, ct);

        var crm = scope.ServiceProvider.GetRequiredService<ICrmStore>();
        var triage = scope.ServiceProvider.GetRequiredService<ITriageQueue>();
        foreach (var c in await crm.ListCasesAsync(new CaseQuery(Status: CaseStatus.New), ct))
        {
            await triage.EnqueueAsync(c.Id, ct);
        }
    }
}
