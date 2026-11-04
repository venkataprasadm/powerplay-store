namespace IplStore.Api.Hosting;

public static class CorsExtensions
{
    /// <summary>
    /// Cross-origin access is opt in. With no Cors:AllowedOrigins configured, no CORS headers are sent at all.
    /// It is only needed when the front end is served from another origin than the API (for example GitHub Pages).
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("ETag", "Retry-After", "Idempotent-Replayed", "X-Correlation-Id");
        }));

        return services;
    }
}
