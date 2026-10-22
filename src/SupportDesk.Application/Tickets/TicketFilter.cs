using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tickets;

public sealed record TicketFilter(TicketStatus? Status, TicketPriority? Priority, int Page, int PageSize);