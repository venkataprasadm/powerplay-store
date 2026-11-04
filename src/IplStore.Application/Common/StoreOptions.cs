namespace IplStore.Application.Common;

/// <summary>Bound from the <c>Store</c> configuration section.</summary>
public sealed class StoreOptions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>How many times an optimistic concurrency conflict is retried before the caller gets a 409.</summary>
    public int MaxConcurrencyAttempts { get; set; } = 8;
}
