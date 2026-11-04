using System.ComponentModel.DataAnnotations;

namespace IplStore.Api.Contracts.Requests;

public sealed class AddCartItemRequest
{
    [Required]
    [StringLength(64)]
    public string? ProductId { get; init; }

    public int Quantity { get; init; }
}
