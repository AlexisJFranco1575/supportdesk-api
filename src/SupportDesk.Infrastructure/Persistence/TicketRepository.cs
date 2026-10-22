using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Infrastructure.Persistence;

public sealed class TicketRepository(SupportDeskDbContext db) : ITicketRepository
{
    public Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        db.Tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<PagedResult<Ticket>> ListAsync(TicketFilter filter, CancellationToken cancellationToken)
    {
        IQueryable<Ticket> query = db.Tickets.AsNoTracking();

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

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Ticket>(items, filter.Page, filter.PageSize, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}