using System.Text.Json.Serialization;
using IplStore.Api.ErrorHandling;
using IplStore.Api.Hosting;
using IplStore.Api.Middleware;
using IplStore.Api.Security;
using IplStore.Api.Serialization;
using IplStore.Application;
using IplStore.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logs. Scopes carry the correlation id onto every line.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "O";
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication();
builder.Services.AddApiRateLimiting();
builder.Services.AddApiRequestTimeouts();
builder.Services.AddApiCors(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create;
    });

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsDefaults.Customize);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// Behind a reverse proxy (Render, Container Apps) this makes the client IP and scheme the real ones.
var forwardedHeadersEnabled = string.Equals(app.Configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<RequestContextMiddleware>();
app.UseExceptionHandler();
app.UseCors();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();
app.UseRateLimiter();

app.MapControllers();

// Liveness: the process is up. Readiness: the dependencies tagged "ready" (Cosmos DB) are reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Lets the integration tests start the API with WebApplicationFactory<Program>.
public partial class Program;
