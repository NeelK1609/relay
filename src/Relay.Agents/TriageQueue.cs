using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Relay.Domain;

namespace Relay.Agents;

/// <summary>Cases waiting for AI triage. In-process for now; Azure Service Bus replaces it when the API scales out.</summary>
public interface ITriageQueue
{
    ValueTask EnqueueAsync(Guid caseId, CancellationToken ct = default);
}

public sealed class InProcessTriageQueue : ITriageQueue
{
    internal Channel<Guid> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid caseId, CancellationToken ct = default) => Channel.Writer.WriteAsync(caseId, ct);
}

public sealed class NoOpTriageQueue : ITriageQueue
{
    public ValueTask EnqueueAsync(Guid caseId, CancellationToken ct = default) => ValueTask.CompletedTask;
}

public sealed class TriageWorker(InProcessTriageQueue queue, IServiceScopeFactory scopes, ILogger<TriageWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var caseId in queue.Channel.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    // Fresh scope per attempt so a concurrency retry reloads the case.
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<CaseAssistant>().TriageAsync(caseId, stoppingToken);
                    break;
                }
                catch (ConcurrencyConflictException) when (attempt == 1)
                {
                    log.LogInformation("Case {CaseId} changed during triage; retrying", caseId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Triage failed for case {CaseId}", caseId);
                    break;
                }
            }
        }
    }
}
