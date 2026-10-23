using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Relay.Domain;

namespace Relay.Agents;

/// <summary>Runs the Relay agents against cases and records the results in the CRM.</summary>
public sealed class CaseAssistant(
    RelayAgents agents, ICrmStore crm, TimeProvider clock, IOptions<FoundryOptions> options, ILogger<CaseAssistant> log)
{
    private const string AssistantSignature =
        "\n\n— Relay Assistant (AI), Highland Energy. A member of our team will follow up if you still need help.";

    public async Task TriageAsync(Guid caseId, CancellationToken ct)
    {
        if (await crm.GetCaseAsync(caseId, ct) is not { } c || c.Insights is not null) return;

        var triage = (await agents.Triage.RunAsync<TriageResult>(Describe(c), serializerOptions: RelayAgents.Json, cancellationToken: ct)).Result;
        c.ApplyTriage(triage.Summary, triage.Sentiment, triage.Category, triage.Priority,
            Math.Clamp(triage.Confidence, 0, 1), triage.SafetyRisk, triage.Reason, options.Value.AutoApplyConfidence, clock.GetUtcNow());

        if (options.Value.AutoAnswer && triage.SelfServiceCandidate && !triage.SafetyRisk && c.Status == CaseStatus.New)
        {
            var answer = (await agents.Answer.RunAsync<KnowledgeAnswer>(Describe(c), serializerOptions: RelayAgents.Json, cancellationToken: ct)).Result;
            if (answer.CanAnswer && !string.IsNullOrWhiteSpace(answer.Reply))
            {
                var sources = answer.Sources.Length > 0 ? $"\n\nHelpful articles: {string.Join("; ", answer.Sources)}" : "";
                c.AddMessage(AuthorType.AiAgent, "Relay Assistant", answer.Reply.Trim() + sources + AssistantSignature, false, clock.GetUtcNow());
            }
        }

        await crm.SaveCaseAsync(c, ct);
        log.LogInformation("Triaged {Case}: {Category}/{Priority} at {Confidence:P0}, applied={Applied}",
            c.Reference, triage.Category, triage.Priority, triage.Confidence, c.Insights!.Applied);
    }

    public async Task<DraftReply?> DraftAsync(Guid caseId, Guid staffId, CancellationToken ct)
    {
        if (await crm.GetCaseAsync(caseId, ct) is not { } c) return null;
        var staff = await crm.GetStaffAsync(staffId, ct);

        var prompt = new StringBuilder(Describe(c, includeInternal: true))
            .AppendLine($"Write the next reply to the customer. You are drafting for {staff?.Name ?? "a support agent"} ({staff?.Team}).")
            .ToString();
        return (await agents.Draft.RunAsync<DraftReply>(prompt, serializerOptions: RelayAgents.Json, cancellationToken: ct)).Result;
    }

    public async Task<bool> SummarizeAsync(Guid caseId, CancellationToken ct)
    {
        if (await crm.GetCaseAsync(caseId, ct) is not { } c) return false;
        var result = (await agents.Summarize.RunAsync<ConversationSummary>(Describe(c, includeInternal: true), serializerOptions: RelayAgents.Json, cancellationToken: ct)).Result;
        c.UpdateSummary(result.Summary, result.Sentiment, clock.GetUtcNow());
        await crm.SaveCaseAsync(c, ct);
        return true;
    }

    private static string Describe(SupportCase c, bool includeInternal = false)
    {
        var sb = new StringBuilder()
            .AppendLine($"Case {c.Reference}: \"{c.Title}\"")
            .AppendLine($"Current category: {c.Category}. Priority: {c.Priority}. Status: {c.Status}. Channel: {c.Channel}.")
            .AppendLine($"Customer: {c.Customer.FirstName} {c.Customer.LastName}, account {c.Customer.Account.AccountNumber}, " +
                        $"{c.Customer.Account.Plan} plan, {c.Customer.Account.City}, balance ${c.Customer.Account.Balance:0.00}.")
            .AppendLine()
            .AppendLine("Conversation, oldest first:");

        foreach (var m in c.Messages.Where(m => includeInternal || !m.IsInternal).OrderBy(m => m.CreatedAt))
        {
            if (m.AuthorType == AuthorType.Customer)
            {
                sb.AppendLine($"<customer_message at=\"{m.CreatedAt:u}\">{m.Body}</customer_message>");
            }
            else
            {
                var label = m.IsInternal ? "internal note" : "reply to customer";
                sb.AppendLine($"[{m.AuthorName}, {label}, {m.CreatedAt:u}] {m.Body}");
            }
        }
        return sb.ToString();
    }
}
