using Relay.Domain;

namespace Relay.Mcp;

public sealed record CaseRow(
    string Reference, string Title, string Status, string Priority, string Category, string Channel,
    string Customer, string AccountNumber, string? Owner, string? Sentiment, bool SafetyRisk,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset ResolutionDueAt, bool SlaBreached)
{
    public static CaseRow From(SupportCase c, DateTimeOffset now) => new(
        c.Reference, c.Title, c.Status.ToString(), c.Priority.ToString(), c.Category.ToString(), c.Channel.ToString(),
        c.Customer.FullName, c.Customer.Account.AccountNumber, c.AssignedTo?.Name,
        c.Insights?.Sentiment.ToString(), c.Insights?.SafetyRisk ?? false,
        c.CreatedAt, c.UpdatedAt, c.ResolutionDueAt,
        c.IsOpen && (c.IsFirstResponseBreached(now) || c.IsResolutionBreached(now)));
}

public sealed record CaseMessageView(string From, string Kind, DateTimeOffset At, string Body);

public sealed record CaseDetail(CaseRow Case, CustomerProfile Customer, string? AiSummary, IReadOnlyList<CaseMessageView> Conversation)
{
    public static CaseDetail From(SupportCase c, DateTimeOffset now) => new(
        CaseRow.From(c, now),
        CustomerProfile.From(c.Customer, []),
        c.Insights?.Summary,
        c.Messages.OrderBy(m => m.CreatedAt).Select(m => new CaseMessageView(
            m.AuthorName,
            m.IsInternal ? "internal note" : m.AuthorType == AuthorType.Customer ? "customer message" : "reply to customer",
            m.CreatedAt,
            m.Body)).ToList());
}

public sealed record CustomerProfile(
    string Name, string Email, string Phone, string AccountNumber, string ServiceAddress, string City,
    string Plan, decimal Balance, int CustomerSinceYear, IReadOnlyList<CaseRow> RecentCases)
{
    public static CustomerProfile From(Customer c, IReadOnlyList<CaseRow> cases) => new(
        c.FullName, c.Email, c.Phone, c.Account.AccountNumber, c.Account.ServiceAddress, c.Account.City,
        c.Account.Plan, c.Account.Balance, c.CustomerSince.Year, cases);
}

public sealed record HelpArticle(string Title, string Category, string Body, double? Relevance);
