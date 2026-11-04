namespace IplStore.Application.Shipping;

/// <summary>Bound from the <c>Shipping</c> configuration section. A flat fee of 0 means shipping is always free.</summary>
public sealed class ShippingOptions
{
    public decimal FlatFee { get; set; }

    public decimal FreeFrom { get; set; }
}
