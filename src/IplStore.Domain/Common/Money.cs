namespace IplStore.Domain.Common;

/// <summary>Rupee amounts are decimals (never double), tax inclusive, with at most two decimal places.</summary>
public static class Money
{
    public const decimal MaxPrice = 10_000_000m;

    public static bool IsValidPrice(decimal value)
    {
        return value > 0
            && value <= MaxPrice
            && decimal.Round(value, 2) == value;
    }

    public static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.ToEven);
    }
}
