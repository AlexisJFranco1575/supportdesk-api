namespace SupportDesk.Application.Common;

/// <summary>Raised when the request conflicts with existing data. The API maps it to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);