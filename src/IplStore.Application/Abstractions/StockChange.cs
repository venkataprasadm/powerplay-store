using IplStore.Domain.Catalogue;

namespace IplStore.Application.Abstractions;

/// <summary>A stock decrement and its audit entry. <c>Updated.ETag</c> must be the ETag of the version that was read.</summary>
public sealed record StockChange(Product Updated, AuditEntry Audit);
