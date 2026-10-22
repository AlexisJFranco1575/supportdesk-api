using SupportDesk.Application.Auth;
using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Tests.Auth;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public IReadOnlyList<User> All => _users;

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Deterministic stand-in for the real hasher; it is NOT secure and only used in tests.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string passwordHash, string password) => passwordHash == $"hashed::{password}";
}

internal sealed class FakeTokenService : ITokenService
{
    public AccessToken CreateAccessToken(User user) =>
        new($"token-for-{user.Email}", DateTimeOffset.UnixEpoch.AddHours(1));
}