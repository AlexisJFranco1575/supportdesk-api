using SupportDesk.Api.Security;
using SupportDesk.Application.Tickets;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Api.Endpoints;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        // Any authenticated user can reach the group; agent-only actions add a stricter policy below.
        var group = app.MapGroup("/api/tickets")
            .WithTags("Tickets")
            .RequireAuthorization()
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
            .RequireAuthorization(Policies.Agent)
            .WithSummary("Assign the ticket and start working on it (agents)");

        group.MapPost("/{id:guid}/resolve", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ResolveAsync(id, cancellationToken)))
            .RequireAuthorization(Policies.Agent)
            .WithSummary("Mark an in-progress ticket as resolved (agents)");

        group.MapPost("/{id:guid}/close", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.CloseAsync(id, cancellationToken)))
            .RequireAuthorization(Policies.Agent)
            .WithSummary("Close a resolved ticket (agents)");

        group.MapPost("/{id:guid}/reopen", async (
                Guid id,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ReopenAsync(id, cancellationToken)))
            .RequireAuthorization(Policies.Agent)
            .WithSummary("Reopen a resolved or closed ticket (agents)");

        group.MapPatch("/{id:guid}/priority", async (
                Guid id,
                ChangePriorityRequest request,
                ITicketService service,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ChangePriorityAsync(id, request, cancellationToken)))
            .RequireAuthorization(Policies.Agent)
            .WithSummary("Change the priority of a ticket that is not closed (agents)");

        return app;
    }
}