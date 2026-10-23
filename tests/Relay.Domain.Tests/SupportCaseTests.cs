using Relay.Domain;

namespace Relay.Domain.Tests;

public class SupportCaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Customer NewCustomer() =>
        new("Sarah", "Mitchell", "sarah@example.com", "204-555-0141",
            new ServiceAccount("HE-1", "1 Main St", "Winnipeg", "Residential Standard"));

    private static SupportCase NewCase(CasePriority priority = CasePriority.Normal) =>
        SupportCase.Open(NewCustomer(), "No power", "The power is out.", CaseCategory.Outage, priority, Channel.Portal, T0);

    private static Staff NewStaff() => new("Aiko Tanaka", "aiko@example.com", "Customer Care");

    [Fact]
    public void Open_records_description_as_first_customer_message()
    {
        var c = NewCase();

        Assert.Equal(CaseStatus.New, c.Status);
        var message = Assert.Single(c.Messages);
        Assert.Equal(AuthorType.Customer, message.AuthorType);
        Assert.Equal("The power is out.", message.Body);
    }

    [Theory]
    [InlineData(CasePriority.Urgent, 1, 8)]
    [InlineData(CasePriority.High, 4, 24)]
    [InlineData(CasePriority.Normal, 8, 72)]
    [InlineData(CasePriority.Low, 24, 120)]
    public void Open_sets_sla_deadlines_from_priority(CasePriority priority, int firstResponseHours, int resolutionHours)
    {
        var c = NewCase(priority);

        Assert.Equal(T0.AddHours(firstResponseHours), c.FirstResponseDueAt);
        Assert.Equal(T0.AddHours(resolutionHours), c.ResolutionDueAt);
    }

    [Fact]
    public void ChangePriority_recalculates_deadlines_from_creation_time()
    {
        var c = NewCase(CasePriority.Low);

        c.ChangePriority(CasePriority.Urgent, T0.AddHours(3));

        Assert.Equal(T0.AddHours(1), c.FirstResponseDueAt);
        Assert.True(c.IsFirstResponseBreached(T0.AddHours(3)));
    }

    [Fact]
    public void First_public_staff_reply_sets_first_response_but_internal_note_does_not()
    {
        var c = NewCase();

        c.AddMessage(AuthorType.Staff, "Aiko", "Checking with the crew.", isInternal: true, T0.AddMinutes(10));
        Assert.Null(c.FirstRespondedAt);

        c.AddMessage(AuthorType.Staff, "Aiko", "A crew is on the way.", isInternal: false, T0.AddMinutes(20));
        Assert.Equal(T0.AddMinutes(20), c.FirstRespondedAt);
    }

    [Fact]
    public void Ai_agent_public_reply_counts_as_first_response()
    {
        var c = NewCase();

        c.AddMessage(AuthorType.AiAgent, "Relay Assistant", "Here is what to do during an outage.", false, T0.AddMinutes(1));

        Assert.Equal(T0.AddMinutes(1), c.FirstRespondedAt);
    }

    [Fact]
    public void Customer_reply_reopens_case_that_was_waiting_on_customer()
    {
        var c = NewCase();
        c.ChangeStatus(CaseStatus.WaitingOnCustomer, T0.AddHours(1));

        c.AddMessage(AuthorType.Customer, "Sarah", "Here is the photo.", false, T0.AddHours(2));

        Assert.Equal(CaseStatus.InProgress, c.Status);
    }

    [Fact]
    public void Customer_reply_reopens_resolved_case_and_clears_resolved_time()
    {
        var c = NewCase();
        c.ChangeStatus(CaseStatus.Resolved, T0.AddHours(1));

        c.AddMessage(AuthorType.Customer, "Sarah", "It's out again.", false, T0.AddHours(2));

        Assert.Equal(CaseStatus.InProgress, c.Status);
        Assert.Null(c.ResolvedAt);
    }

    [Fact]
    public void Closed_case_rejects_messages_and_status_changes()
    {
        var c = NewCase();
        c.ChangeStatus(CaseStatus.Resolved, T0.AddHours(1));
        c.ChangeStatus(CaseStatus.Closed, T0.AddHours(2));

        Assert.Throws<DomainException>(() => c.AddMessage(AuthorType.Customer, "Sarah", "Hello?", false, T0.AddHours(3)));
        Assert.Throws<DomainException>(() => c.ChangeStatus(CaseStatus.InProgress, T0.AddHours(3)));
    }

    [Fact]
    public void Cannot_close_a_case_that_was_never_resolved()
    {
        var c = NewCase();

        Assert.Throws<DomainException>(() => c.ChangeStatus(CaseStatus.Closed, T0.AddHours(1)));
    }

    [Fact]
    public void Customers_cannot_post_internal_notes()
    {
        var c = NewCase();

        Assert.Throws<DomainException>(() => c.AddMessage(AuthorType.Customer, "Sarah", "secret", isInternal: true, T0));
    }

    [Fact]
    public void Assigning_a_new_case_moves_it_to_in_progress()
    {
        var c = NewCase();

        c.AssignTo(NewStaff(), T0.AddMinutes(5));

        Assert.Equal(CaseStatus.InProgress, c.Status);
        Assert.NotNull(c.AssignedStaffId);
    }

    [Fact]
    public void Resolution_breach_uses_resolved_time_once_resolved()
    {
        var c = NewCase(CasePriority.Urgent);
        c.ChangeStatus(CaseStatus.Resolved, T0.AddHours(2));

        Assert.False(c.IsResolutionBreached(T0.AddDays(5)));
    }
}
