using IplStore.Application.Abstractions;
using IplStore.Application.Common;
using IplStore.Application.Contracts;
using IplStore.Application.Shipping;
using IplStore.Domain.Carts;
using IplStore.Domain.Catalogue;
using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Services;

public sealed class CartService
{
    private readonly ICartRepository _carts;
    private readonly IProductRepository _products;
    private readonly IShippingPolicy _shipping;
    private readonly TimeProvider _clock;
    private readonly StoreMetrics _metrics;
    private readonly StoreOptions _options;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ICartRepository carts,
        IProductRepository products,
        IShippingPolicy shipping,
        TimeProvider clock,
        StoreMetrics metrics,
        IOptions<StoreOptions> options,
        ILogger<CartService> logger)
    {
        _carts = carts;
        _products = products;
        _shipping = shipping;
        _clock = clock;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CartDto> GetAsync(string userId, CancellationToken ct)
    {
        var cart = await _carts.GetAsync(userId, ct);
        return await BuildCartDtoAsync(cart?.Lines ?? [], ct);
    }

    public async Task<CartDto> AddAsync(string userId, string requestId, string productId, int quantity, CancellationToken ct)
    {
        Cart.ValidateQuantity(quantity);
        var product = await GetProductAsync(productId, ct);

        return await ChangeCartAsync(userId, requestId, $"add:{productId}:{quantity}", cart =>
        {
            var updated = cart.Add(product, quantity);
            var newQuantity = updated.Lines.First(line => line.ProductId == productId).Quantity;
            EnsureInStock(product, newQuantity);
            return updated;
        }, ct);
    }

    public async Task<CartDto> SetQuantityAsync(string userId, string requestId, string productId, int quantity, CancellationToken ct)
    {
        Cart.ValidateQuantity(quantity);
        var product = await GetProductAsync(productId, ct);

        return await ChangeCartAsync(userId, requestId, $"set:{productId}:{quantity}", cart =>
        {
            var updated = cart.SetQuantity(productId, quantity);
            EnsureInStock(product, quantity);
            return updated;
        }, ct);
    }

    public Task<CartDto> RemoveAsync(string userId, string requestId, string productId, CancellationToken ct)
    {
        return ChangeCartAsync(userId, requestId, $"remove:{productId}", cart => cart.Remove(productId), ct);
    }

    /// <summary>The shopper has seen the new prices and agrees to them.</summary>
    public async Task<CartDto> AcceptChangesAsync(string userId, string requestId, CancellationToken ct)
    {
        var existing = await _carts.GetAsync(userId, ct);
        var productIds = (existing?.Lines ?? []).Select(line => line.ProductId).ToList();
        var currentProducts = (await _products.GetManyAsync(productIds, ct)).ToDictionary(product => product.Id);

        return await ChangeCartAsync(userId, requestId, "accept", cart => cart.AcceptPrices(currentProducts), ct);
    }

    private async Task<Product> GetProductAsync(string productId, CancellationToken ct)
    {
        var product = await _products.GetAsync(productId, ct);
        return product ?? throw new NotFoundException("Product not found.");
    }

    private static void EnsureInStock(Product product, int wanted)
    {
        if (wanted <= product.Stock)
        {
            return;
        }

        var message = product.Stock == 0
            ? $"'{product.Name}' is out of stock."
            : $"Only {product.Stock} of '{product.Name}' available.";
        var failedLine = new FailedLine(product.Id, product.Name, wanted, product.Stock);

        throw new ConflictException("insufficient_stock", message, new OutOfStockDetails([failedLine]));
    }

    /// <summary>
    /// Read, check the request id, change, then save with compare and swap on the cart's ETag.
    /// A repeated request id returns the recorded result and writes nothing. If two copies of the same id arrive at once,
    /// one wins the ETag race and the other retries, then finds the id recorded.
    /// The same id sent with a different change (a client bug) is rejected with 409 instead of answered with an unrelated result.
    /// </summary>
    private async Task<CartDto> ChangeCartAsync(
        string userId, string requestId, string fingerprint, Func<Cart, Cart> change, CancellationToken ct)
    {
        var lines = await ConcurrencyRetry.RunAsync(async () =>
        {
            var current = await _carts.GetAsync(userId, ct) ?? new Cart { UserId = userId };

            var processed = current.FindProcessed(requestId);
            if (processed is not null)
            {
                if (processed.Fingerprint is not null && processed.Fingerprint != fingerprint)
                {
                    throw new ConflictException(
                        "idempotency_key_reused",
                        "This request id was already used for a different cart change. Send a new id.");
                }

                _metrics.IdempotentReplays.Add(1);
                _logger.LogInformation("Replayed cart request {RequestId}", requestId);
                return processed.ResultLines;
            }

            var updated = change(current).Record(requestId, _clock.GetUtcNow(), fingerprint);
            var saved = await _carts.SaveAsync(updated, ct);
            return saved.Lines;
        }, _metrics, _options.MaxConcurrencyAttempts, ct);

        return await BuildCartDtoAsync(lines, ct);
    }

    /// <summary>Joins the cart lines with the current products, so the shopper sees price changes and stock problems.</summary>
    private async Task<CartDto> BuildCartDtoAsync(IReadOnlyList<CartLine> lines, CancellationToken ct)
    {
        var productsById = new Dictionary<string, Product>();
        if (lines.Count > 0)
        {
            var productIds = lines.Select(line => line.ProductId).ToList();
            productsById = (await _products.GetManyAsync(productIds, ct)).ToDictionary(product => product.Id);
        }

        var lineDtos = lines
            .Select(line => BuildLineDto(line, productsById.GetValueOrDefault(line.ProductId)))
            .ToList();

        var itemCount = lineDtos.Sum(line => line.Quantity);
        var total = lineDtos.Sum(line => line.LineTotal);
        var acceptedTotal = lineDtos.Sum(line => Money.Round(line.Quantity * line.AcceptedUnitPrice));
        var requiresAcceptance = lineDtos.Any(line => line.PriceChanged);
        var canCheckout = lineDtos.Count > 0
            && !requiresAcceptance
            && lineDtos.All(line => !line.InsufficientStock);

        // Same policy and same input (item total at current prices) as checkout, so the grand total shown is exactly what is charged.
        var shippingFee = _shipping.Calculate(total);

        return new CartDto(lineDtos, itemCount, total, acceptedTotal, requiresAcceptance, canCheckout, shippingFee, total + shippingFee);
    }

    private static CartLineDto BuildLineDto(CartLine line, Product? product)
    {
        // A product that no longer exists keeps its accepted price and shows as out of stock.
        var currentPrice = product?.Price ?? line.AcceptedUnitPrice;
        var availableStock = product?.Stock ?? 0;

        return new CartLineDto(
            ProductId: line.ProductId,
            ProductName: product?.Name ?? line.ProductName,
            Quantity: line.Quantity,
            AcceptedUnitPrice: line.AcceptedUnitPrice,
            CurrentUnitPrice: currentPrice,
            LineTotal: Money.Round(line.Quantity * currentPrice),
            PriceChanged: currentPrice != line.AcceptedUnitPrice,
            AvailableStock: availableStock,
            StockStatus: product?.StockStatus ?? StockStatus.OutOfStock,
            InsufficientStock: line.Quantity > availableStock,
            ProductType: product?.Type,
            FranchiseId: product?.FranchiseId);
    }
}
