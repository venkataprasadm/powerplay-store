using IplStore.Domain.Catalogue;
using IplStore.Domain.Common;

namespace IplStore.Infrastructure.Cosmos.Documents;

internal sealed class AuditDocument : IKeysetDocument
{
    public const string Kind = "audit";

    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Type { get; set; } = Kind;
    public string ProductId { get; set; } = "";
    public string Field { get; set; } = "";
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public string CreatedUtc { get; set; } = "";

    public static AuditDocument FromDomain(AuditEntry entry, string tenantId)
    {
        return new AuditDocument
        {
            Id = entry.Id,
            TenantId = tenantId,
            ProductId = entry.ProductId,
            Field = entry.Field.ToString(),
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Reason = entry.Reason,
            Actor = entry.Actor,
            CreatedUtc = Timestamp.Format(entry.AtUtc),
        };
    }

    public AuditEntry ToDomain()
    {
        return new AuditEntry(
            Id,
            ProductId,
            Enum.Parse<AuditField>(Field),
            OldValue,
            NewValue,
            Reason,
            Actor,
            Timestamp.Parse(CreatedUtc));
    }
}
