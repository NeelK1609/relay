using Relay.Domain;

namespace Relay.Domain.Tests;

public class ChatSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Customer NewCustomer() =>
        new("Sarah", "Mitchell", "sarah@example.com", "204-555-0141", new ServiceAccount("HE-1", "1 Main St", "Winnipeg", "Residential Standard"));

    private static ChatSession NewChat(Customer customer) => ChatSession.Start(customer, "thread", "customer-user", "assistant-user", T0);

    private static SupportCase CaseFor(Customer customer) =>
        SupportCase.Open(customer, "Move service", "Chat transcript", CaseCategory.MoveService, CasePriority.Normal, Channel.Chat, T0);

    [Fact]
    public void Starts_with_the_assistant()
    {
        Assert.Equal(ChatStatus.WithAssistant, NewChat(NewCustomer()).Status);
    }

    [Fact]
    public void Hand_off_links_the_case_and_waits_for_an_agent()
    {
        var customer = NewCustomer();
        var chat = NewChat(customer);
        var supportCase = CaseFor(customer);

        chat.HandOff(supportCase, T0.AddMinutes(2));

        Assert.Equal(ChatStatus.WaitingForAgent, chat.Status);
        Assert.Equal(supportCase.Id, chat.CaseId);
    }

    [Fact]
    public void Cannot_hand_off_to_another_customers_case()
    {
        var chat = NewChat(NewCustomer());

        Assert.Throws<DomainException>(() => chat.HandOff(CaseFor(NewCustomer()), T0));
    }

    [Fact]
    public void Staff_can_join_only_after_hand_off()
    {
        var customer = NewCustomer();
        var chat = NewChat(customer);
        var staff = new Staff("Aiko Tanaka", "aiko@example.com", "Customer Care");

        Assert.Throws<DomainException>(() => chat.Join(staff, "staff-user", T0));

        chat.HandOff(CaseFor(customer), T0.AddMinutes(1));
        chat.Join(staff, "staff-user", T0.AddMinutes(3));

        Assert.Equal(ChatStatus.WithAgent, chat.Status);
        Assert.Equal(staff.Id, chat.StaffId);
    }

    [Fact]
    public void Ended_chat_cannot_be_joined_or_handed_off()
    {
        var customer = NewCustomer();
        var chat = NewChat(customer);
        chat.End(T0.AddMinutes(5));

        Assert.False(chat.IsActive);
        Assert.Throws<DomainException>(() => chat.HandOff(CaseFor(customer), T0.AddMinutes(6)));
        Assert.Throws<DomainException>(() => chat.Join(new Staff("A", "a@example.com", "Care"), "u", T0.AddMinutes(6)));
    }
}
