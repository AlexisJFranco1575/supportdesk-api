namespace SupportDesk.Domain;

/// <summary>
/// Raised when a business rule is violated. The API layer maps it to a 4xx response.
/// </summary>
public sealed class DomainException(string message) : Exception(message);