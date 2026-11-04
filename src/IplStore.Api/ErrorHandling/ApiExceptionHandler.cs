using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using IplStore.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace IplStore.Api.ErrorHandling;

/// <summary>
/// Turns every exception into RFC 7807 problem details with a stable machine readable <c>code</c>.
/// Unexpected errors never leak internal details to the client.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequest = 499;
    private const string RetryAfterSeconds = "5";

    private static readonly JsonSerializerOptions DetailsJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            // The client went away. There is nobody to send a body to.
            context.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var error = Classify(exception);

        if (error.Status >= 500)
        {
            _logger.LogError(exception, "Unhandled failure {Code}", error.Code);
        }
        else
        {
            _logger.LogInformation("Request rejected: {Code}", error.Code);
        }

        if (error.Status == StatusCodes.Status503ServiceUnavailable)
        {
            context.Response.Headers.RetryAfter = RetryAfterSeconds;
        }

        var problem = new ProblemDetails
        {
            Status = error.Status,
            Title = error.Title,
            Detail = error.Detail,
            Type = $"urn:powerplay-store:problem:{error.Code}",
        };
        problem.Extensions["code"] = error.Code;
        AddExtensions(problem, exception);

        context.Response.StatusCode = error.Status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ApiError Classify(Exception exception)
    {
        const string unavailableDetail = "A dependency is temporarily unavailable. Please retry shortly.";

        return exception switch
        {
            ValidationException e => new ApiError(400, e.Code, "Validation failed", e.Message),
            NotFoundException e => new ApiError(404, e.Code, "Not found", e.Message),
            ConflictException e => new ApiError(409, e.Code, "Conflict", e.Message),
            PreconditionFailedException e => new ApiError(412, e.Code, "Precondition failed", e.Message),
            PreconditionRequiredException e => new ApiError(428, e.Code, "Precondition required", e.Message),
            ConcurrencyException => new ApiError(409, "concurrency_conflict", "Conflict", "The data changed while processing your request. Please retry."),

            // Throttling, timeouts and outages of Cosmos DB or the network are temporary: 503 with Retry-After, not 500.
            CosmosException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout }
                => new ApiError(503, "dependency_unavailable", "Service unavailable", unavailableDetail),
            TimeoutException or HttpRequestException or CosmosException
                => new ApiError(503, "dependency_unavailable", "Service unavailable", unavailableDetail),

            _ => new ApiError(500, "internal_error", "Unexpected error", "An unexpected error occurred. Quote the correlation id when contacting support."),
        };
    }

    private static void AddExtensions(ProblemDetails problem, Exception exception)
    {
        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        // Conflict details (for example the failing lines) become top level members of the problem body.
        if (exception is ConflictException { Details: not null } conflict)
        {
            var details = JsonSerializer.SerializeToElement(conflict.Details, DetailsJsonOptions);
            foreach (var property in details.EnumerateObject())
            {
                problem.Extensions[property.Name] = property.Value.Clone();
            }
        }
    }

    private sealed record ApiError(int Status, string Code, string Title, string Detail);
}
