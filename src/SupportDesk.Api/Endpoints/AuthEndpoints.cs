using SupportDesk.Application.Auth;

namespace SupportDesk.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
                RegisterRequest request,
                IAuthService auth,
                CancellationToken cancellationToken) =>
            {
                var user = await auth.RegisterAsync(request, cancellationToken);
                return TypedResults.Json(user, statusCode: StatusCodes.Status201Created);
            })
            .AllowAnonymous()
            .WithSummary("Create a customer account");

        group.MapPost("/login", async (
                LoginRequest request,
                IAuthService auth,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await auth.LoginAsync(request, cancellationToken)))
            .AllowAnonymous()
            .WithSummary("Exchange credentials for a JWT access token");

        group.MapGet("/me", (HttpContext context) =>
            {
                var principal = context.User;

                return TypedResults.Ok(new
                {
                    id = principal.FindFirst("sub")?.Value,
                    email = principal.FindFirst("email")?.Value,
                    name = principal.FindFirst("name")?.Value,
                    role = principal.FindFirst("role")?.Value
                });
            })
            .RequireAuthorization()
            .WithSummary("Show the identity carried by the current token");

        return app;
    }
}