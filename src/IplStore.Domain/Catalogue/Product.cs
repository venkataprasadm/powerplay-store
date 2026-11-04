using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;

namespace IplStore.Domain.Catalogue;

/// <summary>
/// Immutable product. Every change returns a new instance, so a stale copy can never be changed by accident.
/// <see cref="ETag"/> is an opaque concurrency token owned by the storage layer.
/// </summary>
public sealed record Product
{
    public const int LowStockThreshold = 5;
    public const int MaxStock = 1_000_000;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ProductType Type { get; init; }
    public required string FranchiseId { get; init; }
    public required string FranchiseName { get; init; }
    public string Description { get; init; } = "";
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string? ETag { get; init; }

    public bool InStock => Stock > 0;

    public StockStatus StockStatus => Stock switch
    {
        <= 0 => StockStatus.OutOfStock,
        <= LowStockThreshold => StockStatus.LowStock,
        _ => StockStatus.InStock,
    };

    public Product WithPrice(decimal price)
    {
        if (!Money.IsValidPrice(price))
        {
            throw new ValidationException("price", $"Price must be greater than 0, at most {Money.MaxPrice:0}, with at most two decimal places.");
        }

        return this with { Price = price };
    }

    public Product WithStock(int stock)
    {
        if (stock is < 0 or > MaxStock)
        {
            throw new ValidationException("stock", $"Stock must be between 0 and {MaxStock}.");
        }

        return this with { Stock = stock };
    }

    /// <summary>Removes units for an order. This is the only place stock goes down, and it never goes below zero.</summary>
    public Product Deduct(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("quantity", "Quantity must be positive.");
        }

        if (quantity > Stock)
        {
            throw new ConflictException("insufficient_stock", $"Only {Stock} of '{Name}' available.");
        }

        return this with { Stock = Stock - quantity };
    }
}
