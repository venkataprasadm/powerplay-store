using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.ErrorHandling;

/// <summary>
/// Model binding failures (bad JSON, wrong types, missing required fields) get the same problem shape as every other 400.
/// </summary>
public static class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(ToMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Type = "urn:powerplay-store:problem:validation_failed",
        };
        problem.Extensions["code"] = "validation_failed";

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static string ToMessage(Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error)
    {
        return string.IsNullOrEmpty(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage;
    }
}
