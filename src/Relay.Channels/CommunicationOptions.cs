namespace Relay.Channels;

public sealed class CommunicationOptions
{
    public const string Section = "Communication";

    /// <summary>Azure Communication Services endpoint. Chat and email are off when empty.</summary>
    public string? Endpoint { get; set; }

    /// <summary>From address on the Azure-managed email domain, e.g. DoNotReply@xxxx.azurecomm.net.</summary>
    public string? SenderAddress { get; set; }

    /// <summary>
    /// Comma-separated addresses that may receive email. Demo customers use example.com addresses, and sending
    /// to them would bounce and hurt the sending domain's reputation, so everything else is skipped.
    /// </summary>
    public string? AllowedRecipients { get; set; }

    /// <summary>Public URL of the customer portal, used for links in emails.</summary>
    public string? PortalBaseUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

    public bool CanEmail(string address) =>
        !string.IsNullOrWhiteSpace(SenderAddress) &&
        (AllowedRecipients ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(address, StringComparer.OrdinalIgnoreCase);
}
