using SupportDesk.Application.Auth;
using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Tests.Tickets;

/// <summary>A current user the test can switch at will.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; private set; }

    public UserRole? Role { get; private set; }

    public void Become(Guid userId, UserRole role)
    {
        UserId = userId;
        Role = role;
    }
}