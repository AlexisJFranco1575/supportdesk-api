using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Common;
using SupportDesk.Domain;

namespace SupportDesk.Api.ErrorHandling;

/// <summary>
/// Translates known exceptions into RFC 9457 problem responses.
/// Anything unexpected falls through to the default 500 handling without leaking details.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            NotFoundException =>
                Problem(StatusCodes.Status404NotFound, "Resource not found", exception.Message),

            ConflictException =>
                Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),

            ForbiddenException =>
                Problem(StatusCodes.Status403Forbidden, "Forbidden", exception.Message),

            InvalidCredentialsException =>
                Problem(StatusCodes.Status401Unauthorized, "Authentication failed", exception.Message),

            RequestValidationException validation =>
                new ValidationProblemDetails(validation.Errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                },

            DomainException =>
                Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", exception.Message),

            // Malformed JSON, unknown enum values, invalid query-string values, etc.
            BadHttpRequestException badRequest =>
                Problem(badRequest.StatusCode, "Invalid request", "The request is malformed or contains invalid values."),

            _ => null
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}