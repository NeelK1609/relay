namespace Relay.Domain;

public enum ChatStatus { WithAssistant, WaitingForAgent, WithAgent, Ended }

/// <summary>
/// A live chat between a customer and Highland Energy. The AI assistant handles it until it hands off,
/// at which point a case is opened and a staff member can join the same conversation.
/// </summary>
public sealed class ChatSession
{
    private ChatSession() { }

    public static ChatSession Start(Customer customer, string threadId, string customerChatUserId, string assistantChatUserId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        CustomerId = customer.Id,
        Customer = customer,
        ThreadId = threadId,
        CustomerChatUserId = customerChatUserId,
        AssistantChatUserId = assistantChatUserId,
        Status = ChatStatus.WithAssistant,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public string ThreadId { get; private set; } = "";
    public string CustomerChatUserId { get; private set; } = "";
    public string AssistantChatUserId { get; private set; } = "";
    public string? StaffChatUserId { get; private set; }
    public Guid? StaffId { get; private set; }
    public Staff? Staff { get; private set; }
    public Guid? CaseId { get; private set; }
    public ChatStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status != ChatStatus.Ended;

    public void HandOff(SupportCase supportCase, DateTimeOffset now)
    {
        if (Status != ChatStatus.WithAssistant) throw new DomainException("Only a chat with the assistant can be handed off.");
        if (supportCase.CustomerId != CustomerId) throw new DomainException("The case belongs to a different customer.");
        CaseId = supportCase.Id;
        Status = ChatStatus.WaitingForAgent;
        UpdatedAt = now;
    }

    public void Join(Staff staff, string staffChatUserId, DateTimeOffset now)
    {
        if (Status is ChatStatus.WithAssistant or ChatStatus.Ended)
        {
            throw new DomainException("Staff can join only after the assistant hands the chat off.");
        }
        Staff = staff;
        StaffId = staff.Id;
        StaffChatUserId = staffChatUserId;
        Status = ChatStatus.WithAgent;
        UpdatedAt = now;
    }

    public void End(DateTimeOffset now)
    {
        Status = ChatStatus.Ended;
        UpdatedAt = now;
    }
}
