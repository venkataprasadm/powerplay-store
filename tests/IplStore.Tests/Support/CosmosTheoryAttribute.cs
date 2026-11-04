namespace IplStore.Tests.Support;

/// <summary>A theory that only runs when COSMOS_TEST_CONNECTION points at a Cosmos DB emulator. Otherwise it is reported as skipped.</summary>
public sealed class CosmosTheoryAttribute : TheoryAttribute
{
    public CosmosTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMOS_TEST_CONNECTION")))
        {
            Skip = "Set COSMOS_TEST_CONNECTION to run against the Cosmos DB emulator.";
        }
    }
}
