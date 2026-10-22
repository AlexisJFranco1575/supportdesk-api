using SupportDesk.Application.Common;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tickets;

/// <summary>
/// Persistence port. The Application layer defines it, the Infrastructure layer implements it.
/// </summary>
public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);

    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Ticket>> ListAsync(TicketFilter filter, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}