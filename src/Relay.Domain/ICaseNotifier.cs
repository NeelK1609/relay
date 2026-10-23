namespace Relay.Domain;

public enum NotificationKind { CaseOpened, NewReply }

public sealed record CaseNotification(Guid CaseId, NotificationKind Kind);

/// <summary>Tells the customer about their case outside the portal (email today).</summary>
public interface ICaseNotifier
{
    ValueTask NotifyAsync(CaseNotification notification, CancellationToken ct = default);
}
