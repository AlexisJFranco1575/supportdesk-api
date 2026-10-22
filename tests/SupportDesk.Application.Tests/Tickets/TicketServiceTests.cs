#pragma warning disable xUnit1051

using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets;
using SupportDesk.Domain;
using SupportDesk.Domain.Tickets;
using SupportDesk.Domain.Users;
using Xunit;

namespace SupportDesk.Application.Tests.Tickets;

public class TicketServiceTests
{
    private static readonly Guid Agent = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid CustomerA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CustomerB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private readonly FakeTicketRepository _repository = new();
    private readonly FakeCurrentUser _user = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        // Most tests act as an agent; ownership tests switch identity with BecomeCustomer / BecomeAgent.
        _user.Become(Agent, UserRole.Agent);
        _service = new TicketService(_repository, _user, _time);
    }

    private void BecomeCustomer(Guid id) => _user.Become(id, UserRole.Customer);

    private void BecomeAgent() => _user.Become(Agent, UserRole.Agent);

    private Task<TicketResponse> CreateAsync(
        string title = "Printer is down",
        TicketPriority priority = TicketPriority.Medium) =>
        _service.CreateAsync(
            new CreateTicketRequest(title, "Something is broken.", priority),
            CancellationToken.None);

    [Fact]
    public async Task Create_persists_the_ticket_and_returns_it()
    {
        var created = await CreateAsync(priority: TicketPriority.High);

        Assert.Equal(TicketStatus.Open, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(Agent, created.RequesterId);
        Assert.Equal(1, _repository.SaveCount);

        var loaded = await _service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(created.Id, loaded.Id);
    }

    [Fact]
    public async Task Get_throws_NotFound_for_an_unknown_ticket()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task A_ticket_moves_through_the_whole_workflow()
    {
        var created = await CreateAsync();

        var started = await _service.StartProgressAsync(
            created.Id, new StartProgressRequest(Agent), CancellationToken.None);
        Assert.Equal(TicketStatus.InProgress, started.Status);
        Assert.Equal(Agent, started.AssigneeId);

        var resolved = await _service.ResolveAsync(created.Id, CancellationToken.None);
        Assert.Equal(TicketStatus.Resolved, resolved.Status);

        var closed = await _service.CloseAsync(created.Id, CancellationToken.None);
        Assert.Equal(TicketStatus.Closed, closed.Status);

        Assert.Equal(4, _repository.SaveCount);
    }

    [Fact]
    public async Task An_invalid_transition_throws_and_saves_nothing()
    {
        var created = await CreateAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.ResolveAsync(created.Id, CancellationToken.None));

        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var first = await CreateAsync("First");
        await CreateAsync("Second");
        await _service.StartProgressAsync(first.Id, new StartProgressRequest(Agent), CancellationToken.None);

        var result = await _service.ListAsync(TicketStatus.InProgress, null, 1, 20, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(first.Id, result.Items[0].Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_paginates_newest_first()
    {
        await CreateAsync("Oldest");
        _time.Advance(TimeSpan.FromMinutes(1));
        await CreateAsync("Middle");
        _time.Advance(TimeSpan.FromMinutes(1));
        await CreateAsync("Newest");

        var page1 = await _service.ListAsync(null, null, 1, 2, CancellationToken.None);
        var page2 = await _service.ListAsync(null, null, 2, 2, CancellationToken.None);

        Assert.Equal(new[] { "Newest", "Middle" }, page1.Items.Select(t => t.Title).ToArray());
        Assert.Equal(new[] { "Oldest" }, page2.Items.Select(t => t.Title).ToArray());
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task List_clamps_out_of_range_paging_values()
    {
        await CreateAsync();

        var result = await _service.ListAsync(null, null, 0, 1000, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(TicketService.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task Customers_only_see_their_own_tickets()
    {
        BecomeCustomer(CustomerA);
        await CreateAsync("A1");
        await CreateAsync("A2");

        BecomeCustomer(CustomerB);
        await CreateAsync("B1");

        var seenByB = await _service.ListAsync(null, null, 1, 20, CancellationToken.None);
        Assert.Single(seenByB.Items);
        Assert.Equal("B1", seenByB.Items[0].Title);
        Assert.Equal(1, seenByB.TotalCount);

        BecomeCustomer(CustomerA);
        var seenByA = await _service.ListAsync(null, null, 1, 20, CancellationToken.None);
        Assert.Equal(2, seenByA.TotalCount);

        BecomeAgent();
        var seenByAgent = await _service.ListAsync(null, null, 1, 20, CancellationToken.None);
        Assert.Equal(3, seenByAgent.TotalCount);
    }

    [Fact]
    public async Task A_customer_gets_NotFound_for_someone_elses_ticket()
    {
        BecomeCustomer(CustomerA);
        var ticket = await CreateAsync();

        BecomeCustomer(CustomerB);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(ticket.Id, CancellationToken.None));

        BecomeCustomer(CustomerA);
        var own = await _service.GetAsync(ticket.Id, CancellationToken.None);
        Assert.Equal(ticket.Id, own.Id);
    }

    [Fact]
    public async Task Customers_cannot_work_on_tickets_even_their_own()
    {
        BecomeCustomer(CustomerA);
        var ticket = await CreateAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.StartProgressAsync(ticket.Id, new StartProgressRequest(Agent), CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ChangePriorityAsync(ticket.Id, new ChangePriorityRequest(TicketPriority.High), CancellationToken.None));
    }

    [Fact]
    public async Task Agents_can_read_any_customers_ticket()
    {
        BecomeCustomer(CustomerA);
        var ticket = await CreateAsync();

        BecomeAgent();
        var seen = await _service.GetAsync(ticket.Id, CancellationToken.None);

        Assert.Equal(ticket.Id, seen.Id);
        Assert.Equal(CustomerA, seen.RequesterId);
    }
}