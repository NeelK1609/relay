namespace Relay.Domain;

public sealed class SupportCase
{
    private static readonly Dictionary<CaseStatus, CaseStatus[]> AllowedTransitions = new()
    {
        [CaseStatus.New] = [CaseStatus.InProgress, CaseStatus.WaitingOnCustomer, CaseStatus.Resolved],
        [CaseStatus.InProgress] = [CaseStatus.WaitingOnCustomer, CaseStatus.Resolved],
        [CaseStatus.WaitingOnCustomer] = [CaseStatus.InProgress, CaseStatus.Resolved],
        [CaseStatus.Resolved] = [CaseStatus.InProgress, CaseStatus.Closed],
        [CaseStatus.Closed] = [],
    };

    private readonly List<CaseMessage> _messages = [];

    private SupportCase() { }

    public static SupportCase Open(
        Customer customer, string title, string description, CaseCategory category,
        CasePriority priority, Channel channel, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("A case needs a title.");
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("A case needs a description.");

        var c = new SupportCase
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Category = category,
            Channel = channel,
            Status = CaseStatus.New,
            Customer = customer,
            CustomerId = customer.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        c.ApplyPriority(priority);
        c._messages.Add(new CaseMessage(c.Id, AuthorType.Customer, customer.FullName, description.Trim(), false, now));
        return c;
    }

    public Guid Id { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = "";
    public CaseCategory Category { get; private set; }
    public CasePriority Priority { get; private set; }
    public CaseStatus Status { get; private set; }
    public Channel Channel { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public Guid? AssignedStaffId { get; private set; }
    public Staff? AssignedTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset FirstResponseDueAt { get; private set; }
    public DateTimeOffset ResolutionDueAt { get; private set; }
    public DateTimeOffset? FirstRespondedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public IReadOnlyList<CaseMessage> Messages => _messages;

    public string Reference => $"CAS-{Number:D6}";
    public bool IsOpen => Status is not (CaseStatus.Resolved or CaseStatus.Closed);

    public bool IsFirstResponseBreached(DateTimeOffset now) =>
        (FirstRespondedAt ?? now) > FirstResponseDueAt;

    public bool IsResolutionBreached(DateTimeOffset now) =>
        (ResolvedAt ?? now) > ResolutionDueAt;

    public CaseMessage AddMessage(AuthorType author, string authorName, string body, bool isInternal, DateTimeOffset now)
    {
        if (Status == CaseStatus.Closed) throw new DomainException("Closed cases cannot receive new messages.");
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("A message cannot be empty.");
        if (isInternal && author == AuthorType.Customer) throw new DomainException("Customers cannot post internal notes.");

        var message = new CaseMessage(Id, author, authorName, body.Trim(), isInternal, now);
        _messages.Add(message);

        var isPublicReplyFromSupport = !isInternal && author is AuthorType.Staff or AuthorType.AiAgent;
        if (isPublicReplyFromSupport && FirstRespondedAt is null) FirstRespondedAt = now;
        if (author == AuthorType.Customer && Status is CaseStatus.WaitingOnCustomer or CaseStatus.Resolved)
        {
            TransitionTo(CaseStatus.InProgress, now);
        }

        UpdatedAt = now;
        return message;
    }

    public void ChangeStatus(CaseStatus next, DateTimeOffset now)
    {
        if (next == Status) return;
        if (!AllowedTransitions[Status].Contains(next))
        {
            throw new DomainException($"A case cannot move from {Status} to {next}.");
        }
        TransitionTo(next, now);
    }

    public void ChangePriority(CasePriority priority, DateTimeOffset now)
    {
        if (priority == Priority) return;
        if (!IsOpen) throw new DomainException("Only open cases can be reprioritized.");
        ApplyPriority(priority);
        UpdatedAt = now;
    }

    public void ChangeCategory(CaseCategory category, DateTimeOffset now)
    {
        Category = category;
        UpdatedAt = now;
    }

    public void AssignTo(Staff? staff, DateTimeOffset now)
    {
        AssignedTo = staff;
        AssignedStaffId = staff?.Id;
        if (staff is not null && Status == CaseStatus.New) TransitionTo(CaseStatus.InProgress, now);
        UpdatedAt = now;
    }

    private void TransitionTo(CaseStatus next, DateTimeOffset now)
    {
        Status = next;
        ResolvedAt = next is CaseStatus.Resolved or CaseStatus.Closed ? ResolvedAt ?? now : null;
        UpdatedAt = now;
    }

    private void ApplyPriority(CasePriority priority)
    {
        Priority = priority;
        var (firstResponse, resolution) = SlaPolicy.For(priority);
        FirstResponseDueAt = CreatedAt + firstResponse;
        ResolutionDueAt = CreatedAt + resolution;
    }
}

public sealed class CaseMessage
{
    private CaseMessage() { }

    internal CaseMessage(Guid caseId, AuthorType authorType, string authorName, string body, bool isInternal, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CaseId = caseId;
        AuthorType = authorType;
        AuthorName = authorName;
        Body = body;
        IsInternal = isInternal;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public AuthorType AuthorType { get; private set; }
    public string AuthorName { get; private set; } = "";
    public string Body { get; private set; } = "";
    public bool IsInternal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class DomainException(string message) : Exception(message);
