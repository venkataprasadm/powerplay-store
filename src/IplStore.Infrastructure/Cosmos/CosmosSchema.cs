using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;

namespace IplStore.Infrastructure.Cosmos;

/// <summary>
/// Creates the database and containers with their partition keys and indexing policies. scripts/create-cosmos.sh defines the same.
/// Partition keys cannot be changed after a container is created.
/// </summary>
public static class CosmosSchema
{
    private const int CartTimeToLiveSeconds = 60 * 60 * 24 * 30;

    public static async Task EnsureAsync(CosmosClient client, CosmosOptions options, CancellationToken ct)
    {
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(options.Database, cancellationToken: ct);
        var database = databaseResponse.Database;

        await database.CreateContainerIfNotExistsAsync(
            StoreContainer(options),
            ThroughputProperties.CreateManualThroughput(options.Throughput),
            cancellationToken: ct);

        await database.CreateContainerIfNotExistsAsync(
            CartContainer(options),
            ThroughputProperties.CreateManualThroughput(options.Throughput),
            cancellationToken: ct);
    }

    private static ContainerProperties StoreContainer(CosmosOptions options)
    {
        var container = new ContainerProperties(options.StoreContainer, "/tenantId");

        // One composite index serves every ORDER BY in CosmosQueries: (createdUtc, id) ascending, and its exact reverse for newest first.
        container.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new() { Path = "/createdUtc", Order = CompositePathSortOrder.Ascending },
            new() { Path = "/id", Order = CompositePathSortOrder.Ascending },
        });

        // These properties are never filtered or sorted on, so indexing them would only add RU to every write.
        // The root path is included explicitly because the vNext emulator rejects excluded sub-paths without it.
        container.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        foreach (var path in new[] { "/description/?", "/lines/*", "/\"_etag\"/?" })
        {
            container.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = path });
        }

        return container;
    }

    private static ContainerProperties CartContainer(CosmosOptions options)
    {
        // Abandoned carts expire after 30 idle days.
        var container = new ContainerProperties(options.CartContainer, "/userId")
        {
            DefaultTimeToLive = CartTimeToLiveSeconds,
        };

        // Carts are only read by id, so indexing is turned off to save RU on every write.
        container.IndexingPolicy.IncludedPaths.Clear();
        container.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });

        return container;
    }
}
