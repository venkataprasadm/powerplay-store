namespace IplStore.Tests.Storage;

public class InMemoryStoreContractTests
{
    public static IEnumerable<object[]> ScenarioNames => StoreScenarios.All.Keys.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task Scenario(string name)
    {
        var store = new InMemoryStore();
        var target = new StoreTestTarget(store, store, store, new InMemoryCartRepository());

        await StoreScenarios.All[name](target);
    }
}
