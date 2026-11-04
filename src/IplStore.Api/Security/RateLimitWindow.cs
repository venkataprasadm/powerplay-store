namespace IplStore.Api.Security;

/// <summary>A fixed window: at most <see cref="PermitLimit"/> requests every <see cref="WindowSeconds"/> seconds.</summary>
public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}
