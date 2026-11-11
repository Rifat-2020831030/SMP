using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace SchoolAdmission.Api.Infrastructure;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var expected = exception as AdmissionFailure;
        var status = expected?.Status ?? StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = expected is null ? "An unexpected error occurred." : "The request could not be completed.",
            Status = status,
            Detail = expected?.Detail ?? "An unexpected error occurred. Please try again later."
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem, cancellationToken: cancellationToken);
        return true;
    }
}
