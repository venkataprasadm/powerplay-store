using IplStore.Domain.Common;

namespace IplStore.Domain.Orders;

/// <summary>A snapshot of what was bought. Name and unit price are copied, so later catalogue edits never change order history.</summary>
public sealed record OrderLine(string ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Money.Round(Quantity * UnitPrice);
}
