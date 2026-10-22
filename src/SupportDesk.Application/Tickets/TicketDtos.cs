using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tickets;

public sealed record CreateTicketRequest(
    string? Title,
    string? Description,
    TicketPriority Priority = TicketPriority.Medium);

public sealed record StartProgressRequest(Guid AssigneeId);

public sealed record ChangePriorityRequest(TicketPriority Priority);

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    Guid? AssigneeId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt)
{
    public static TicketResponse From(Ticket ticket) => new(
        ticket.Id,
        ticket.Title,
        ticket.Description,
        ticket.Status,
        ticket.Priority,
        ticket.AssigneeId,
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.ResolvedAt);
}