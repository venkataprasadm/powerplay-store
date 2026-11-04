using IplStore.Domain.Orders;

namespace IplStore.Application.Contracts;

public sealed record OrderDto(
    string Id,
    string UserId,
    OrderStatus Status,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<OrderLineDto> Lines,
    decimal Subtotal,
    decimal Shipping,
    decimal Total)
{
    public static OrderDto From(Order order)
    {
        var lines = order.Lines
            .Select(line => new OrderLineDto(line.ProductId, line.ProductName, line.Quantity, line.UnitPrice, line.LineTotal))
            .ToList();

        return new OrderDto(order.Id, order.UserId, order.Status, order.CreatedUtc, lines, order.Subtotal, order.Shipping, order.Total);
    }
}
