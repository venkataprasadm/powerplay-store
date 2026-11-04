using Microsoft.Extensions.Options;

namespace IplStore.Tests.Storage;

/// <summary>Runs every storage scenario against a real Cosmos DB emulator, in a throwaway database per scenario.</summary>
public class CosmosStoreContractTests
{
    public static IEnumerable<object[]> ScenarioNames => StoreScenarios.All.Keys.Select(name => new object[] { name });

    [CosmosTheory]
    [MemberData(nameof(ScenarioNames))]
    public async Task Scenario(string name)
    {
        var options = new CosmosOptions
        {
            ConnectionString = Environment.GetEnvironmentVariable("COSMOS_TEST_CONNECTION")!,
            Database = $"iplstore-test-{Guid.NewGuid():N}",
            TenantId = "ipl",
            Throughput = 400,
            AllowInsecureEmulatorCertificate = true,
        };

        using var client = CosmosClientFactory.Create(options);
        await CosmosSchema.EnsureAsync(client, options, default);

        try
        {
            var store = new CosmosStore(client, Options.Create(options));
            var carts = new CosmosCartRepository(client, Options.Create(options));

            await StoreScenarios.All[name](new StoreTestTarget(store, store, store, carts));
        }
        finally
        {
            await client.GetDatabase(options.Database).DeleteAsync();
        }
    }
}
