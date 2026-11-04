using System.Net;
using IplStore.Application.Abstractions;
using IplStore.Application.Paging;
using IplStore.Domain.Catalogue;
using IplStore.Domain.Exceptions;
using IplStore.Domain.Orders;
using IplStore.Infrastructure.Cosmos.Documents;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure.Cosmos;

/// <summary>
/// Products, audit entries and orders share one container and one logical partition (the tenant). That is what lets checkout be
/// a single transactional batch: stock deductions, audit trail and order commit together, or not at all.
/// </summary>
internal sealed class CosmosStore : IProductRepository, ICheckoutStore, IOrderRepository
{
    private readonly CosmosClient _client;
    private readonly CosmosOptions _options;

    public CosmosStore(CosmosClient client, IOptions<CosmosOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    private Container Container => _client.GetContainer(_options.Database, _options.StoreContainer);

    private PartitionKey TenantPartition => new(_options.TenantId);

    // ---------- Products ----------

    public async Task<Product?> GetAsync(string id, CancellationToken ct)
    {
        var document = await ReadAsync<ProductDocument>(id, ct);
        return document?.Type == ProductDocument.Kind ? document.ToDomain() : null;
    }

    public async Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var wantedIds = ids.Distinct().ToArray();
        var query = Query<ProductDocument>()
            .Where(document => document.Type == ProductDocument.Kind && wantedIds.Contains(document.Id));

        var documents = await ToListAsync(query, ct);
        return documents.Select(document => document.ToDomain()).ToList();
    }

    public async Task<Slice<Product>> SearchAsync(ProductQuery query, CancellationToken ct)
    {
        var filtered = CosmosQueries.Products(Query<ProductDocument>(), query);

        // One extra row tells us whether another page exists.
        var page = CosmosQueries.Page(filtered, query.After, newestFirst: false).Take(query.PageSize + 1);

        var documents = await ToListAsync(page, ct);
        var products = documents.Take(query.PageSize).Select(document => document.ToDomain()).ToList();
        return new Slice<Product>(products, HasMore: documents.Count > query.PageSize);
    }

    public async Task<int> CountAsync(ProductQuery query, CancellationToken ct)
    {
        var filtered = CosmosQueries.Products(Query<ProductDocument>(), query);
        var response = await filtered.CountAsync(ct);
        return response.Resource;
    }

    public async Task<Product> UpdateAsync(Product updated, IReadOnlyList<AuditEntry> audit, CancellationToken ct)
    {
        var batch = Container.CreateTransactionalBatch(TenantPartition);
        batch.ReplaceItem(updated.Id, ProductDocument.FromDomain(updated, _options.TenantId), IfMatch(updated.ETag));
        foreach (var entry in audit)
        {
            batch.CreateItem(AuditDocument.FromDomain(entry, _options.TenantId));
        }

        using var response = await batch.ExecuteAsync(ct);
        ThrowIfFailed(response, updated.Id);

        return updated with { ETag = response[0].ETag };
    }

    public async Task<bool> CreateIfAbsentAsync(Product product, CancellationToken ct)
    {
        try
        {
            await Container.CreateItemAsync(ProductDocument.FromDomain(product, _options.TenantId), TenantPartition, cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task<Slice<AuditEntry>> GetAuditAsync(string productId, KeysetCursor? after, int pageSize, CancellationToken ct)
    {
        var filtered = Query<AuditDocument>()
            .Where(document => document.Type == AuditDocument.Kind && document.ProductId == productId);
        var page = CosmosQueries.Page(filtered, after, newestFirst: true).Take(pageSize + 1);

        var documents = await ToListAsync(page, ct);
        var entries = documents.Take(pageSize).Select(document => document.ToDomain()).ToList();
        return new Slice<AuditEntry>(entries, HasMore: documents.Count > pageSize);
    }

    // ---------- Checkout ----------

    public async Task CommitAsync(Order order, IReadOnlyList<StockChange> changes, CancellationToken ct)
    {
        var batch = Container.CreateTransactionalBatch(TenantPartition);

        foreach (var change in changes)
        {
            var product = ProductDocument.FromDomain(change.Updated, _options.TenantId);
            batch.ReplaceItem(change.Updated.Id, product, IfMatch(change.Updated.ETag));
        }

        foreach (var change in changes)
        {
            batch.CreateItem(AuditDocument.FromDomain(change.Audit, _options.TenantId));
        }

        // The document id is the order id, so a repeated order id conflicts (409) and the whole batch rolls back.
        batch.CreateItem(OrderDocument.FromDomain(order, _options.TenantId));

        using var response = await batch.ExecuteAsync(ct);
        ThrowIfFailed(response, order.Id);
    }

    // ---------- Orders ----------

    async Task<Order?> IOrderRepository.GetAsync(string orderId, CancellationToken ct)
    {
        var document = await ReadAsync<OrderDocument>(orderId, ct);
        return document?.Type == OrderDocument.Kind ? document.ToDomain() : null;
    }

    public async Task<Slice<Order>> ListAsync(OrderQuery query, CancellationToken ct)
    {
        var filtered = CosmosQueries.Orders(Query<OrderDocument>(), query);
        var page = CosmosQueries.Page(filtered, query.After, newestFirst: true).Take(query.PageSize + 1);

        var documents = await ToListAsync(page, ct);
        var orders = documents.Take(query.PageSize).Select(document => document.ToDomain()).ToList();
        return new Slice<Order>(orders, HasMore: documents.Count > query.PageSize);
    }

    // ---------- Helpers ----------

    private static TransactionalBatchItemRequestOptions IfMatch(string? etag)
    {
        return new TransactionalBatchItemRequestOptions { IfMatchEtag = etag };
    }

    /// <summary>Maps a failed batch to the exceptions the application understands. A failed batch has applied nothing.</summary>
    private static void ThrowIfFailed(TransactionalBatchResponse response, string key)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw response.StatusCode switch
        {
            HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound =>
                new ConcurrencyException($"A document in the batch for '{key}' changed."),
            HttpStatusCode.Conflict =>
                new DuplicateOrderException(key),
            _ =>
                new CosmosException(
                    response.ErrorMessage ?? "Transactional batch failed.",
                    response.StatusCode,
                    subStatusCode: 0,
                    response.ActivityId,
                    response.RequestCharge),
        };
    }

    private async Task<T?> ReadAsync<T>(string id, CancellationToken ct)
        where T : class
    {
        try
        {
            var response = await Container.ReadItemAsync<T>(id, TenantPartition, cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Typed LINQ over the tenant's partition. Every query targets one partition; nothing fans out across partitions.</summary>
    private IOrderedQueryable<T> Query<T>()
    {
        var requestOptions = new QueryRequestOptions { PartitionKey = TenantPartition };
        return Container.GetItemLinqQueryable<T>(requestOptions: requestOptions);
    }

    private static async Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct)
    {
        using var iterator = query.ToFeedIterator();

        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            results.AddRange(page);
        }

        return results;
    }
}
