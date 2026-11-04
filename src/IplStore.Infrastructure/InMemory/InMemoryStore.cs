using IplStore.Application.Abstractions;
using IplStore.Application.Paging;
using IplStore.Domain.Catalogue;
using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;
using IplStore.Domain.Orders;

namespace IplStore.Infrastructure.InMemory;

/// <summary>
/// Test double and zero-dependency dev store. It reproduces the behaviour the application relies on from Cosmos DB:
/// ETag compare and swap, all or nothing batches, create-if-absent conflicts and stable keyset ordering.
/// <c>Task.Yield</c> before each call forces real thread interleaving, so the concurrency tests are meaningful.
/// </summary>
public sealed class InMemoryStore : IProductRepository, ICheckoutStore, IOrderRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Product> _products = new();
    private readonly List<AuditEntry> _audit = [];
    private readonly Dictionary<string, Order> _orders = new();

    public static string NewETag()
    {
        return $"\"{Guid.NewGuid():N}\"";
    }

    // ---------- Products ----------

    public async Task<Product?> GetAsync(string id, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            return _products.GetValueOrDefault(id);
        }
    }

    public async Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            return ids
                .Distinct()
                .Where(_products.ContainsKey)
                .Select(id => _products[id])
                .ToList();
        }
    }

    public async Task<Slice<Product>> SearchAsync(ProductQuery query, CancellationToken ct)
    {
        await Task.Yield();

        List<Product> products;
        lock (_gate)
        {
            products = Filter(query)
                .OrderBy(product => Timestamp.Format(product.CreatedUtc), StringComparer.Ordinal)
                .ThenBy(product => product.Id, StringComparer.Ordinal)
                .ToList();
        }

        if (query.After is not null)
        {
            products = products
                .Where(product => CompareToCursor(Timestamp.Format(product.CreatedUtc), product.Id, query.After) > 0)
                .ToList();
        }

        return ToSlice(products, query.PageSize);
    }

    public async Task<int> CountAsync(ProductQuery query, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            return Filter(query).Count();
        }
    }

    public async Task<Product> UpdateAsync(Product updated, IReadOnlyList<AuditEntry> audit, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            if (!_products.TryGetValue(updated.Id, out var current) || current.ETag != updated.ETag)
            {
                throw new ConcurrencyException($"Product {updated.Id} changed.");
            }

            var saved = updated with { ETag = NewETag() };
            _products[saved.Id] = saved;
            _audit.AddRange(audit);
            return saved;
        }
    }

    public async Task<bool> CreateIfAbsentAsync(Product product, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            if (_products.ContainsKey(product.Id))
            {
                return false;
            }

            _products[product.Id] = product with { ETag = NewETag() };
            return true;
        }
    }

    public async Task<Slice<AuditEntry>> GetAuditAsync(string productId, KeysetCursor? after, int pageSize, CancellationToken ct)
    {
        await Task.Yield();

        List<AuditEntry> entries;
        lock (_gate)
        {
            entries = _audit
                .Where(entry => entry.ProductId == productId)
                .OrderByDescending(entry => Timestamp.Format(entry.AtUtc), StringComparer.Ordinal)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .ToList();
        }

        if (after is not null)
        {
            entries = entries
                .Where(entry => CompareToCursor(Timestamp.Format(entry.AtUtc), entry.Id, after) < 0)
                .ToList();
        }

        return ToSlice(entries, pageSize);
    }

    // ---------- Checkout ----------

    /// <summary>
    /// The lock stands in for a single partition transactional batch: validate everything first, then apply everything.
    /// </summary>
    public async Task CommitAsync(Order order, IReadOnlyList<StockChange> changes, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            foreach (var change in changes)
            {
                if (!_products.TryGetValue(change.Updated.Id, out var current) || current.ETag != change.Updated.ETag)
                {
                    throw new ConcurrencyException($"Product {change.Updated.Id} changed.");
                }
            }

            if (_orders.ContainsKey(order.Id))
            {
                throw new DuplicateOrderException(order.Id);
            }

            foreach (var change in changes)
            {
                _products[change.Updated.Id] = change.Updated with { ETag = NewETag() };
                _audit.Add(change.Audit);
            }

            _orders[order.Id] = order;
        }
    }

    // ---------- Orders ----------

    async Task<Order?> IOrderRepository.GetAsync(string orderId, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            return _orders.GetValueOrDefault(orderId);
        }
    }

    public async Task<Slice<Order>> ListAsync(OrderQuery query, CancellationToken ct)
    {
        await Task.Yield();

        List<Order> orders;
        lock (_gate)
        {
            orders = _orders.Values
                .Where(order => order.UserId == query.UserId)
                .Where(order => query.From is null || order.CreatedUtc >= query.From)
                .Where(order => query.To is null || order.CreatedUtc < query.To)
                .OrderByDescending(order => Timestamp.Format(order.CreatedUtc), StringComparer.Ordinal)
                .ThenByDescending(order => order.Id, StringComparer.Ordinal)
                .ToList();
        }

        if (query.After is not null)
        {
            orders = orders
                .Where(order => CompareToCursor(Timestamp.Format(order.CreatedUtc), order.Id, query.After) < 0)
                .ToList();
        }

        return ToSlice(orders, query.PageSize);
    }

    // ---------- Helpers ----------

    private IEnumerable<Product> Filter(ProductQuery query)
    {
        return _products.Values
            .Where(product => query.Text is null || product.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase))
            .Where(product => query.Type is null || product.Type == query.Type)
            .Where(product => query.FranchiseId is null || product.FranchiseId == query.FranchiseId);
    }

    private static Slice<T> ToSlice<T>(List<T> items, int pageSize)
    {
        return new Slice<T>(items.Take(pageSize).ToList(), HasMore: items.Count > pageSize);
    }

    /// <summary>Orders by sort key, then id, the same way the Cosmos composite index does.</summary>
    private static int CompareToCursor(string sortKey, string id, KeysetCursor cursor)
    {
        var bySortKey = string.CompareOrdinal(sortKey, cursor.SortKey);
        return bySortKey != 0 ? bySortKey : string.CompareOrdinal(id, cursor.Id);
    }
}
