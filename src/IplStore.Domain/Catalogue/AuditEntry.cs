namespace IplStore.Domain.Catalogue;

/// <summary>One recorded change to a product's price or stock: who, when, old and new value, and why.</summary>
public sealed record AuditEntry(
    string Id,
    string ProductId,
    AuditField Field,
    decimal OldValue,
    decimal NewValue,
    string Reason,
    string Actor,
    DateTimeOffset AtUtc);
