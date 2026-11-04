using IplStore.Domain.Catalogue;

namespace IplStore.Application.Contracts;

public sealed record AuditEntryDto(
    string Id,
    string ProductId,
    AuditField Field,
    decimal OldValue,
    decimal NewValue,
    string Reason,
    string Actor,
    DateTimeOffset AtUtc)
{
    public static AuditEntryDto From(AuditEntry entry)
    {
        return new AuditEntryDto(entry.Id, entry.ProductId, entry.Field, entry.OldValue, entry.NewValue, entry.Reason, entry.Actor, entry.AtUtc);
    }
}
