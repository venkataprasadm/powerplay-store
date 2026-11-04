namespace IplStore.Domain.Carts;

/// <summary>A product in the cart, with the unit price the shopper last accepted.</summary>
public sealed record CartLine(string ProductId, string ProductName, int Quantity, decimal AcceptedUnitPrice);
