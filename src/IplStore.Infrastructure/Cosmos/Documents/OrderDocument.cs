using IplStore.Domain.Common;
using IplStore.Domain.Orders;

namespace IplStore.Infrastructure.Cosmos.Documents;

internal sealed class OrderDocument : IKeysetDocument
{
    public const string Kind = "order";

    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Type { get; set; } = Kind;
    public string UserId { get; set; } = "";
    public string Status { get; set; } = "";
    public string CreatedUtc { get; set; } = "";
    public List<OrderLineDocument> Lines { get; set; } = [];
    public decimal Shipping { get; set; }

    // The derived totals are stored too, so future reporting queries (per franchise sales reports) need no recalculation.
    // The domain recalculates them and ignores these values when reading.
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }

    public static OrderDocument FromDomain(Order order, string tenantId)
    {
        var lines = order.Lines
            .Select(line => new OrderLineDocument
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
            })
            .ToList();

        return new OrderDocument
        {
            Id = order.Id,
            TenantId = tenantId,
            UserId = order.UserId,
            Status = order.Status.ToString(),
            CreatedUtc = Timestamp.Format(order.CreatedUtc),
            Lines = lines,
            Shipping = order.Shipping,
            Subtotal = order.Subtotal,
            Total = order.Total,
        };
    }

    public Order ToDomain()
    {
        var lines = Lines
            .Select(line => new OrderLine(line.ProductId, line.ProductName, line.Quantity, line.UnitPrice))
            .ToList();

        return new Order
        {
            Id = Id,
            UserId = UserId,
            Status = Enum.Parse<OrderStatus>(Status),
            CreatedUtc = Timestamp.Parse(CreatedUtc),
            Shipping = Shipping,
            Lines = lines,
        };
    }
}
