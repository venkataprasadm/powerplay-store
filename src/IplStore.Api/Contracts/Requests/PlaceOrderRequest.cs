using System.ComponentModel.DataAnnotations;

namespace IplStore.Api.Contracts.Requests;

public sealed class PlaceOrderRequest
{
    /// <summary>The cart's grand total the shopper saw. Checkout is rejected if the real total differs.</summary>
    [Required]
    public decimal? ExpectedTotal { get; init; }
}
