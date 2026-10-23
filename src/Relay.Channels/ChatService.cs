using System.Text;
using Microsoft.Agents.AI;
using Relay.Agents;
using Relay.Domain;

namespace Relay.Channels;

public sealed record ChatConnection(
    Guid SessionId, string Endpoint, string ThreadId, string UserId, string DisplayName,
    string Token, DateTimeOffset ExpiresOn, ChatStatus Status, Guid? CaseId, string? CaseReference);

public sealed record ChatState(Guid SessionId, ChatStatus Status, Guid? CaseId, string? CaseReference, string? StaffName);

/// <summary>Runs live chats: the assistant answers, hands off by opening a case, and staff can join the same thread.</summary>
public sealed class ChatService(
    ChatGateway gateway, RelayAgents agents, ICrmStore crm, ITriageQueue triage, ICaseNotifier notifier, TimeProvider clock)
{
    private const string AssistantName = "Relay Assistant";
    private const int MaxTranscriptLength = 7000;

    public async Task<ChatConnection> StartAsync(Customer customer, CancellationToken ct)
    {
        var assistantId = await gateway.CreateUserAsync(ct);
        var customerId = await gateway.CreateUserAsync(ct);
        var threadId = await gateway.CreateThreadAsync($"Highland Energy chat with {customer.FullName}",
            (assistantId, AssistantName), (customerId, customer.FullName), ct);

        await gateway.SendAsync(threadId, assistantId, AssistantName,
            $"Hi {customer.FirstName}! I'm Relay, Highland Energy's virtual assistant. How can I help today?", ct);

        var chat = ChatSession.Start(customer, threadId, customerId, assistantId, clock.GetUtcNow());
        await crm.AddChatAsync(chat, ct);
        return await ConnectAsync(chat, chat.CustomerChatUserId, customer.FullName, ct);
    }

    public Task<ChatConnection> ConnectCustomerAsync(ChatSession chat, CancellationToken ct) =>
        ConnectAsync(chat, chat.CustomerChatUserId, chat.Customer.FullName, ct);

    public async Task<ChatState> CustomerSaysAsync(ChatSession chat, string text, CancellationToken ct)
    {
        if (!chat.IsActive) throw new DomainException("This chat has ended. Start a new one if you need more help.");
        await gateway.SendAsync(chat.ThreadId, chat.CustomerChatUserId, chat.Customer.FullName, text, ct);

        if (chat.CaseId is { } caseId)
        {
            // Already with the team: keep the case record in step with the conversation.
            await AppendToCaseAsync(caseId, AuthorType.Customer, chat.Customer.FullName, text, ct);
            return await StateAsync(chat, ct);
        }

        var transcript = await gateway.GetTranscriptAsync(chat.ThreadId, chat.AssistantChatUserId, ct);
        var turn = (await agents.Chat.RunAsync<ChatTurn>(Describe(chat, transcript),
            serializerOptions: RelayAgents.Json, cancellationToken: ct)).Result;

        if (!turn.HandOff)
        {
            await gateway.SendAsync(chat.ThreadId, chat.AssistantChatUserId, AssistantName, turn.Reply, ct);
            return await StateAsync(chat, ct);
        }

        var now = clock.GetUtcNow();
        var title = string.IsNullOrWhiteSpace(turn.CaseTitle) ? "Live chat request" : turn.CaseTitle.Trim();
        var priority = turn.Category == CaseCategory.Outage ? CasePriority.High : CasePriority.Normal;
        var supportCase = SupportCase.Open(chat.Customer, title, FormatTranscript(transcript), turn.Category, priority, Channel.Chat, now);
        await crm.AddCaseAsync(supportCase, ct);
        chat.HandOff(supportCase, now);
        await crm.SaveChangesAsync(ct);

        var reference = (await crm.GetCaseAsync(supportCase.Id, ct))!.Reference;
        await gateway.SendAsync(chat.ThreadId, chat.AssistantChatUserId, AssistantName,
            $"{turn.Reply}\n\nI've opened case {reference} for you. A member of our team will join this chat shortly, " +
            "and you can follow it any time under My requests.", ct);

        await triage.EnqueueAsync(supportCase.Id, ct);
        await notifier.NotifyAsync(new CaseNotification(supportCase.Id, NotificationKind.CaseOpened), ct);
        return await StateAsync(chat, ct);
    }

    public async Task<ChatConnection> JoinAsync(ChatSession chat, Staff staff, CancellationToken ct)
    {
        if (chat.StaffId == staff.Id && chat.StaffChatUserId is { } existing && chat.Status == ChatStatus.WithAgent)
        {
            return await ConnectAsync(chat, existing, staff.Name, ct);
        }

        var staffUserId = await gateway.CreateUserAsync(ct);
        chat.Join(staff, staffUserId, clock.GetUtcNow());
        await gateway.AddParticipantAsync(chat.ThreadId, chat.AssistantChatUserId, staffUserId, staff.Name, ct);
        await crm.SaveChangesAsync(ct);
        await gateway.SendAsync(chat.ThreadId, chat.AssistantChatUserId, AssistantName,
            $"{staff.Name} from {staff.Team} has joined the chat.", ct);
        return await ConnectAsync(chat, staffUserId, staff.Name, ct);
    }

    public async Task StaffSaysAsync(ChatSession chat, Staff staff, string text, CancellationToken ct)
    {
        if (chat.Status != ChatStatus.WithAgent || chat.StaffId != staff.Id)
        {
            throw new DomainException("Join the chat before sending messages.");
        }

        await gateway.SendAsync(chat.ThreadId, chat.StaffChatUserId!, staff.Name, text, ct);
        await AppendToCaseAsync(chat.CaseId!.Value, AuthorType.Staff, staff.Name, text, ct);
    }

    /// <summary>
    /// Records a chat message on the case. Triage or staff may update the same case at the same moment,
    /// so a conflict reloads the case and tries again rather than dropping the message.
    /// </summary>
    private async Task AppendToCaseAsync(Guid caseId, AuthorType author, string name, string text, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var supportCase = (await crm.GetCaseAsync(caseId, ct))!;
                supportCase.AddMessage(author, name, text, isInternal: false, clock.GetUtcNow());
                await crm.SaveChangesAsync(ct);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < 3)
            {
                crm.DiscardChanges();
            }
        }
    }

    public async Task EndAsync(ChatSession chat, string endedBy, CancellationToken ct)
    {
        if (!chat.IsActive) return;
        await gateway.SendAsync(chat.ThreadId, chat.AssistantChatUserId, AssistantName,
            $"{endedBy} ended the chat. Thanks for contacting Highland Energy.", ct);
        chat.End(clock.GetUtcNow());
        await crm.SaveChangesAsync(ct);
    }

    public async Task<ChatState> StateAsync(ChatSession chat, CancellationToken ct)
    {
        var reference = chat.CaseId is { } id ? (await crm.GetCaseAsync(id, ct))?.Reference : null;
        return new ChatState(chat.Id, chat.Status, chat.CaseId, reference, chat.Staff?.Name);
    }

    private async Task<ChatConnection> ConnectAsync(ChatSession chat, string userId, string displayName, CancellationToken ct)
    {
        var token = await gateway.GetTokenAsync(userId, ct);
        var state = await StateAsync(chat, ct);
        return new ChatConnection(chat.Id, gateway.Endpoint.ToString(), chat.ThreadId, userId, displayName,
            token.Token, token.ExpiresOn, chat.Status, state.CaseId, state.CaseReference);
    }

    private static string Describe(ChatSession chat, IReadOnlyList<ChatLine> transcript)
    {
        var account = chat.Customer.Account;
        var sb = new StringBuilder()
            .AppendLine($"Customer: {chat.Customer.FirstName} {chat.Customer.LastName}, account {account.AccountNumber}, {account.Plan} plan, {account.City}.")
            .AppendLine("Chat so far, oldest first:");
        foreach (var line in transcript)
        {
            sb.AppendLine(line.SenderId == chat.CustomerChatUserId
                ? $"<customer_message>{line.Text}</customer_message>"
                : $"[{line.SenderName}] {line.Text}");
        }
        return sb.ToString();
    }

    private static string FormatTranscript(IReadOnlyList<ChatLine> transcript)
    {
        var sb = new StringBuilder("Live chat transcript:\n");
        foreach (var line in transcript) sb.AppendLine($"{line.SenderName}: {line.Text}");
        var text = sb.ToString();
        return text.Length <= MaxTranscriptLength ? text : "…" + text[^MaxTranscriptLength..];
    }
}
