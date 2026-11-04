using IplStore.Domain.Carts;

namespace IplStore.Application.Abstractions;

public interface ICartRepository
{
    Task<Cart?> GetAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Creates the cart when its ETag is null, otherwise compare and swap. Throws ConcurrencyException on a conflict.
    /// Returns the saved cart with its new ETag.
    /// </summary>
    Task<Cart> SaveAsync(Cart cart, CancellationToken ct);

    Task DeleteAsync(string userId, CancellationToken ct);
}
