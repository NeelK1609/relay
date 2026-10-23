using Relay.Domain;

namespace Relay.Api;

/// <summary>Endpoints for support staff working cases in the agent workspace.</summary>
public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var ws = app.MapGroup("/api/workspace").WithTags("Workspace");

        ws.MapGet("/staff", async (ICrmStore crm, CancellationToken ct) =>
            (await crm.ListStaffAsync(ct)).Select(StaffDto.From));

        ws.MapGet("/customers", async (ICrmStore crm, CancellationToken ct) =>
            (await crm.ListCustomersAsync(ct)).Select(CustomerDto.From));

        ws.MapGet("/stats", async (ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
            await crm.GetStatsAsync(clock.GetUtcNow(), ct));

        ws.MapGet("/cases", async (
            CaseStatus? status, CasePriority? priority, CaseCategory? category,
            Guid? customerId, Guid? assignedStaffId, bool? openOnly, string? search,
            ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
        {
            var query = new CaseQuery(status, priority, category, customerId, assignedStaffId, openOnly ?? false, search);
            var now = clock.GetUtcNow();
            return (await crm.ListCasesAsync(query, ct)).Select(c => CaseSummaryDto.From(c, now));
        });

        ws.MapGet("/cases/{caseId:guid}", async (Guid caseId, ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
            await crm.GetCaseAsync(caseId, ct) is { } c
                ? Results.Ok(CaseDetailDto.From(c, clock.GetUtcNow(), includeInternal: true))
                : Results.NotFound());

        ws.MapPatch("/cases/{caseId:guid}", async (Guid caseId, UpdateCaseRequest req, ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
        {
            if (await crm.GetCaseAsync(caseId, ct) is not { } c) return Results.NotFound();
            var now = clock.GetUtcNow();

            if (req.Unassign) c.AssignTo(null, now);
            else if (req.AssignedStaffId is { } staffId)
            {
                if (await crm.GetStaffAsync(staffId, ct) is not { } staff) return Results.BadRequest("Unknown staff member.");
                c.AssignTo(staff, now);
            }
            if (req.Category is { } category) c.ChangeCategory(category, now);
            if (req.Priority is { } priority) c.ChangePriority(priority, now);
            if (req.Status is { } status) c.ChangeStatus(status, now);

            await crm.SaveCaseAsync(c, ct);
            return Results.Ok(CaseDetailDto.From(c, now, includeInternal: true));
        });

        ws.MapPost("/cases/{caseId:guid}/messages", async (Guid caseId, StaffMessageRequest req, ICrmStore crm, ICaseNotifier notifier, TimeProvider clock, CancellationToken ct) =>
        {
            if (await crm.GetCaseAsync(caseId, ct) is not { } c) return Results.NotFound();
            if (await crm.GetStaffAsync(req.StaffId, ct) is not { } staff) return Results.BadRequest("Unknown staff member.");

            var now = clock.GetUtcNow();
            c.AddMessage(AuthorType.Staff, staff.Name, req.Body, req.IsInternal, now);
            await crm.SaveCaseAsync(c, ct);
            if (!req.IsInternal) await notifier.NotifyAsync(new CaseNotification(c.Id, NotificationKind.NewReply), ct);
            return Results.Ok(CaseDetailDto.From(c, now, includeInternal: true));
        });
    }

    public static void MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var kb = app.MapGroup("/api/knowledge").WithTags("Knowledge");

        kb.MapGet("/", async (string? q, CaseCategory? category, ICrmStore crm, CancellationToken ct) =>
            (await crm.SearchKnowledgeAsync(q, category, ct)).Select(ArticleDto.From));

        kb.MapGet("/{id:guid}", async (Guid id, ICrmStore crm, CancellationToken ct) =>
            await crm.GetArticleAsync(id, ct) is { } a ? Results.Ok(ArticleDto.From(a)) : Results.NotFound());
    }
}
