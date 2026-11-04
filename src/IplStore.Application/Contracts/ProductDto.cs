using IplStore.Domain.Catalogue;

namespace IplStore.Application.Contracts;

/// <remarks><c>StockStatus</c> was added after v1 shipped, next to the original <c>InStock</c> flag (an additive change).</remarks>
public sealed record ProductDto(
    string Id,
    string Name,
    ProductType Type,
    string FranchiseId,
    string FranchiseName,
    string Description,
    decimal Price,
    int Stock,
    bool InStock,
    StockStatus StockStatus,
    string? Etag)
{
    public static ProductDto From(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Type,
            product.FranchiseId,
            product.FranchiseName,
            product.Description,
            product.Price,
            product.Stock,
            product.InStock,
            product.StockStatus,
            product.ETag);
    }
}
