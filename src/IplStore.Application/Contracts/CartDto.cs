namespace IplStore.Application.Contracts;

/// <remarks>
/// <c>Total</c> is the item total (its v1 meaning, unchanged). <c>Shipping</c> and <c>GrandTotal</c> were added later as additive fields:
/// <c>GrandTotal</c> = <c>Total</c> + <c>Shipping</c> is what checkout charges and what a client sends as <c>expectedTotal</c>.
/// </remarks>
public sealed record CartDto(
    IReadOnlyList<CartLineDto> Lines,
    int ItemCount,
    decimal Total,
    decimal AcceptedTotal,
    bool RequiresAcceptance,
    bool CanCheckout,
    decimal Shipping = 0m,
    decimal GrandTotal = 0m);
