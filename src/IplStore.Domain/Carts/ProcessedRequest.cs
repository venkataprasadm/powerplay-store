namespace IplStore.Domain.Carts;

/// <summary>
/// Remembers the outcome of a client request id, so a repeat does nothing and returns the original result.
/// <see cref="Fingerprint"/> describes what the request asked for, so the same id reused for a different change is rejected
/// instead of silently answered with an unrelated result. It is null for entries written before fingerprints existed.
/// </summary>
public sealed record ProcessedRequest(
    string RequestId,
    IReadOnlyList<CartLine> ResultLines,
    DateTimeOffset AtUtc,
    string? Fingerprint = null);
