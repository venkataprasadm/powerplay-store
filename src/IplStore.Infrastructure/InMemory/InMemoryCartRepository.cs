using IplStore.Application.Abstractions;
using IplStore.Domain.Carts;
using IplStore.Domain.Exceptions;

namespace IplStore.Infrastructure.InMemory;

/// <summary>In-memory carts with the same ETag rules as the Cosmos repository.</summary>
public sealed class InMemoryCartRepository : ICartRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Cart> _carts = new();

    public async Task<Cart?> GetAsync(string userId, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            return _carts.GetValueOrDefault(userId);
        }
    }

    public async Task<Cart> SaveAsync(Cart cart, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            var exists = _carts.TryGetValue(cart.UserId, out var current);

            // A cart without an ETag is new and must not exist yet. Otherwise its ETag must match the stored one.
            var isNew = cart.ETag is null;
            var conflict = isNew ? exists : !exists || current!.ETag != cart.ETag;
            if (conflict)
            {
                throw new ConcurrencyException("Cart changed.");
            }

            var saved = cart with { ETag = InMemoryStore.NewETag() };
            _carts[cart.UserId] = saved;
            return saved;
        }
    }

    public async Task DeleteAsync(string userId, CancellationToken ct)
    {
        await Task.Yield();
        lock (_gate)
        {
            _carts.Remove(userId);
        }
    }
}
