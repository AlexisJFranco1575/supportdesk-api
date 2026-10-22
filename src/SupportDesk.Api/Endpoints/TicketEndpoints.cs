using SupportDesk.Application.Tickets;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets")
            .WithTags("Tickets")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/", async (
                CreateTicketRequest request,
                ITicketService service,
                CancellationToken cancellationToken) =>
            {
                var ticket = await service.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/tickets/{ticket.Id}", ticket);
            })
            .WithSummary("Create a ticket");

        group.MapGet("/", async (
                ITicketService service,
                CancellationToken cancellationToken,
                TicketStatus? status = null,
                TicketPriority? priority = null,
                int page = 1,
                int pageSize = 20) =>
                TypedResults.Ok(await service.ListAsync(status, priority, page, pageSize, cancellationToken)))
            .WithSummary("List tickets (filterable and paginated)");

        group.MapGet("/{id:guid}", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetAsync(id, cancellationToken)))
            .WithSummary("Get a ticket by id");

        group.MapPost("/{id:guid}/start", async (
                Guid id,
                StartProgressRequest request,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.StartProgressAsync(id, request, cancellationToken)))
            .WithSummary("Assign the ticket and start working on it");

        group.MapPost("/{id:guid}/resolve", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ResolveAsync(id, cancellationToken)))
            .WithSummary("Mark an in-progress ticket as resolved");

        group.MapPost("/{id:guid}/close", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.CloseAsync(id, cancellationToken)))
            .WithSummary("Close a resolved ticket");

        group.MapPost("/{id:guid}/reopen", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ReopenAsync(id, cancellationToken)))
            .WithSummary("Reopen a resolved or closed ticket");

        group.MapPatch("/{id:guid}/priority", async (
                Guid id,
                ChangePriorityRequest request,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ChangePriorityAsync(id, request, cancellationToken)))
            .WithSummary("Change the priority of a ticket that is not closed");

        return app;
    }
}