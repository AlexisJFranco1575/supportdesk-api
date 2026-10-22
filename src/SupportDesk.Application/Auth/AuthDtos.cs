using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Auth;

public sealed record RegisterRequest(string? Email, string? FullName, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record ChangeRoleRequest(UserRole Role);

public sealed record UserResponse(Guid Id, string Email, string FullName, UserRole Role, DateTimeOffset CreatedAt)
{
    public static UserResponse From(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.CreatedAt);
}

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);