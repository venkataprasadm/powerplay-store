namespace IplStore.Api.Middleware;

public static class HttpContextExtensions
{
    /// <summary>The correlation id set by <see cref="RequestContextMiddleware"/>.</summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items[RequestContextMiddleware.ItemKey] as string;
    }
}
