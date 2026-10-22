using SupportDesk.Domain;
using SupportDesk.Domain.Users;
using Xunit;

namespace SupportDesk.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_the_email_and_sets_the_initial_state()
    {
        var user = User.Create("  Ana@Example.COM ", " Ana Perez ", "some-hash", UserRole.Customer, Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("ana@example.com", user.Email);
        Assert.Equal("Ana Perez", user.FullName);
        Assert.Equal("some-hash", user.PasswordHash);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Display Name <ana@example.com>")]
    public void Create_rejects_an_invalid_email(string? email)
    {
        Assert.Throws<DomainException>(() =>
            User.Create(email, "Ana Perez", "some-hash", UserRole.Customer, Now));
    }

    [Fact]
    public void Create_rejects_a_missing_full_name()
    {
        Assert.Throws<DomainException>(() =>
            User.Create("ana@example.com", "   ", "some-hash", UserRole.Customer, Now));
    }

    [Fact]
    public void Create_rejects_an_empty_password_hash()
    {
        Assert.Throws<DomainException>(() =>
            User.Create("ana@example.com", "Ana Perez", "", UserRole.Customer, Now));
    }

    [Fact]
    public void ChangeRole_updates_the_role()
    {
        var user = User.Create("ana@example.com", "Ana Perez", "some-hash", UserRole.Customer, Now);

        user.ChangeRole(UserRole.Agent);

        Assert.Equal(UserRole.Agent, user.Role);
    }

    [Fact]
    public void ChangeRole_rejects_an_unknown_role()
    {
        var user = User.Create("ana@example.com", "Ana Perez", "some-hash", UserRole.Customer, Now);

        Assert.Throws<DomainException>(() => user.ChangeRole((UserRole)99));
    }
}