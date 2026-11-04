using System.Text.Json.Serialization;
using IplStore.Domain.Catalogue;
using IplStore.Domain.Common;

namespace IplStore.Infrastructure.Cosmos.Documents;

/// <summary>
/// Storage shape of a product. Kept separate from the domain, so storage naming (id, _etag, tenantId, type) never leaks upwards.
/// Timestamps are fixed-width strings (see <see cref="Timestamp"/>), so ORDER BY and range filters behave like time.
/// </summary>
internal sealed class ProductDocument : IKeysetDocument
{
    public const string Kind = "product";

    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Type { get; set; } = Kind;
    public string Name { get; set; } = "";
    public string ProductType { get; set; } = "";
    public string FranchiseId { get; set; } = "";
    public string FranchiseName { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string CreatedUtc { get; set; } = "";

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    public static ProductDocument FromDomain(Product product, string tenantId)
    {
        return new ProductDocument
        {
            Id = product.Id,
            TenantId = tenantId,
            Name = product.Name,
            ProductType = product.Type.ToString(),
            FranchiseId = product.FranchiseId,
            FranchiseName = product.FranchiseName,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            CreatedUtc = Timestamp.Format(product.CreatedUtc),
        };
    }

    public Product ToDomain()
    {
        return new Product
        {
            Id = Id,
            Name = Name,
            Type = Enum.Parse<ProductType>(ProductType),
            FranchiseId = FranchiseId,
            FranchiseName = FranchiseName,
            Description = Description,
            Price = Price,
            Stock = Stock,
            CreatedUtc = Timestamp.Parse(CreatedUtc),
            ETag = ETag,
        };
    }
}
