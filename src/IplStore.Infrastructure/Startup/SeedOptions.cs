namespace IplStore.Infrastructure.Startup;

/// <summary>Bound from the <c>Seed</c> configuration section.</summary>
public sealed class SeedOptions
{
    /// <summary>Insert the demo catalogue at startup when the products are missing.</summary>
    public bool Enabled { get; set; } = true;
}
