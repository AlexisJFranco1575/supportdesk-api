using SupportDesk.Domain;
using SupportDesk.Domain.Tickets;
using Xunit;

namespace SupportDesk.Domain.Tests.Tickets;

public class TicketTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Agent = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Requester = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static Ticket NewTicket() =>
        Ticket.Create("Printer is down", "The 3rd floor printer does not respond.", TicketPriority.High, Requester, Now);

    private static Ticket InProgressTicket()
    {
        var ticket = NewTicket();
        ticket.StartProgress(Agent, Now.AddMinutes(5));
        return ticket;
    }

    private static Ticket ResolvedTicket()
    {
        var ticket = InProgressTicket();
        ticket.Resolve(Now.AddHours(1));
        return ticket;
    }

    [Fact]
    public void Create_sets_the_initial_state()
    {
        var ticket = NewTicket();

        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Equal("Printer is down", ticket.Title);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(Requester, ticket.RequesterId);
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ResolvedAt);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.Equal(Now, ticket.UpdatedAt);
    }

    [Fact]
    public void Create_trims_title_and_description()
    {
        var ticket = Ticket.Create("  Title  ", "  Body  ", TicketPriority.Low, Requester, Now);

        Assert.Equal("Title", ticket.Title);
        Assert.Equal("Body", ticket.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_missing_title(string? title)
    {
        Assert.Throws<DomainException>(() =>
            Ticket.Create(title, "Some description", TicketPriority.Low, Requester, Now));
    }

    [Fact]
    public void Create_rejects_a_title_that_is_too_long()
    {
        var title = new string('a', Ticket.TitleMaxLength + 1);

        Assert.Throws<DomainException>(() =>
            Ticket.Create(title, "Some description", TicketPriority.Low, Requester, Now));
    }

    [Fact]
    public void Create_rejects_a_missing_description()
    {
        Assert.Throws<DomainException>(() =>
            Ticket.Create("A title", "  ", TicketPriority.Low, Requester, Now));
    }

    [Fact]
    public void Create_rejects_an_unknown_priority()
    {
        Assert.Throws<DomainException>(() =>
            Ticket.Create("A title", "A description", (TicketPriority)99, Requester, Now));
    }

    [Fact]
    public void Create_rejects_an_empty_requester()
    {
        Assert.Throws<DomainException>(() =>
            Ticket.Create("A title", "A description", TicketPriority.Low, Guid.Empty, Now));
    }

    [Fact]
    public void StartProgress_requires_an_assignee()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainException>(() => ticket.StartProgress(Guid.Empty, Now));
    }

    [Fact]
    public void A_ticket_follows_the_full_workflow()
    {
        var ticket = NewTicket();

        ticket.StartProgress(Agent, Now.AddMinutes(5));
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(Agent, ticket.AssigneeId);

        var resolvedAt = Now.AddHours(1);
        ticket.Resolve(resolvedAt);
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(resolvedAt, ticket.ResolvedAt);

        ticket.Close(Now.AddHours(2));
        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(Now.AddHours(2), ticket.UpdatedAt);
    }

    [Fact]
    public void Resolve_is_only_allowed_while_in_progress()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainException>(() => ticket.Resolve(Now));
    }

    [Fact]
    public void Close_is_only_allowed_once_resolved()
    {
        var ticket = InProgressTicket();

        Assert.Throws<DomainException>(() => ticket.Close(Now));
    }

    [Fact]
    public void A_ticket_cannot_start_progress_twice()
    {
        var ticket = InProgressTicket();

        Assert.Throws<DomainException>(() => ticket.StartProgress(Agent, Now));
    }

    [Fact]
    public void Reopen_clears_the_assignee_and_the_resolution_date()
    {
        var ticket = ResolvedTicket();

        ticket.Reopen(Now.AddDays(1));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ResolvedAt);
        Assert.Equal(Requester, ticket.RequesterId);
    }

    [Fact]
    public void Reopen_is_not_allowed_on_an_open_ticket()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainException>(() => ticket.Reopen(Now));
    }

    [Fact]
    public void Priority_can_be_changed_until_the_ticket_is_closed()
    {
        var ticket = ResolvedTicket();
        ticket.ChangePriority(TicketPriority.Critical, Now.AddHours(3));
        Assert.Equal(TicketPriority.Critical, ticket.Priority);

        ticket.Close(Now.AddHours(4));

        Assert.Throws<DomainException>(() =>
            ticket.ChangePriority(TicketPriority.Low, Now.AddHours(5)));
    }
}