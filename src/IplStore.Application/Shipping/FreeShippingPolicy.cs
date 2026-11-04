namespace IplStore.Application.Shipping;

/// <summary>Never charges shipping.</summary>
public sealed class FreeShippingPolicy : IShippingPolicy
{
    public decimal Calculate(decimal subtotal)
    {
        return 0m;
    }
}
