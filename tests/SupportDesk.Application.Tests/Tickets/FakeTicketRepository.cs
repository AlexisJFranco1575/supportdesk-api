using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tests.Tickets;

/// <summary>In-memory repository used to test the service without a database.</summary>
internal sealed class FakeTicketRepository : ITicketRepository
{
    private readonly List<Ticket> _tickets = [];

    public int SaveCount { get; private set; }

    public Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        _tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_tickets.FirstOrDefault(t => t.Id == id));

    public Task<PagedResult<Ticket>> ListAsync(TicketFilter filter, CancellationToken cancellationToken)
    {
        var query = _tickets.AsEnumerable();

        if (filter.RequesterId is { } requesterId)
        {
            query = query.Where(t => t.RequesterId == requesterId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        var all = query.OrderByDescending(t => t.CreatedAt).ToList();
        var items = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();

        return Task.FromResult(new PagedResult<Ticket>(items, filter.Page, filter.PageSize, all.Count));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}