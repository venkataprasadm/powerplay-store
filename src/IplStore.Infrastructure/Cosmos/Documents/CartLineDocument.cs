using IplStore.Domain.Carts;

namespace IplStore.Infrastructure.Cosmos.Documents;

internal sealed class CartLineDocument
{
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal AcceptedUnitPrice { get; set; }

    public static CartLineDocument FromDomain(CartLine line)
    {
        return new CartLineDocument
        {
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            Quantity = line.Quantity,
            AcceptedUnitPrice = line.AcceptedUnitPrice,
        };
    }

    public CartLine ToDomain()
    {
        return new CartLine(ProductId, ProductName, Quantity, AcceptedUnitPrice);
    }
}
