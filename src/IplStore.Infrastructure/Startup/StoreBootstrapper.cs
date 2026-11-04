using IplStore.Application.Abstractions;
using IplStore.Application.Seeding;
using IplStore.Infrastructure.Cosmos;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure.Startup;

/// <summary>
/// Runs once at startup. Creates the Cosmos database and containers (when Cosmos is the provider) and seeds the catalogue.
/// Both steps are idempotent and retried, so the API tolerates an emulator that is still warming up.
/// </summary>
public sealed class StoreBootstrapper : IHostedService
{
    private const int MaxAttempts = 12;

    private readonly IServiceProvider _services;
    private readonly CosmosOptions _cosmosOptions;
    private readonly SeedOptions _seedOptions;
    private readonly TimeProvider _clock;
    private readonly ILogger<StoreBootstrapper> _logger;

    public StoreBootstrapper(
        IServiceProvider services,
        IOptions<CosmosOptions> cosmosOptions,
        IOptions<SeedOptions> seedOptions,
        TimeProvider clock,
        ILogger<StoreBootstrapper> logger)
    {
        _services = services;
        _cosmosOptions = cosmosOptions.Value;
        _seedOptions = seedOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // The Cosmos client is only registered when Cosmos is the storage provider.
        var cosmosClient = _services.GetService<CosmosClient>();
        if (cosmosClient is not null && _cosmosOptions.CreateIfNotExists)
        {
            await RetryAsync(() => CosmosSchema.EnsureAsync(cosmosClient, _cosmosOptions, ct), ct);
        }

        if (_seedOptions.Enabled)
        {
            await RetryAsync(() => SeedProductsAsync(ct), ct);
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private async Task SeedProductsAsync(CancellationToken ct)
    {
        var products = _services.GetRequiredService<IProductRepository>();

        var created = 0;
        foreach (var product in SeedData.Products(_clock.GetUtcNow().AddYears(-1)))
        {
            if (await products.CreateIfAbsentAsync(product, ct))
            {
                created++;
            }
        }

        _logger.LogInformation("Seeded {Count} new products", created);
    }

    private async Task RetryAsync(Func<Task> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Store bootstrap attempt {Attempt} failed, retrying", attempt);

                var delay = TimeSpan.FromSeconds(Math.Min(10, attempt * 2));
                await Task.Delay(delay, ct);
            }
        }
    }
}
