using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;

namespace SupportDesk.Domain.Users;

public sealed class User
{
    public const int EmailMaxLength = 254;
    public const int FullNameMaxLength = 100;
    public const int PasswordHashMaxLength = 500;

    // Needed by the persistence layer (EF Core).
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static bool IsValidEmail([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= EmailMaxLength
        && MailAddress.TryCreate(value, out var address)
        && address.Address == value;

    public static User Create(
        string? email,
        string? fullName,
        string? passwordHash,
        UserRole role,
        DateTimeOffset now)
    {
        var normalizedEmail = email?.Trim();

        if (!IsValidEmail(normalizedEmail))
        {
            throw new DomainException("The email is not a valid address.");
        }

        var name = fullName?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new DomainException("The full name is required.");
        }

        if (name.Length > FullNameMaxLength)
        {
            throw new DomainException($"The full name cannot exceed {FullNameMaxLength} characters.");
        }

        if (string.IsNullOrEmpty(passwordHash))
        {
            throw new DomainException("A password hash is required.");
        }

        EnsureValidRole(role);

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail.ToLowerInvariant(),
            FullName = name,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = now
        };
    }

    public void ChangeRole(UserRole role)
    {
        EnsureValidRole(role);
        Role = role;
    }

    private static void EnsureValidRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("The role is not valid.");
        }
    }
}