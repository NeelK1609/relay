using Microsoft.Extensions.Options;
using Relay.Agents;
using Relay.Domain;

namespace Relay.Api;

/// <summary>On-demand AI actions for staff in the workspace.</summary>
public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspace/ask", async (AskRequest req, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<OpsCopilot>() is not { } copilot) return AiDisabled();
            if (string.IsNullOrWhiteSpace(req.Question) || req.Question.Length > 1000) return Results.BadRequest("Ask a question of up to 1,000 characters.");
            if (await crm.GetStaffAsync(req.StaffId, ct) is not { } staff) return Results.BadRequest("Unknown staff member.");
            return Results.Ok(await copilot.AskAsync(staff, req.Question.Trim(), ct));
        }).WithTags("AI");

        var ai = app.MapGroup("/api/workspace/cases/{caseId:guid}/ai").WithTags("AI");

        ai.MapGet("/status", (IOptions<FoundryOptions> options) => new { enabled = options.Value.IsConfigured });

        ai.MapPost("/draft", async (Guid caseId, DraftRequest req, IServiceProvider sp, CancellationToken ct) =>
        {
            if (sp.GetService<CaseAssistant>() is not { } assistant) return AiDisabled();
            return await assistant.DraftAsync(caseId, req.StaffId, ct) is { } draft ? Results.Ok(draft) : Results.NotFound();
        });

        ai.MapPost("/summarize", async (Guid caseId, IServiceProvider sp, ICrmStore crm, TimeProvider clock, CancellationToken ct) =>
        {
            if (sp.GetService<CaseAssistant>() is not { } assistant) return AiDisabled();
            if (!await assistant.SummarizeAsync(caseId, ct)) return Results.NotFound();
            var c = await crm.GetCaseAsync(caseId, ct);
            return Results.Ok(CaseDetailDto.From(c!, clock.GetUtcNow(), includeInternal: true));
        });
    }

    private static IResult AiDisabled() => Results.Problem(
        title: "AI features are not configured.",
        detail: "Set Foundry:Endpoint to enable them.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
