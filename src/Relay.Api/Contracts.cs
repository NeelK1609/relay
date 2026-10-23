using Relay.Domain;

namespace Relay.Api;

public sealed record CustomerDto(
    Guid Id, string FullName, string Email, string Phone, string AccountNumber,
    string ServiceAddress, string City, string Plan, decimal Balance, DateTimeOffset CustomerSince)
{
    public static CustomerDto From(Customer c) => new(
        c.Id, c.FullName, c.Email, c.Phone, c.Account.AccountNumber,
        c.Account.ServiceAddress, c.Account.City, c.Account.Plan, c.Account.Balance, c.CustomerSince);
}

public sealed record StaffDto(Guid Id, string Name, string Email, string Team)
{
    public static StaffDto From(Staff s) => new(s.Id, s.Name, s.Email, s.Team);
}

public sealed record CaseSummaryDto(
    Guid Id, string Reference, string Title, CaseCategory Category, CasePriority Priority, CaseStatus Status,
    Channel Channel, Guid CustomerId, string CustomerName, string AccountNumber,
    Guid? AssignedStaffId, string? AssignedStaffName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset FirstResponseDueAt, DateTimeOffset ResolutionDueAt,
    bool FirstResponseBreached, bool ResolutionBreached)
{
    public static CaseSummaryDto From(SupportCase c, DateTimeOffset now) => new(
        c.Id, c.Reference, c.Title, c.Category, c.Priority, c.Status, c.Channel,
        c.CustomerId, c.Customer.FullName, c.Customer.Account.AccountNumber,
        c.AssignedStaffId, c.AssignedTo?.Name, c.CreatedAt, c.UpdatedAt,
        c.FirstResponseDueAt, c.ResolutionDueAt,
        c.IsOpen && c.IsFirstResponseBreached(now), c.IsOpen && c.IsResolutionBreached(now));
}

public sealed record MessageDto(Guid Id, AuthorType AuthorType, string AuthorName, string Body, bool IsInternal, DateTimeOffset CreatedAt)
{
    public static MessageDto From(CaseMessage m) => new(m.Id, m.AuthorType, m.AuthorName, m.Body, m.IsInternal, m.CreatedAt);
}

public sealed record CaseDetailDto(CaseSummaryDto Summary, CustomerDto Customer, IReadOnlyList<MessageDto> Messages)
{
    public static CaseDetailDto From(SupportCase c, DateTimeOffset now, bool includeInternal) => new(
        CaseSummaryDto.From(c, now),
        CustomerDto.From(c.Customer),
        c.Messages.Where(m => includeInternal || !m.IsInternal).OrderBy(m => m.CreatedAt).Select(MessageDto.From).ToList());
}

public sealed record ArticleDto(Guid Id, string Title, CaseCategory Category, string Body, DateTimeOffset UpdatedAt)
{
    public static ArticleDto From(KnowledgeArticle a) => new(a.Id, a.Title, a.Category, a.Body, a.UpdatedAt);
}

public sealed record OpenCaseRequest(string Title, string Description, CaseCategory Category, Channel Channel = Channel.Portal);

public sealed record CustomerReplyRequest(string Body);

public sealed record StaffMessageRequest(Guid StaffId, string Body, bool IsInternal);

public sealed record UpdateCaseRequest(
    CaseStatus? Status = null,
    CasePriority? Priority = null,
    CaseCategory? Category = null,
    Guid? AssignedStaffId = null,
    bool Unassign = false);
