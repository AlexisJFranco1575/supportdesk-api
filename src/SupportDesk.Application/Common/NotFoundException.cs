namespace SupportDesk.Application.Common;

/// <summary>
/// Raised when a requested resource does not exist. The API maps it to HTTP 404.
/// </summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");