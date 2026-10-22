namespace SupportDesk.Application.Common;

/// <summary>Raised when an authenticated user is not allowed to do something. The API maps it to HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);