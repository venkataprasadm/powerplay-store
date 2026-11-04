using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace IplStore.Tests.Storage;

/// <summary>
/// Checks the SQL the SDK generates from the LINQ read queries. Translation is local, so these run without a database (the client never connects).
/// They pin what the storage contract tests can only check with an emulator: names match what the serializer writes, every list is ordered by the
/// composite index (createdUtc, id), and user input stays inside an escaped literal.
/// </summary>
public class CosmosQueryTests
{
    private static readonly Container Container = CosmosClientFactory.Create(new CosmosOptions
    {
        ConnectionString = "AccountEndpoint=http://localhost:1/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
        UseGatewayMode = true,
    }).GetContainer("db", "store");

    private static readonly KeysetCursor Cursor = new("2026-01-01T10:00:00.0000000Z", "mi-jersey");

    private static string Sql<T>(IQueryable<T> q) => q.ToQueryDefinition().QueryText;

    [Fact]
    public void Product_search_uses_stored_names_and_the_composite_index_order()
    {
        var sql = Sql(CosmosQueries.Page(CosmosQueries.Products(Container.GetItemLinqQueryable<ProductDocument>(), new ProductQuery("jer", ProductType.Jersey, "mi", 20, Cursor)), Cursor, newestFirst: false).Take(21));

        Assert.StartsWith("SELECT TOP 21 VALUE root FROM root WHERE", sql);
        Assert.Contains("""root["type"] = "product""", sql);
        Assert.Contains("""CONTAINS(root["name"], "jer", true)""", sql);
        Assert.Contains("""root["productType"] = "Jersey""", sql);
        Assert.Contains("""root["franchiseId"] = "mi""", sql);
        Assert.Contains("""(root["createdUtc"] > "2026-01-01T10:00:00.0000000Z") OR ((root["createdUtc"] = "2026-01-01T10:00:00.0000000Z") AND (root["id"] > "mi-jersey"))""", sql);
        Assert.EndsWith("""ORDER BY root["createdUtc"] ASC, root["id"] ASC""", sql);
    }

    [Fact]
    public void Order_history_is_newest_first_with_inclusive_from_and_exclusive_to()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var sql = Sql(CosmosQueries.Page(CosmosQueries.Orders(Container.GetItemLinqQueryable<OrderDocument>(), new OrderQuery("asha", from, from.AddDays(1), 10, Cursor)), Cursor, newestFirst: true).Take(11));

        Assert.Contains("""root["type"] = "order""", sql);
        Assert.Contains("""root["userId"] = "asha""", sql);
        Assert.Contains("""root["createdUtc"] >= "2026-01-01T00:00:00.0000000Z""", sql);
        Assert.Contains("""root["createdUtc"] < "2026-01-02T00:00:00.0000000Z""", sql);
        Assert.Contains("""(root["createdUtc"] < "2026-01-01T10:00:00.0000000Z") OR ((root["createdUtc"] = "2026-01-01T10:00:00.0000000Z") AND (root["id"] < "mi-jersey"))""", sql);
        Assert.EndsWith("""ORDER BY root["createdUtc"] DESC, root["id"] DESC""", sql);
    }

    [Fact]
    public void Count_ignores_the_cursor_and_ordering()
    {
        var sql = Sql(CosmosQueries.Products(Container.GetItemLinqQueryable<ProductDocument>(), new ProductQuery(null, null, "csk", 20, Cursor)));

        Assert.Equal("""SELECT VALUE root FROM root WHERE ((root["type"] = "product") AND (root["franchiseId"] = "csk"))""", sql);
    }

    [Theory]
    [InlineData("""cap" OR root["type"] = "order""")]
    [InlineData("cap') OR 1=1 --")]
    [InlineData("\\\" OR true OR \\\"")]
    public void Search_text_cannot_break_out_of_its_string_literal(string text)
    {
        var sql = Sql(CosmosQueries.Products(Container.GetItemLinqQueryable<ProductDocument>(), new ProductQuery(text, null, null, 20, null)));

        // The SDK writes values as JSON string literals with minimal escaping (\" and \\), so the text stays one literal.
        var literal = JsonSerializer.Serialize(text, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Equal($"""SELECT VALUE root FROM root WHERE ((root["type"] = "product") AND CONTAINS(root["name"], {literal}, true))""", sql);
    }
}
