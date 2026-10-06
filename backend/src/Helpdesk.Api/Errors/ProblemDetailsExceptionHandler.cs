using System.Text.Json;
using FluentValidation;
using Helpdesk.Application.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Errors;

/// <summary>
/// Maps Application exceptions to problem+json. Anything not listed falls through to the default
/// handler, which answers a generic 500 without exception details.
/// </summary>
internal sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails? problemDetails = exception switch
        {
            ValidationException validationException => CreateValidationProblem(validationException),
            InvalidCredentialsException or InvalidRefreshTokenException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Detail = exception.Message
            },
            EmailAlreadyRegisteredException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = exception.Message
            },
            _ => null
        };

        if (problemDetails is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    // Keys use camelCase so they match the JSON property names the client sent.
    private static ValidationProblemDetails CreateValidationProblem(ValidationException exception) =>
        new(exception.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest
        };
}
