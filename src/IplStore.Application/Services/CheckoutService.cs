using IplStore.Application.Abstractions;
using IplStore.Application.Common;
using IplStore.Application.Contracts;
using IplStore.Application.Shipping;
using IplStore.Domain.Carts;
using IplStore.Domain.Catalogue;
using IplStore.Domain.Exceptions;
using IplStore.Domain.Orders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IplStore.Application.Services;

public sealed class CheckoutService
{
    private readonly ICartRepository _carts;
    private readonly IProductRepository _products;
    private readonly ICheckoutStore _checkoutStore;
    private readonly IOrderRepository _orders;
    private readonly IShippingPolicy _shipping;
    private readonly TimeProvider _clock;
    private readonly StoreMetrics _metrics;
    private readonly StoreOptions _options;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        ICartRepository carts,
        IProductRepository products,
        ICheckoutStore checkoutStore,
        IOrderRepository orders,
        IShippingPolicy shipping,
        TimeProvider clock,
        StoreMetrics metrics,
        IOptions<StoreOptions> options,
        ILogger<CheckoutService> logger)
    {
        _carts = carts;
        _products = products;
        _checkoutStore = checkoutStore;
        _orders = orders;
        _shipping = shipping;
        _clock = clock;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Idempotent on <paramref name="orderId"/>. Each retry round reads fresh product versions, so the outcome is always decided
    /// against current stock: either the order commits atomically, or it is rejected with nothing changed.
    /// </summary>
    public async Task<CheckoutResult> PlaceOrderAsync(string userId, string orderId, decimal expectedTotal, CancellationToken ct)
    {
        if (expectedTotal < 0 || decimal.Round(expectedTotal, 2) != expectedTotal)
        {
            throw new ValidationException("expectedTotal", "Expected total must be a rupee amount with at most two decimal places.");
        }

        var existingOrder = await _orders.GetAsync(orderId, ct);
        if (existingOrder is not null)
        {
            return Replay(existingOrder, userId);
        }

        var cart = await _carts.GetAsync(userId, ct);
        if (cart is null || cart.Lines.Count == 0)
        {
            // An empty cart is also what a retry sees when the first attempt just succeeded and cleared the cart.
            // Answer with that order instead of an error.
            var placedOrder = await _orders.GetAsync(orderId, ct);
            if (placedOrder is not null)
            {
                return Replay(placedOrder, userId);
            }

            throw new ConflictException("cart_empty", "Your cart is empty.");
        }

        try
        {
            var attempt = 0;
            var order = await ConcurrencyRetry.RunAsync(
                () =>
                {
                    attempt++;
                    return TryCommitAsync(userId, orderId, expectedTotal, cart, isRetry: attempt > 1, ct);
                },
                _metrics,
                _options.MaxConcurrencyAttempts,
                ct);

            _metrics.OrdersPlaced.Add(1);
            _logger.LogInformation("Order {OrderId} placed for {UserId}, total {Total}", orderId, userId, order.Total);

            await ClearCartAsync(userId, ct);
            return new CheckoutResult(OrderDto.From(order), Replayed: false);
        }
        catch (DuplicateOrderException)
        {
            // A concurrent retry of the same order id won the race. The batch is atomic, so exactly one set of deductions exists.
            var winningOrder = await _orders.GetAsync(orderId, ct)
                ?? throw new ConflictException("concurrency_conflict", "Please retry.");
            return Replay(winningOrder, userId);
        }
    }

    private CheckoutResult Replay(Order existingOrder, string userId)
    {
        if (existingOrder.UserId != userId)
        {
            throw new ConflictException("order_id_in_use", "That order id is already in use.");
        }

        _metrics.IdempotentReplays.Add(1);
        return new CheckoutResult(OrderDto.From(existingOrder), Replayed: true);
    }

    private async Task<Order> TryCommitAsync(
        string userId, string orderId, decimal expectedTotal, Cart cart, bool isRetry, CancellationToken ct)
    {
        // After a lost race, first check whether the winner was a concurrent copy of this same order. If so, return that order
        // instead of checking stock again (the winner just used it) and wrongly reporting out of stock.
        if (isRetry && await _orders.GetAsync(orderId, ct) is not null)
        {
            throw new DuplicateOrderException(orderId);
        }

        var productIds = cart.Lines.Select(line => line.ProductId).ToList();
        var products = (await _products.GetManyAsync(productIds, ct)).ToDictionary(product => product.Id);

        // 1. Stock first. Any shortfall rejects the whole order and names every failing line.
        var failedLines = FindFailedLines(cart, products);
        if (failedLines.Count > 0)
        {
            _metrics.CheckoutRejected.Add(1);
            throw new ConflictException(
                "out_of_stock",
                "Some items are no longer available in the quantity requested. No order was created and your cart is unchanged.",
                new OutOfStockDetails(failedLines));
        }

        // 2. Price consent. The shopper must have accepted the exact total they are about to pay.
        var order = BuildOrder(userId, orderId, cart, products);
        var pricesChanged = cart.Lines.Any(line => products[line.ProductId].Price != line.AcceptedUnitPrice);
        if (pricesChanged || order.Total != expectedTotal)
        {
            _metrics.CheckoutRejected.Add(1);
            throw new ConflictException(
                "cart_changed",
                "Prices changed since you last reviewed your cart. Review and accept the new total.",
                new CartChangedDetails(order.Total));
        }

        // 3. One atomic commit: the stock deductions (ETag guarded), the audit trail and the order.
        var stockChanges = BuildStockChanges(userId, orderId, cart, products);
        await _checkoutStore.CommitAsync(order, stockChanges, ct);
        return order;
    }

    private static List<FailedLine> FindFailedLines(Cart cart, IReadOnlyDictionary<string, Product> products)
    {
        var failedLines = new List<FailedLine>();
        foreach (var line in cart.Lines)
        {
            var product = products.GetValueOrDefault(line.ProductId);
            if (product is null || product.Stock < line.Quantity)
            {
                var name = product?.Name ?? line.ProductName;
                var available = product?.Stock ?? 0;
                failedLines.Add(new FailedLine(line.ProductId, name, line.Quantity, available));
            }
        }

        return failedLines;
    }

    private Order BuildOrder(string userId, string orderId, Cart cart, IReadOnlyDictionary<string, Product> products)
    {
        // Name and price are copied from the current product, so later catalogue changes never alter the order.
        var lines = cart.Lines
            .Select(line => new OrderLine(line.ProductId, products[line.ProductId].Name, line.Quantity, products[line.ProductId].Price))
            .ToList();

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            CreatedUtc = _clock.GetUtcNow(),
            Lines = lines,
        };

        return order with { Shipping = _shipping.Calculate(order.Subtotal) };
    }

    private List<StockChange> BuildStockChanges(string userId, string orderId, Cart cart, IReadOnlyDictionary<string, Product> products)
    {
        var now = _clock.GetUtcNow();
        var changes = new List<StockChange>();

        foreach (var line in cart.Lines)
        {
            var before = products[line.ProductId];
            var after = before.Deduct(line.Quantity);
            var audit = new AuditEntry(
                Guid.NewGuid().ToString("N"),
                before.Id,
                AuditField.Stock,
                before.Stock,
                after.Stock,
                Reason: $"order:{orderId}",
                Actor: userId,
                AtUtc: now);

            changes.Add(new StockChange(after, audit));
        }

        return changes;
    }

    private async Task ClearCartAsync(string userId, CancellationToken ct)
    {
        try
        {
            await _carts.DeleteAsync(userId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The order is already committed. A leftover cart does not affect correctness (a retry returns the same order),
            // so the request does not fail.
            _logger.LogWarning(ex, "Order committed but the cart for {UserId} could not be cleared", userId);
        }
    }
}
