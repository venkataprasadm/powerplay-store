using System.Text.Json.Serialization;
using IplStore.Domain.Carts;

namespace IplStore.Infrastructure.Cosmos.Documents;

/// <summary>One document per user in the carts container. The document id is the user id.</summary>
internal sealed class CartDocument
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public List<CartLineDocument> Lines { get; set; } = [];
    public List<ProcessedRequestDocument> Processed { get; set; } = [];

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    public static CartDocument FromDomain(Cart cart)
    {
        return new CartDocument
        {
            Id = cart.UserId,
            UserId = cart.UserId,
            Lines = cart.Lines.Select(CartLineDocument.FromDomain).ToList(),
            Processed = cart.Processed.Select(ProcessedRequestDocument.FromDomain).ToList(),
        };
    }

    public Cart ToDomain()
    {
        return new Cart
        {
            UserId = UserId,
            ETag = ETag,
            Lines = Lines.Select(line => line.ToDomain()).ToList(),
            Processed = Processed.Select(request => request.ToDomain()).ToList(),
        };
    }
}
