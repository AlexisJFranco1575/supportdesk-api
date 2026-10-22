using SupportDesk.Api.Security;
using SupportDesk.Application.Auth;

namespace SupportDesk.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.Admin);

        group.MapPut("/{id:guid}/role", async (
                Guid id,
                ChangeRoleRequest request,
                IAuthService auth,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await auth.ChangeRoleAsync(id, request, cancellationToken)))
            .WithSummary("Change a user's role (administrators only)");

        return app;
    }
}