using SupportDesk.Application.Common;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Application.Tickets;

public sealed class TicketService(ITicketRepository repository, TimeProvider timeProvider) : ITicketService
{
    public const int MaxPageSize = 100;
    private const int MaxPage = 100_000;

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = Ticket.Create(
            request.Title,
            request.Description,
            request.Priority,
            timeProvider.GetUtcNow());

        await repository.AddAsync(ticket, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return TicketResponse.From(ticket);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await GetOrThrowAsync(id, cancellationToken);
        return TicketResponse.From(ticket);
    }

    public async Task<PagedResult<TicketResponse>> ListAsync(
        TicketStatus? status,
        TicketPriority? priority,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var filter = new TicketFilter(
            status,
            priority,
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
        var ticket = await GetOrThrowAsync(id, cancellationToken);

        // The domain enforces the rules; if it throws, nothing is saved.
        change(ticket, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        return TicketResponse.From(ticket);
    }

    private async Task<Ticket> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("Ticket", id);
}