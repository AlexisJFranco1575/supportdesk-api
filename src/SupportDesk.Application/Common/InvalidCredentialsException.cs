namespace SupportDesk.Application.Common;

/// <summary>
/// Raised when a login fails. The message is deliberately the same for an unknown email
/// and a wrong password, so the API does not reveal which accounts exist. Maps to HTTP 401.
/// </summary>
public sealed class InvalidCredentialsException() : Exception("Incorrect email or password.");