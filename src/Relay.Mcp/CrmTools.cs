using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Relay.Agents;
using Relay.Domain;

namespace Relay.Mcp;

/// <summary>
/// CRM tools for staff-facing assistants. There is deliberately no tool that messages customers:
/// anything a customer sees goes through a person in the workspace.
/// </summary>
[McpServerToolType]
public sealed class CrmTools(ICrmStore crm, TimeProvider clock, IServiceProvider services, IHttpContextAccessor http)
{
    [McpServerTool(Name = "search_cases", Title = "Search cases", ReadOnly = true, OpenWorld = false)]
    [Description("Finds support cases, most urgent first. All filters are optional; by default only open cases are returned.")]
    public async Task<IReadOnlyList<CaseRow>> SearchCases(
        [Description("Words from the title, a customer name, an account number (HE-…) or a case reference (CAS-…).")] string? query = null,
        [Description("New, InProgress, WaitingOnCustomer, Resolved or Closed.")] string? status = null,
        [Description("Low, Normal, High or Urgent.")] string? priority = null,
        [Description("Outage, Billing, Payment, MoveService, Meter or General.")] string? category = null,
        [Description("A staff member's name to filter by owner, or \"unassigned\".")] string? owner = null,
        [Description("Include resolved and closed cases. Ignored when a status is given.")] bool includeClosed = false,
        [Description("Maximum number of cases, 1 to 50.")] int limit = 20,
        CancellationToken ct = default)
    {
        var statusFilter = ParseEnum<CaseStatus>(status, nameof(status));
        var cases = await crm.ListCasesAsync(new CaseQuery(
            Status: statusFilter,
            Priority: ParseEnum<CasePriority>(priority, nameof(priority)),
            Category: ParseEnum<CaseCategory>(category, nameof(category)),
            OpenOnly: statusFilter is null && !includeClosed,
            Search: query,
            Take: 500), ct);

        IEnumerable<SupportCase> filtered = cases;
        if (!string.IsNullOrWhiteSpace(owner))
        {
            filtered = owner.Equals("unassigned", StringComparison.OrdinalIgnoreCase)
                ? filtered.Where(c => c.AssignedTo is null)
                : filtered.Where(c => c.AssignedTo?.Name.Contains(owner, StringComparison.OrdinalIgnoreCase) == true);
        }

        var now = clock.GetUtcNow();
        return filtered.Take(Math.Clamp(limit, 1, 50)).Select(c => CaseRow.From(c, now)).ToList();
    }

    [McpServerTool(Name = "get_case", Title = "Get case details", ReadOnly = true, OpenWorld = false)]
    [Description("Returns one case with its full conversation (including internal notes), customer details and AI insights.")]
    public async Task<CaseDetail> GetCase(
        [Description("Case reference such as CAS-001001.")] string reference,
        CancellationToken ct = default)
    {
        var c = await FindCaseAsync(reference, ct);
        return CaseDetail.From(c, clock.GetUtcNow());
    }

    [McpServerTool(Name = "find_customer", Title = "Find a customer", ReadOnly = true, OpenWorld = false)]
    [Description("Looks up customers by name, email or account number and returns their account details and recent cases.")]
    public async Task<IReadOnlyList<CustomerProfile>> FindCustomer(
        [Description("Part of a name, an email address, or an account number (HE-…).")] string query,
        CancellationToken ct = default)
    {
        var term = query.Trim();
        var matches = (await crm.ListCustomersAsync(ct))
            .Where(c => c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || c.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || c.Account.AccountNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        var now = clock.GetUtcNow();
        var profiles = new List<CustomerProfile>();
        foreach (var c in matches)
        {
            var cases = await crm.ListCasesAsync(new CaseQuery(CustomerId: c.Id, Take: 10), ct);
            profiles.Add(CustomerProfile.From(c, cases.OrderByDescending(x => x.CreatedAt).Select(x => CaseRow.From(x, now)).ToList()));
        }
        return profiles;
    }

    [McpServerTool(Name = "search_help_centre", Title = "Search help centre", ReadOnly = true, OpenWorld = false)]
    [Description("Searches Highland Energy's help articles by meaning and returns the best matches with full text.")]
    public async Task<IReadOnlyList<HelpArticle>> SearchHelpCentre(
        [Description("What to look up, in plain words.")] string query,
        CancellationToken ct = default)
    {
        if (services.GetService<KnowledgeIndex>() is { } index)
        {
            return (await index.SearchAsync(query, top: 3, ct))
                .Select(h => new HelpArticle(h.Title, h.Category.ToString(), h.Body, Math.Round(h.Score, 2)))
                .ToList();
        }

        return (await crm.SearchKnowledgeAsync(query, null, ct))
            .Take(3)
            .Select(a => new HelpArticle(a.Title, a.Category.ToString(), a.Body, null))
            .ToList();
    }

    [McpServerTool(Name = "get_queue_stats", Title = "Queue statistics", ReadOnly = true, OpenWorld = false)]
    [Description("Current queue health: open, new and waiting cases, SLA breaches, cases resolved today, and open cases by category.")]
    public Task<CaseStats> GetQueueStats(CancellationToken ct = default) => crm.GetStatsAsync(clock.GetUtcNow(), ct);

    [McpServerTool(Name = "add_internal_note", Title = "Add internal note", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Adds an internal note to a case. Internal notes are visible to staff only, never to the customer.")]
    public async Task<string> AddInternalNote(
        McpServer server,
        [Description("Case reference such as CAS-001001.")] string reference,
        [Description("The note text.")] string note,
        CancellationToken ct = default)
    {
        var c = await FindCaseAsync(reference, ct);
        await ApplyRulesAsync(() =>
        {
            c.AddMessage(AuthorType.AiAgent, AuthorFor(server), note, isInternal: true, clock.GetUtcNow());
            return crm.SaveCaseAsync(c, ct);
        });
        return $"Added an internal note to {c.Reference}.";
    }

    [McpServerTool(Name = "update_case", Title = "Update case", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Changes a case's status, priority, category or owner. Only provide the fields to change. Closing a case cannot be undone.")]
    public async Task<CaseRow> UpdateCase(
        McpServer server,
        [Description("Case reference such as CAS-001001.")] string reference,
        [Description("New, InProgress, WaitingOnCustomer, Resolved or Closed.")] string? status = null,
        [Description("Low, Normal, High or Urgent.")] string? priority = null,
        [Description("Outage, Billing, Payment, MoveService, Meter or General.")] string? category = null,
        [Description("Staff member's name to assign, or \"unassigned\" to clear the owner.")] string? owner = null,
        CancellationToken ct = default)
    {
        var c = await FindCaseAsync(reference, ct);
        var now = clock.GetUtcNow();
        var changes = new List<string>();
        Staff? newOwner = null;

        if (!string.IsNullOrWhiteSpace(owner) && !owner.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
        {
            var all = await crm.ListStaffAsync(ct);
            newOwner = all.FirstOrDefault(s => s.Name.Contains(owner, StringComparison.OrdinalIgnoreCase))
                       ?? throw new McpException($"No staff member matches \"{owner}\". Staff: {string.Join(", ", all.Select(s => s.Name))}.");
        }

        await ApplyRulesAsync(() =>
        {
            if (!string.IsNullOrWhiteSpace(owner))
            {
                c.AssignTo(newOwner, now);
                changes.Add($"owner → {newOwner?.Name ?? "unassigned"}");
            }
            if (ParseEnum<CaseCategory>(category, nameof(category)) is { } cat) { c.ChangeCategory(cat, now); changes.Add($"category → {cat}"); }
            if (ParseEnum<CasePriority>(priority, nameof(priority)) is { } pri) { c.ChangePriority(pri, now); changes.Add($"priority → {pri}"); }
            if (ParseEnum<CaseStatus>(status, nameof(status)) is { } st) { c.ChangeStatus(st, now); changes.Add($"status → {st}"); }

            if (changes.Count == 0) throw new McpException("Nothing to change. Provide status, priority, category or owner.");

            c.AddMessage(AuthorType.AiAgent, AuthorFor(server), $"Updated via MCP: {string.Join(", ", changes)}.", isInternal: true, now);
            return crm.SaveCaseAsync(c, ct);
        });
        return CaseRow.From(c, now);
    }

    private static async Task ApplyRulesAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is DomainException or ConcurrencyConflictException)
        {
            throw new McpException(ex.Message);
        }
    }

    private async Task<SupportCase> FindCaseAsync(string reference, CancellationToken ct)
    {
        var digits = new string(reference.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out var number)) throw new McpException($"\"{reference}\" is not a case reference like CAS-001001.");

        var match = (await crm.ListCasesAsync(new CaseQuery(Search: reference, Take: 50), ct)).FirstOrDefault(c => c.Number == number)
                    ?? throw new McpException($"Case {reference} was not found.");
        return (await crm.GetCaseAsync(match.Id, ct))!;
    }

    private string AuthorFor(McpServer server)
    {
        var staff = http.HttpContext?.Request.Headers["X-Relay-Staff"].ToString();
        var client = server.ClientInfo?.Name ?? "MCP client";
        return string.IsNullOrWhiteSpace(staff) ? $"{client} (MCP)" : $"{staff} via {client}";
    }

    private static T? ParseEnum<T>(string? value, string name) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Replace(" ", "").Replace("_", "");
        return Enum.TryParse<T>(normalized, ignoreCase: true, out var parsed)
            ? parsed
            : throw new McpException($"Unknown {name} \"{value}\". Use one of: {string.Join(", ", Enum.GetNames<T>())}.");
    }
}
