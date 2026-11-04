using IplStore.Application.Abstractions;
using IplStore.Application.Paging;
using IplStore.Domain.Common;
using IplStore.Infrastructure.Cosmos.Documents;

namespace IplStore.Infrastructure.Cosmos;

/// <summary>
/// The read queries, written as typed LINQ so property names are checked by the compiler and match what the serializer writes.
/// Kept separate from the store so tests can check the SQL the SDK generates without a database.
/// </summary>
internal static class CosmosQueries
{
    public static IQueryable<ProductDocument> Products(IQueryable<ProductDocument> documents, ProductQuery query)
    {
        documents = documents.Where(document => document.Type == ProductDocument.Kind);

        if (query.Text is not null)
        {
            // Translates to CONTAINS(name, text, true), a case-insensitive match.
            var text = query.Text;
            documents = documents.Where(document => document.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Type is not null)
        {
            var productType = query.Type.Value.ToString();
            documents = documents.Where(document => document.ProductType == productType);
        }

        if (query.FranchiseId is not null)
        {
            var franchiseId = query.FranchiseId;
            documents = documents.Where(document => document.FranchiseId == franchiseId);
        }

        return documents;
    }

    public static IQueryable<OrderDocument> Orders(IQueryable<OrderDocument> documents, OrderQuery query)
    {
        var userId = query.UserId;
        documents = documents.Where(document => document.Type == OrderDocument.Kind && document.UserId == userId);

        if (query.From is not null)
        {
            // Inclusive.
            var from = Timestamp.Format(query.From.Value);
            documents = documents.Where(document => document.CreatedUtc.CompareTo(from) >= 0);
        }

        if (query.To is not null)
        {
            // Exclusive.
            var to = Timestamp.Format(query.To.Value);
            documents = documents.Where(document => document.CreatedUtc.CompareTo(to) < 0);
        }

        return documents;
    }

    /// <summary>
    /// Keyset paging on (createdUtc, id), served by the container's composite index in either direction.
    /// Sort keys never change, so an item never moves between pages, and a deep page costs the same as the first.
    /// </summary>
    public static IQueryable<T> Page<T>(IQueryable<T> documents, KeysetCursor? after, bool newestFirst)
        where T : IKeysetDocument
    {
        if (after is not null)
        {
            var sortKey = after.SortKey;
            var id = after.Id;

            if (newestFirst)
            {
                documents = documents.Where(document =>
                    document.CreatedUtc.CompareTo(sortKey) < 0
                    || (document.CreatedUtc == sortKey && document.Id.CompareTo(id) < 0));
            }
            else
            {
                documents = documents.Where(document =>
                    document.CreatedUtc.CompareTo(sortKey) > 0
                    || (document.CreatedUtc == sortKey && document.Id.CompareTo(id) > 0));
            }
        }

        if (newestFirst)
        {
            return documents
                .OrderByDescending(document => document.CreatedUtc)
                .ThenByDescending(document => document.Id);
        }

        return documents
            .OrderBy(document => document.CreatedUtc)
            .ThenBy(document => document.Id);
    }
}
