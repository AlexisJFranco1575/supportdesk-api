using SupportDesk.Application.Auth;
using SupportDesk.Domain.Users;

namespace SupportDesk.Api.Security;

/// <summary>Reads the identity from the claims of the already-validated JWT.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(accessor.HttpContext?.User.FindFirst("role")?.Value, out var role)
        && Enum.IsDefined(role)
            ? role
            : null;
}