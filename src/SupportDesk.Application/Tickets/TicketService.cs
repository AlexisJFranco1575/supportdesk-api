using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;
using SupportDesk.Domain.Tickets;
using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Tickets;

public sealed class TicketService(
    ITicketRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ITicketService
{
    public const int MaxPageSize = 100;
    private const int MaxPage = 100_000;

    /// <summary>Agents and administrators work on every ticket; customers only own theirs.</summary>
    private bool IsStaff => currentUser.Role is UserRole.Agent or UserRole.Admin;

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        // The requester always comes from the authenticated identity, never from the request body.
        var ticket = Ticket.Create(
            request.Title,
            request.Description,
            request.Priority,
            RequireUserId(),
            timeProvider.GetUtcNow());

        await repository.AddAsync(ticket, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return TicketResponse.From(ticket);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await GetVisibleAsync(id, cancellationToken);
        return TicketResponse.From(ticket);
    }

    public async Task<PagedResult<TicketResponse>> ListAsync(
        TicketStatus? status,
        TicketPriority? priority,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // The restriction is part of the query itself, so other people's tickets never leave the database.
        var filter = new TicketFilter(
            status,
            priority,
            IsStaff ? null : RequireUserId(),
            Math.Clamp(page, 1, MaxPage),
            Math.Clamp(pageSize, 1, MaxPageSize));

        var result = await repository.ListAsync(filter, cancellationToken);

        return new PagedResult<TicketResponse>(
            result.Items.Select(TicketResponse.From).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public Task<TicketResponse> StartProgressAsync(
        Guid id,
        StartProgressRequest request,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, (ticket, now) => ticket.StartProgress(request.AssigneeId, now), cancellationToken);

    public Task<TicketResponse> ResolveAsync(Guid id, CancellationToken cancellationToken) =>
        UpdateAsync(id, (ticket, now) => ticket.Resolve(now), cancellationToken);

    public Task<TicketResponse> CloseAsync(Guid id, CancellationToken cancellationToken) =>
        UpdateAsync(id, (ticket, now) => ticket.Close(now), cancellationToken);

    public Task<TicketResponse> ReopenAsync(Guid id, CancellationToken cancellationToken) =>
        UpdateAsync(id, (ticket, now) => ticket.Reopen(now), cancellationToken);

    public Task<TicketResponse> ChangePriorityAsync(
        Guid id,
        ChangePriorityRequest request,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, (ticket, now) => ticket.ChangePriority(request.Priority, now), cancellationToken);

    private async Task<TicketResponse> UpdateAsync(
        Guid id,
        Action<Ticket, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        // Second layer of protection: the endpoint policy already restricts these actions to staff,
        // but the rule also lives here so it holds no matter who calls the service.
        if (!IsStaff)
        {
            throw new ForbiddenException("Only agents and administrators can work on tickets.");
        }

        var ticket = await GetOrThrowAsync(id, cancellationToken);

        // The domain enforces the workflow; if it throws, nothing is saved.
        change(ticket, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        return TicketResponse.From(ticket);
    }

    private async Task<Ticket> GetVisibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await GetOrThrowAsync(id, cancellationToken);

        // Answer 404 (not 403) for someone else's ticket, so its existence is not revealed.
        if (!IsStaff && ticket.RequesterId != RequireUserId())
        {
            throw new NotFoundException("Ticket", id);
        }

        return ticket;
    }

    private async Task<Ticket> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("Ticket", id);

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new InvalidOperationException("An authenticated user is required.");
}