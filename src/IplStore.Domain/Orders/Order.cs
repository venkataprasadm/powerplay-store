using IplStore.Domain.Common;

namespace IplStore.Domain.Orders;

public sealed record Order
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public OrderStatus Status { get; init; } = OrderStatus.Confirmed;
    public DateTimeOffset CreatedUtc { get; init; }
    public required IReadOnlyList<OrderLine> Lines { get; init; }
    public decimal Shipping { get; init; }

    public decimal Subtotal => Money.Round(Lines.Sum(line => line.LineTotal));

    public decimal Total => Subtotal + Shipping;
}
