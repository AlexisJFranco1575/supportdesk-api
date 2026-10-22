using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tickets;

/// <param name="RequesterId">When set, only tickets opened by this user are returned.</param>
public sealed record TicketFilter(
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? RequesterId,
    int Page,
    int PageSize);