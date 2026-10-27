using System.Security.Claims;
using SupportDesk.Api.Security;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;

namespace SupportDesk.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.Admin);

        group.MapGet("/", async (
                IAuthService auth,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
                TypedResults.Ok(await auth.ListUsersAsync(page, pageSize, cancellationToken)))
            .WithSummary("List user accounts, oldest first (administrators only)");

        group.MapPut("/{id:guid}/role", async (
                Guid id,
                ChangeRoleRequest request,
                ClaimsPrincipal caller,
                IAuthService auth,
                CancellationToken cancellationToken) =>
            {
                // An administrator cannot change their own role, so a mistaken click cannot
                // leave the system without anyone able to manage users.
                if (Guid.TryParse(caller.FindFirst("sub")?.Value, out var callerId) && callerId == id)
                {
                    throw new ForbiddenException("You cannot change your own role.");
                }

                return TypedResults.Ok(await auth.ChangeRoleAsync(id, request, cancellationToken));
            })
            .WithSummary("Change a user's role (administrators only)");

        return app;
    }
}
