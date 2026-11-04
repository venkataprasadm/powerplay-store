namespace IplStore.Api.Security;

/// <summary>Bound from the <c>RateLimit</c> configuration section.</summary>
public sealed class RateLimitSettings
{
    public bool Enabled { get; set; } = true;

    public RateLimitWindow Search { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    public RateLimitWindow Checkout { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}
