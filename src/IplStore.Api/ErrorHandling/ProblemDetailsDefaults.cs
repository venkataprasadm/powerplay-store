using System.Diagnostics;
using IplStore.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace IplStore.Api.ErrorHandling;

public static class ProblemDetailsDefaults
{
    /// <summary>
    /// Adds the instance, trace id, correlation id and a default code to every problem response,
    /// including the 401, 403 and 404 responses the framework produces itself.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Instance ??= context.HttpContext.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();

        if (!problem.Extensions.ContainsKey("code"))
        {
            problem.Extensions["code"] = DefaultCode(problem.Status);
        }
    }

    private static string DefaultCode(int? status)
    {
        return status switch
        {
            400 => "validation_failed",
            401 => "unauthorized",
            403 => "forbidden",
            404 => "not_found",
            405 => "method_not_allowed",
            415 => "unsupported_media_type",
            429 => "rate_limited",
            _ => "error",
        };
    }
}
