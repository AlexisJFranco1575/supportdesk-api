namespace SupportDesk.Application.Common;

/// <summary>Raised when one or more input fields are invalid. The API maps it to HTTP 400.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}