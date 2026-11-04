using IplStore.Application.Paging;
using IplStore.Domain.Catalogue;

namespace IplStore.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetAsync(string id, CancellationToken ct);

    Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<string> ids, CancellationToken ct);

    /// <summary>Stable order: CreatedUtc ascending, then Id. Returns at most PageSize items, plus whether more exist.</summary>
    Task<Slice<Product>> SearchAsync(ProductQuery query, CancellationToken ct);

    Task<int> CountAsync(ProductQuery query, CancellationToken ct);

    /// <summary>
    /// Writes the product (compare and swap on its ETag) and its audit entries atomically.
    /// Throws ConcurrencyException when the ETag is stale.
    /// </summary>
    Task<Product> UpdateAsync(Product updated, IReadOnlyList<AuditEntry> audit, CancellationToken ct);

    Task<bool> CreateIfAbsentAsync(Product product, CancellationToken ct);

    /// <summary>Newest first.</summary>
    Task<Slice<AuditEntry>> GetAuditAsync(string productId, KeysetCursor? after, int pageSize, CancellationToken ct);
}
