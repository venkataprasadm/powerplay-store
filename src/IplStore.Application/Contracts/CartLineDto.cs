using IplStore.Domain.Catalogue;

namespace IplStore.Application.Contracts;

/// <remarks><c>ProductType</c> and <c>FranchiseId</c> were added after v1 shipped (additive). They are null if the product was removed.</remarks>
public sealed record CartLineDto(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal AcceptedUnitPrice,
    decimal CurrentUnitPrice,
    decimal LineTotal,
    bool PriceChanged,
    int AvailableStock,
    StockStatus StockStatus,
    bool InsufficientStock,
    ProductType? ProductType = null,
    string? FranchiseId = null);
