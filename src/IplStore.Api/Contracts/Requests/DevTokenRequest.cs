using System.ComponentModel.DataAnnotations;

namespace IplStore.Api.Contracts.Requests;

public sealed class DevTokenRequest
{
    [Required]
    [RegularExpression("^[a-z0-9_-]{2,32}$")]
    public string? UserId { get; init; }

    [Required]
    [RegularExpression("^(shopper|admin)$")]
    public string? Role { get; init; }
}
