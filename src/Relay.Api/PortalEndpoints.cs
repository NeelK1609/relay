using Relay.Agents;
using Relay.Domain;

namespace Relay.Api;

/// <summary>Customer-facing endpoints. Every route is scoped to one customer and never returns internal notes.</summary>
public static class PortalEndpoints
{
    public static void MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal/customers/{customerId:guid}").WithTags("Portal");

        portal.MapGet("/", async (Guid customerId, ICrmStore crm, CancellationToken ct) =>
            await crm.GetCustomerAsync(customerId, ct) is { } c ? Results.Ok(CustomerDto.From(c)) : Results.NotFound());

        portal.MapGet("/cases", async (Guid customerId, ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
        {
            var cases = await crm.ListCasesAsync(new CaseQuery(CustomerId: customerId), ct);
            var now = clock.GetUtcNow();
            return cases.OrderByDescending(c => c.CreatedAt).Select(c => CaseSummaryDto.From(c, now));
        });

        portal.MapPost("/cases", async (Guid customerId, OpenCaseRequest req, ICrmStore crm, ITriageQueue triage, ICaseNotifier notifier, TimeProvider clock, CancellationToken ct) =>
        {
            if (await crm.GetCustomerAsync(customerId, ct) is not { } customer) return Results.NotFound();

            // Starting priority until AI triage (or a person) refines it.
            var priority = req.Category == CaseCategory.Outage ? CasePriority.High : CasePriority.Normal;
            var now = clock.GetUtcNow();
            var supportCase = SupportCase.Open(customer, req.Title, req.Description, req.Category, priority, req.Channel, now);
            await crm.AddCaseAsync(supportCase, ct);
            await triage.EnqueueAsync(supportCase.Id, ct);
            await notifier.NotifyAsync(new CaseNotification(supportCase.Id, NotificationKind.CaseOpened), ct);

            var saved = await crm.GetCaseAsync(supportCase.Id, ct);
            return Results.Created($"/api/portal/customers/{customerId}/cases/{supportCase.Id}",
                CaseDetailDto.From(saved!, now, includeInternal: false));
        });

        portal.MapGet("/cases/{caseId:guid}", async (Guid customerId, Guid caseId, ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
            await crm.GetCaseAsync(caseId, ct) is { } c && c.CustomerId == customerId
                ? Results.Ok(CaseDetailDto.From(c, clock.GetUtcNow(), includeInternal: false))
                : Results.NotFound());

        portal.MapPost("/cases/{caseId:guid}/messages", async (Guid customerId, Guid caseId, CustomerReplyRequest req, ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
        {
            if (await crm.GetCaseAsync(caseId, ct) is not { } c || c.CustomerId != customerId) return Results.NotFound();
            var now = clock.GetUtcNow();
            c.AddMessage(AuthorType.Customer, c.Customer.FullName, req.Body, isInternal: false, now);
            await crm.SaveCaseAsync(c, ct);
            return Results.Ok(CaseDetailDto.From(c, now, includeInternal: false));
        });
    }
}
