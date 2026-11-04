using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure.Cosmos;

/// <summary>Readiness check: the store container can be read within 3 seconds.</summary>
public sealed class CosmosHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly CosmosClient _client;
    private readonly CosmosOptions _options;

    public CosmosHealthCheck(CosmosClient client, IOptions<CosmosOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            var container = _client.GetContainer(_options.Database, _options.StoreContainer);
            await container.ReadContainerAsync(cancellationToken: timeout.Token);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cosmos DB is unreachable.", ex);
        }
    }
}
