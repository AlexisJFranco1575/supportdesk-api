using SupportDesk.Domain.Users;

namespace SupportDesk.Application.Auth;

/// <summary>
/// The user making the current request. The Application layer only sees this interface;
/// the API implements it from the validated token.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    UserRole? Role { get; }
}