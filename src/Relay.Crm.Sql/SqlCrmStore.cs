using Microsoft.EntityFrameworkCore;
using Relay.Domain;

namespace Relay.Crm.Sql;

public sealed class SqlCrmStore(RelayDbContext db) : ICrmStore
{
    public async Task<IReadOnlyList<Customer>> ListCustomersAsync(CancellationToken ct) =>
        await db.Customers.Include(c => c.Account).OrderBy(c => c.LastName).ToListAsync(ct);

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken ct) =>
        db.Customers.Include(c => c.Account).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Staff>> ListStaffAsync(CancellationToken ct) =>
        await db.Staff.OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Staff?> GetStaffAsync(Guid id, CancellationToken ct) =>
        db.Staff.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SupportCase>> ListCasesAsync(CaseQuery q, CancellationToken ct)
    {
        var cases = db.Cases
            .Include(c => c.Customer).ThenInclude(c => c.Account)
            .Include(c => c.AssignedTo)
            .AsNoTracking();

        if (q.Status is { } status) cases = cases.Where(c => c.Status == status);
        if (q.Priority is { } priority) cases = cases.Where(c => c.Priority == priority);
        if (q.Category is { } category) cases = cases.Where(c => c.Category == category);
        if (q.CustomerId is { } customerId) cases = cases.Where(c => c.CustomerId == customerId);
        if (q.AssignedStaffId is { } staffId) cases = cases.Where(c => c.AssignedStaffId == staffId);
        if (q.OpenOnly) cases = cases.Where(c => c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Closed);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var number = int.TryParse(digits, out var n) ? n : -1;
            cases = cases.Where(c =>
                c.Title.Contains(term) ||
                c.Number == number ||
                (c.Customer.FirstName + " " + c.Customer.LastName).Contains(term) ||
                c.Customer.Account.AccountNumber.Contains(term));
        }

        return await cases
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.ResolutionDueAt)
            .Take(Math.Clamp(q.Take, 1, 500))
            .ToListAsync(ct);
    }

    public Task<SupportCase?> GetCaseAsync(Guid id, CancellationToken ct) =>
        db.Cases
            .Include(c => c.Customer).ThenInclude(c => c.Account)
            .Include(c => c.AssignedTo)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddCaseAsync(SupportCase supportCase, CancellationToken ct)
    {
        db.Cases.Add(supportCase);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveCaseAsync(SupportCase supportCase, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public async Task<CaseStats> GetStatsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var startOfDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var open = db.Cases.Where(c => c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Closed);

        var byCategory = await open
            .GroupBy(c => c.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return new CaseStats(
            Open: await open.CountAsync(ct),
            New: await open.CountAsync(c => c.Status == CaseStatus.New, ct),
            WaitingOnCustomer: await open.CountAsync(c => c.Status == CaseStatus.WaitingOnCustomer, ct),
            ResolvedToday: await db.Cases.CountAsync(c => c.ResolvedAt >= startOfDay, ct),
            FirstResponseBreached: await open.CountAsync(c => (c.FirstRespondedAt ?? now) > c.FirstResponseDueAt, ct),
            ResolutionBreached: await open.CountAsync(c => c.ResolutionDueAt < now, ct),
            OpenByCategory: byCategory);
    }

    public async Task<IReadOnlyList<KnowledgeArticle>> SearchKnowledgeAsync(string? text, CaseCategory? category, CancellationToken ct)
    {
        var articles = db.KnowledgeArticles.AsNoTracking();
        if (category is { } cat) articles = articles.Where(a => a.Category == cat);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim();
            articles = articles.Where(a => a.Title.Contains(term) || a.Body.Contains(term));
        }
        return await articles.OrderBy(a => a.Title).Take(50).ToListAsync(ct);
    }

    public Task<KnowledgeArticle?> GetArticleAsync(Guid id, CancellationToken ct) =>
        db.KnowledgeArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
}
