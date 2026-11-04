namespace IplStore.Tests.Storage;

/// <summary>The storage implementations a scenario runs against, all backed by the same store.</summary>
public sealed record StoreTestTarget(
    IProductRepository Products,
    ICheckoutStore Checkout,
    IOrderRepository Orders,
    ICartRepository Carts);
