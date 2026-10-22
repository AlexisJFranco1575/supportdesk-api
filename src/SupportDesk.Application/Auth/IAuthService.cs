namespace SupportDesk.Application.Auth;

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserResponse> ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Creates the first administrator if it does not exist yet. Safe to call on every startup.</summary>
    Task EnsureAdminAsync(string email, string password, string fullName, CancellationToken cancellationToken);
}