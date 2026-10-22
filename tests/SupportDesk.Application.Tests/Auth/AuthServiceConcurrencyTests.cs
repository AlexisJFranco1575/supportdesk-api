#pragma warning disable xUnit1051

using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;
using SupportDesk.Application.Tests.Tickets;
using SupportDesk.Domain.Users;
using Xunit;

namespace SupportDesk.Application.Tests.Auth;

/// <summary>
/// Simulates the race where another request inserts the same email between our "does it exist?"
/// check and our insert: the lookup finds nothing, but saving hits the unique index.
/// </summary>
public class AuthServiceConcurrencyTests
{
    private const string StrongPassword = "Str0ng_Passw0rd!";

    private readonly AuthService _service = new(
        new RacingUserRepository(),
        new FakePasswordHasher(),
        new FakeTokenService(),
        new FixedTimeProvider(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task EnsureAdmin_ignores_an_administrator_created_by_another_instance()
    {
        // Must not throw: the admin exists, which is the goal.
        await _service.EnsureAdminAsync("admin@example.com", StrongPassword, "Administrator", CancellationToken.None);
    }

    [Fact]
    public async Task Register_reports_a_concurrent_duplicate_as_a_conflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RegisterAsync(
                new RegisterRequest("ana@example.com", "Ana Perez", StrongPassword),
                CancellationToken.None));
    }

    private sealed class RacingUserRepository : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        // The check says "no such user"...
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        // ...but by the time we save, somebody else won the race.
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new ConflictException("An account with this email already exists.");
    }
}