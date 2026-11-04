using IplStore.Domain.Orders;

namespace IplStore.Application.Abstractions;

public interface ICheckoutStore
{
    /// <summary>
    /// All or nothing: every stock change (ETag guarded), every audit entry and the order itself commit together, or none of them do.
    /// Throws ConcurrencyException if any product changed since it was read, and DuplicateOrderException if the order id exists.
    /// </summary>
    Task CommitAsync(Order order, IReadOnlyList<StockChange> changes, CancellationToken ct);
}
