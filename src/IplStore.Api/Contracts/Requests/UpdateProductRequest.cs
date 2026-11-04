using System.ComponentModel.DataAnnotations;

namespace IplStore.Api.Contracts.Requests;

/// <summary>An admin change. At least one of price and stock is required, plus a reason for the audit trail.</summary>
public sealed class UpdateProductRequest
{
    public decimal? Price { get; init; }

    public int? Stock { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string? Reason { get; init; }
}
