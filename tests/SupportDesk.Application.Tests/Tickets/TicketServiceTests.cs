#pragma warning disable xUnit1051

using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets;
using SupportDesk.Domain;
using SupportDesk.Domain.Tickets;
using Xunit;

namespace SupportDesk.Application.Tests.Tickets;

public class TicketServiceTests
{
    private static readonly Guid Agent = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private readonly FakeTicketRepository _repository = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _service = new TicketService(_repository, _time);
    }

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
}