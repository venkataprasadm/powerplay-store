using IplStore.Application.Paging;
using IplStore.Domain.Catalogue;

namespace IplStore.Application.Abstractions;

/// <summary>Product search filters. Null filters are ignored; the rest combine with AND.</summary>
public sealed record ProductQuery(string? Text, ProductType? Type, string? FranchiseId, int PageSize, KeysetCursor? After);
