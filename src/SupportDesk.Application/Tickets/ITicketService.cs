using SupportDesk.Application.Common;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tickets;

public interface ITicketService
{
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken);

    Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<TicketResponse>> ListAsync(
        TicketStatus? status,
        TicketPriority? priority,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<TicketResponse> StartProgressAsync(Guid id, StartProgressRequest request, CancellationToken cancellationToken);

    Task<TicketResponse> ResolveAsync(Guid id, CancellationToken cancellationToken);

    Task<TicketResponse> CloseAsync(Guid id, CancellationToken cancellationToken);

    Task<TicketResponse> ReopenAsync(Guid id, CancellationToken cancellationToken);

    Task<TicketResponse> ChangePriorityAsync(Guid id, ChangePriorityRequest request, CancellationToken cancellationToken);
}