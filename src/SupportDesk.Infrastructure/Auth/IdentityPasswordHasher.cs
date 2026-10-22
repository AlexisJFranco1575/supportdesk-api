using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Auth;

namespace SupportDesk.Infrastructure.Auth;

/// <summary>
/// Password hashing with the battle-tested hasher from ASP.NET Core Identity
/// (PBKDF2, HMAC-SHA512, random salt, versioned format). Nothing is hand-rolled.
/// </summary>
public sealed class IdentityPasswordHasher : SupportDesk.Application.Auth.IPasswordHasher
{
    // The hasher does not use the user object; any reference works.
    private static readonly object Subject = new();

    private readonly PasswordHasher<object> _hasher = new(
        Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(Subject, passwordHash, password) != PasswordVerificationResult.Failed;
}