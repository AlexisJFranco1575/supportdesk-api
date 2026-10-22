#pragma warning disable xUnit1051

using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;
using SupportDesk.Application.Tests.Tickets;
using SupportDesk.Domain.Users;
using Xunit;

namespace SupportDesk.Application.Tests.Auth;

public class AuthServiceTests
{
    private const string StrongPassword = "Str0ng_Passw0rd!";

    private readonly FakeUserRepository _users = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(
            _users,
            new FakePasswordHasher(),
            new FakeTokenService(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero)));
    }

    private Task<UserResponse> RegisterAsync(string email = "ana@example.com") =>
        _service.RegisterAsync(new RegisterRequest(email, "Ana Perez", StrongPassword), CancellationToken.None);

    [Fact]
    public async Task Register_creates_a_customer_with_a_normalized_email_and_a_hashed_password()
    {
        var response = await RegisterAsync("  Ana@Example.COM ");

        Assert.Equal("ana@example.com", response.Email);
        Assert.Equal(UserRole.Customer, response.Role);

        var stored = await _users.GetByEmailAsync("ana@example.com", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.NotEqual(StrongPassword, stored.PasswordHash);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_regardless_of_case()
    {
        await RegisterAsync("ana@example.com");

        await Assert.ThrowsAsync<ConflictException>(() => RegisterAsync("ANA@example.com"));
    }

    [Fact]
    public async Task Register_reports_every_invalid_field()
    {
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.RegisterAsync(new RegisterRequest("nope", "", "short"), CancellationToken.None));

        Assert.Contains("email", exception.Errors.Keys);
        Assert.Contains("fullName", exception.Errors.Keys);
        Assert.Contains("password", exception.Errors.Keys);
    }

    [Fact]
    public async Task Login_returns_a_bearer_token_for_valid_credentials()
    {
        await RegisterAsync();

        var response = await _service.LoginAsync(
            new LoginRequest("ANA@example.com", StrongPassword), CancellationToken.None);

        Assert.Equal("token-for-ana@example.com", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password()
    {
        await RegisterAsync();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest("ana@example.com", "wrong-password"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_rejects_an_unknown_email_with_the_same_error()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest("ghost@example.com", StrongPassword), CancellationToken.None));
    }

    [Fact]
    public async Task ChangeRole_promotes_a_user()
    {
        var user = await RegisterAsync();

        var updated = await _service.ChangeRoleAsync(
            user.Id, new ChangeRoleRequest(UserRole.Agent), CancellationToken.None);

        Assert.Equal(UserRole.Agent, updated.Role);
    }

    [Fact]
    public async Task EnsureAdmin_creates_the_admin_once()
    {
        await _service.EnsureAdminAsync("admin@example.com", StrongPassword, "Administrator", CancellationToken.None);
        await _service.EnsureAdminAsync("admin@example.com", StrongPassword, "Administrator", CancellationToken.None);

        var admin = Assert.Single(_users.All);
        Assert.Equal(UserRole.Admin, admin.Role);
    }
}