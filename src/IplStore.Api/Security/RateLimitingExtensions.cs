using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace IplStore.Api.Security;

public static class RateLimitingExtensions
{
    /// <summary>Per client fixed window limits for search and checkout. Excess requests get 429 with Retry-After.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitSettings>().BindConfiguration("RateLimit");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Search, context => GetPartition(context, settings => settings.Search));
            options.AddPolicy(RateLimitPolicies.Checkout, context => GetPartition(context, settings => settings.Checkout));
            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    /// <summary>
    /// One limit per client: the signed-in user, otherwise the remote address.
    /// Behind a proxy, enable forwarded headers so the remote address is the real client.
    /// </summary>
    private static RateLimitPartition<string> GetPartition(HttpContext context, Func<RateLimitSettings, RateLimitWindow> selectWindow)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        var clientKey = context.User.FindFirstValue("sub")
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        if (!settings.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(clientKey);
        }

        var window = selectWindow(settings);
        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = window.PermitLimit,
            Window = TimeSpan.FromSeconds(window.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Rate limit exceeded. Retry after the number of seconds in the Retry-After header.",
            Type = "urn:powerplay-store:problem:rate_limited",
        };
        problem.Extensions["code"] = "rate_limited";

        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
