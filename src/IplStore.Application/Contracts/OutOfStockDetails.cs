namespace IplStore.Application.Contracts;

/// <summary>Extra members of the 409 out_of_stock and insufficient_stock problem details.</summary>
public sealed record OutOfStockDetails(IReadOnlyList<FailedLine> FailedLines);
