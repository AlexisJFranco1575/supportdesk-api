using SupportDesk.Application.Common;
using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Auth;

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider) : IAuthService
{
    public const int MaxPageSize = 100;
    private const int MaxPage = 100_000;

    public Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        CreateUserAsync(request, UserRole.Customer, cancellationToken);

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = AuthValidator.NormalizeEmail(request.Email);
        var user = email.Length == 0 ? null : await users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            // Do the same amount of hashing work as a real login, so the response time
            // does not reveal whether the account exists.
            passwordHasher.Hash(request.Password ?? string.Empty);
            throw new InvalidCredentialsException();
        }

        if (string.IsNullOrEmpty(request.Password) || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var token = tokenService.CreateAccessToken(user);

        return new LoginResponse(token.Value, "Bearer", token.ExpiresAt);
    }

    public async Task<UserResponse> ChangeRoleAsync(
        Guid userId,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        user.ChangeRole(request.Role);
        await users.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }

    public async Task<PagedResult<UserResponse>> ListUsersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await users.ListAsync(
            Math.Clamp(page, 1, MaxPage),
            Math.Clamp(pageSize, 1, MaxPageSize),
            cancellationToken);

        return new PagedResult<UserResponse>(
            result.Items.Select(UserResponse.From).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task EnsureAdminAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = AuthValidator.NormalizeEmail(email);

        if (await users.GetByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            return;
        }

        try
        {
            await CreateUserAsync(new RegisterRequest(email, fullName, password), UserRole.Admin, cancellationToken);
        }
        catch (ConflictException)
        {
            // Another instance created the administrator between our check and our insert.
            // That is exactly the outcome we wanted, so there is nothing left to do.
        }
    }

    private async Task<UserResponse> CreateUserAsync(
        RegisterRequest request,
        UserRole role,
        CancellationToken cancellationToken)
    {
        AuthValidator.ValidateRegistration(request);

        var email = AuthValidator.NormalizeEmail(request.Email);

        if (await users.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.Create(
            email,
            request.FullName,
            passwordHasher.Hash(request.Password!),
            role,
            timeProvider.GetUtcNow());

        await users.AddAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}
