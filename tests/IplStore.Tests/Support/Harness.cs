using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IplStore.Tests.Support;

/// <summary>Wires the real services to the in-memory store, seeded with the brief's walkthrough catalogue.</summary>
public sealed class Harness
{
    public static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    /// <param name="shipping">Defaults to free shipping, which is what the brief's worked totals assume.</param>
    public Harness(IShippingPolicy? shipping = null)
    {
        shipping ??= new FreeShippingPolicy();
        var options = Options.Create(new StoreOptions { MaxConcurrencyAttempts = 8 });

        Catalog = new CatalogService(Store, Clock, NullLogger<CatalogService>.Instance);
        Cart = new CartService(Carts, Store, shipping, Clock, Metrics, options, NullLogger<CartService>.Instance);
        Checkout = new CheckoutService(Carts, Store, Store, Store, shipping, Clock, Metrics, options, NullLogger<CheckoutService>.Instance);
        Orders = new OrderService(Store);
    }

    public InMemoryStore Store { get; } = new();
    public InMemoryCartRepository Carts { get; } = new();
    public FakeClock Clock { get; } = new(Start);
    public StoreMetrics Metrics { get; } = new();
    public CatalogService Catalog { get; }
    public CartService Cart { get; }
    public CheckoutService Checkout { get; }
    public OrderService Orders { get; }

    public static async Task<Harness> CreateAsync(IShippingPolicy? shipping = null)
    {
        var harness = new Harness(shipping);
        foreach (var product in SeedData.Products(Start.AddDays(-30)))
        {
            await harness.Store.CreateIfAbsentAsync(product, default);
        }

        return harness;
    }

    public static string Key()
    {
        return Guid.NewGuid().ToString("D");
    }

    public static string OrderId()
    {
        return Guid.NewGuid().ToString("D");
    }

    public Task<CartDto> Add(string userId, string productId, int quantity, string? key = null)
    {
        return Cart.AddAsync(userId, key ?? Key(), productId, quantity, default);
    }

    public async Task<ProductDto> Admin(string productId, decimal? price = null, int? stock = null, string reason = "manual adjustment")
    {
        var current = await Catalog.GetAsync(productId, default);
        return await Catalog.UpdateAsync(productId, current.Etag, price, stock, reason, "admin", default);
    }

    public async Task<int> StockOf(string productId)
    {
        var product = await Catalog.GetAsync(productId, default);
        return product.Stock;
    }
}
