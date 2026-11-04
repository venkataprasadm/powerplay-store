using System.Diagnostics;
using System.Text.RegularExpressions;

namespace IplStore.Api.Middleware;

/// <summary>
/// Runs first for every request. Accepts or creates the X-Correlation-Id, puts it on every log line and the response,
/// writes one access log line, and adds safe default response headers.
/// </summary>
public sealed partial class RequestContextMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestContextMiddleware> _logger;

    public RequestContextMiddleware(RequestDelegate next, ILogger<RequestContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        context.Response.OnStarting(() =>
        {
            AddResponseHeaders(context, correlationId);
            return Task.CompletedTask;
        });

        var startedAt = Stopwatch.GetTimestamp();
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                // Path only. Query strings and bodies are never logged: they can carry search text, tokens or personal data.
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        // Only a well-formed id is accepted. Writing an arbitrary header value into the logs would allow log forging.
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        if (supplied is not null && SafeIdPattern().IsMatch(supplied))
        {
            return supplied;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static void AddResponseHeaders(HttpContext context, string correlationId)
    {
        var headers = context.Response.Headers;
        headers[HeaderName] = correlationId;
        headers["X-Content-Type-Options"] = "nosniff";

        // Responses to signed-in requests hold user specific data, which must not sit in shared caches.
        if (context.Request.Headers.ContainsKey("Authorization") && !headers.ContainsKey("Cache-Control"))
        {
            headers["Cache-Control"] = "no-store";
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeIdPattern();
}
