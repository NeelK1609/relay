using System.Collections.Concurrent;
using Azure.Communication;
using Azure.Communication.Chat;
using Azure.Communication.Identity;
using Azure.Core;

namespace Relay.Channels;

public sealed record ChatLine(string SenderId, string SenderName, string Text, DateTimeOffset At);

/// <summary>
/// Thin wrapper over Azure Communication Services identities and chat threads.
/// Every participant (customer, assistant, staff) gets its own chat identity per conversation.
/// </summary>
public sealed class ChatGateway(CommunicationIdentityClient identities, Uri endpoint)
{
    private readonly ConcurrentDictionary<string, AccessToken> _tokens = new();

    public Uri Endpoint => endpoint;

    public async Task<string> CreateUserAsync(CancellationToken ct) =>
        (await identities.CreateUserAsync(ct)).Value.Id;

    /// <summary>A chat-scoped token for the identity, reused until shortly before it expires.</summary>
    public async Task<AccessToken> GetTokenAsync(string userId, CancellationToken ct)
    {
        if (_tokens.TryGetValue(userId, out var cached) && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(10)) return cached;
        var token = (await identities.GetTokenAsync(new CommunicationUserIdentifier(userId), [CommunicationTokenScope.Chat], ct)).Value;
        _tokens[userId] = token;
        return token;
    }

    public async Task<string> CreateThreadAsync(string topic, (string Id, string Name) owner, (string Id, string Name) member, CancellationToken ct)
    {
        var client = await ClientForAsync(owner.Id, ct);
        var result = await client.CreateChatThreadAsync(topic,
            [Participant(owner.Id, owner.Name), Participant(member.Id, member.Name)], cancellationToken: ct);
        return result.Value.ChatThread.Id;
    }

    public async Task SendAsync(string threadId, string fromUserId, string displayName, string text, CancellationToken ct)
    {
        var thread = (await ClientForAsync(fromUserId, ct)).GetChatThreadClient(threadId);
        await thread.SendMessageAsync(new SendChatMessageOptions
        {
            Content = text,
            MessageType = ChatMessageType.Text,
            SenderDisplayName = displayName,
        }, ct);
    }

    public async Task AddParticipantAsync(string threadId, string byUserId, string userId, string displayName, CancellationToken ct)
    {
        var thread = (await ClientForAsync(byUserId, ct)).GetChatThreadClient(threadId);
        await thread.AddParticipantAsync(Participant(userId, displayName), ct);
    }

    /// <summary>Text messages in the thread, oldest first.</summary>
    public async Task<IReadOnlyList<ChatLine>> GetTranscriptAsync(string threadId, string asUserId, CancellationToken ct)
    {
        var thread = (await ClientForAsync(asUserId, ct)).GetChatThreadClient(threadId);
        var lines = new List<ChatLine>();
        await foreach (var m in thread.GetMessagesAsync(cancellationToken: ct))
        {
            if (m.Type == ChatMessageType.Text && m.Content?.Message is { Length: > 0 } text)
            {
                lines.Add(new ChatLine(m.Sender is CommunicationUserIdentifier u ? u.Id : "", m.SenderDisplayName ?? "", text, m.CreatedOn));
            }
        }
        lines.Reverse();
        return lines;
    }

    private async Task<ChatClient> ClientForAsync(string userId, CancellationToken ct) =>
        new(endpoint, new CommunicationTokenCredential((await GetTokenAsync(userId, ct)).Token));

    private static ChatParticipant Participant(string id, string name) =>
        new(new CommunicationUserIdentifier(id)) { DisplayName = name };
}
