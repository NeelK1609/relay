namespace Relay.Domain;

public sealed record CaseQuery(
    CaseStatus? Status = null,
    CasePriority? Priority = null,
    CaseCategory? Category = null,
    Guid? CustomerId = null,
    Guid? AssignedStaffId = null,
    bool OpenOnly = false,
    string? Search = null,
    int Take = 100);

public sealed record CaseStats(
    int Open,
    int New,
    int WaitingOnCustomer,
    int ResolvedToday,
    int FirstResponseBreached,
    int ResolutionBreached,
    IReadOnlyDictionary<CaseCategory, int> OpenByCategory);

/// <summary>CRM persistence boundary; implemented by SQL today and Dataverse later.</summary>
public interface ICrmStore
{
    Task<IReadOnlyList<Customer>> ListCustomersAsync(CancellationToken ct);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Staff>> ListStaffAsync(CancellationToken ct);
    Task<Staff?> GetStaffAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<SupportCase>> ListCasesAsync(CaseQuery query, CancellationToken ct);
    Task<SupportCase?> GetCaseAsync(Guid id, CancellationToken ct);
    Task AddCaseAsync(SupportCase supportCase, CancellationToken ct);
    Task SaveCaseAsync(SupportCase supportCase, CancellationToken ct);
    Task<CaseStats> GetStatsAsync(DateTimeOffset now, CancellationToken ct);

    Task<IReadOnlyList<KnowledgeArticle>> SearchKnowledgeAsync(string? text, CaseCategory? category, CancellationToken ct);
    Task<KnowledgeArticle?> GetArticleAsync(Guid id, CancellationToken ct);
}
