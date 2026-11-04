using Microsoft.Extensions.Options;

namespace IplStore.Application.Shipping;

/// <summary>A flat fee below a threshold, free at or above it. An empty order costs nothing to ship.</summary>
public sealed class ThresholdShippingPolicy : IShippingPolicy
{
    private readonly ShippingOptions _options;

    public ThresholdShippingPolicy(IOptions<ShippingOptions> options)
    {
        _options = options.Value;

        if (_options.FlatFee < 0 || decimal.Round(_options.FlatFee, 2) != _options.FlatFee)
        {
            throw new InvalidOperationException("Shipping:FlatFee must be zero or a positive rupee amount with at most two decimals.");
        }

        if (_options.FreeFrom < 0)
        {
            throw new InvalidOperationException("Shipping:FreeFrom must not be negative.");
        }
    }

    public decimal Calculate(decimal subtotal)
    {
        if (subtotal <= 0 || _options.FlatFee == 0 || subtotal >= _options.FreeFrom)
        {
            return 0m;
        }

        return _options.FlatFee;
    }
}
