using Relay.Channels;
using Relay.Domain;

namespace Relay.Api;

public sealed record ChatMessageRequest(string Text);

public sealed record StaffChatRequest(Guid StaffId, string? Text = null);

/// <summary>Live chat for customers (portal) and staff (workspace).</summary>
public static class ChatEndpoints
{
    private const int MaxMessageLength = 1000;

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal/customers/{customerId:guid}/chats").WithTags("Chat");

        portal.MapPost("/", async (Guid customerId, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            if (await crm.GetCustomerAsync(customerId, ct) is not { } customer) return Results.NotFound();
            return Results.Ok(await chats.StartAsync(customer, ct));
        });

        portal.MapGet("/{chatId:guid}", async (Guid customerId, Guid chatId, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            return await crm.GetChatAsync(chatId, ct) is { } chat && chat.CustomerId == customerId
                ? Results.Ok(await chats.ConnectCustomerAsync(chat, ct))
                : Results.NotFound();
        });

        portal.MapPost("/{chatId:guid}/messages", async (Guid customerId, Guid chatId, ChatMessageRequest req, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            if (InvalidText(req.Text) is { } invalid) return invalid;
            if (await crm.GetChatAsync(chatId, ct) is not { } chat || chat.CustomerId != customerId) return Results.NotFound();
            return Results.Ok(await chats.CustomerSaysAsync(chat, req.Text.Trim(), ct));
        });

        portal.MapPost("/{chatId:guid}/end", async (Guid customerId, Guid chatId, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            if (await crm.GetChatAsync(chatId, ct) is not { } chat || chat.CustomerId != customerId) return Results.NotFound();
            await chats.EndAsync(chat, chat.Customer.FirstName, ct);
            return Results.NoContent();
        });

        var ws = app.MapGroup("/api/workspace/cases/{caseId:guid}/chat").WithTags("Chat");

        ws.MapGet("/", async (Guid caseId, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            return await crm.GetChatForCaseAsync(caseId, ct) is { } chat
                ? Results.Ok(await chats.StateAsync(chat, ct))
                : Results.NotFound();
        });

        ws.MapPost("/join", async (Guid caseId, StaffChatRequest req, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            if (await crm.GetChatForCaseAsync(caseId, ct) is not { } chat) return Results.NotFound();
            if (await crm.GetStaffAsync(req.StaffId, ct) is not { } staff) return Results.BadRequest("Unknown staff member.");
            return Results.Ok(await chats.JoinAsync(chat, staff, ct));
        });

        ws.MapPost("/messages", async (Guid caseId, StaffChatRequest req, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            if (InvalidText(req.Text) is { } invalid) return invalid;
            if (await crm.GetChatForCaseAsync(caseId, ct) is not { } chat) return Results.NotFound();
            if (await crm.GetStaffAsync(req.StaffId, ct) is not { } staff) return Results.BadRequest("Unknown staff member.");
            await chats.StaffSaysAsync(chat, staff, req.Text!.Trim(), ct);
            return Results.NoContent();
        });

        ws.MapPost("/end", async (Guid caseId, StaffChatRequest req, IServiceProvider sp, ICrmStore crm, CancellationToken ct) =>
        {
            if (sp.GetService<ChatService>() is not { } chats) return ChatDisabled();
            if (await crm.GetChatForCaseAsync(caseId, ct) is not { } chat) return Results.NotFound();
            if (await crm.GetStaffAsync(req.StaffId, ct) is not { } staff) return Results.BadRequest("Unknown staff member.");
            await chats.EndAsync(chat, staff.Name, ct);
            return Results.NoContent();
        });
    }

    private static IResult? InvalidText(string? text) =>
        string.IsNullOrWhiteSpace(text) || text.Length > MaxMessageLength
            ? Results.BadRequest($"Messages must be 1 to {MaxMessageLength:N0} characters.")
            : null;

    private static IResult ChatDisabled() => Results.Problem(
        title: "Live chat is not configured.",
        detail: "Set Communication:Endpoint and Foundry:Endpoint to enable it.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
