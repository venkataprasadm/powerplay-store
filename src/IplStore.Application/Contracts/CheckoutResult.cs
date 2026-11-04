namespace IplStore.Application.Contracts;

/// <summary>The placed order, and whether it was returned for a retried request instead of created now.</summary>
public sealed record CheckoutResult(OrderDto Order, bool Replayed);
