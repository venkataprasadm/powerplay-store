using IplStore.Application.Paging;
using IplStore.Domain.Orders;

namespace IplStore.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetAsync(string orderId, CancellationToken ct);

    /// <summary>Newest first (CreatedUtc descending, then Id descending).</summary>
    Task<Slice<Order>> ListAsync(OrderQuery query, CancellationToken ct);
}
