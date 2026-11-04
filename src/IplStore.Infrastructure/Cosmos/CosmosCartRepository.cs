using System.Net;
using IplStore.Application.Abstractions;
using IplStore.Domain.Carts;
using IplStore.Domain.Exceptions;
using IplStore.Infrastructure.Cosmos.Documents;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure.Cosmos;

/// <summary>Carts live in their own container, partitioned by user id. They are only ever read by id.</summary>
internal sealed class CosmosCartRepository : ICartRepository
{
    private readonly CosmosClient _client;
    private readonly CosmosOptions _options;

    public CosmosCartRepository(CosmosClient client, IOptions<CosmosOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    private Container Container => _client.GetContainer(_options.Database, _options.CartContainer);

    public async Task<Cart?> GetAsync(string userId, CancellationToken ct)
    {
        try
        {
            var response = await Container.ReadItemAsync<CartDocument>(userId, new PartitionKey(userId), cancellationToken: ct);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Cart> SaveAsync(Cart cart, CancellationToken ct)
    {
        var document = CartDocument.FromDomain(cart);
        var partitionKey = new PartitionKey(cart.UserId);

        try
        {
            ItemResponse<CartDocument> response;
            if (cart.ETag is null)
            {
                response = await Container.CreateItemAsync(document, partitionKey, cancellationToken: ct);
            }
            else
            {
                var requestOptions = new ItemRequestOptions { IfMatchEtag = cart.ETag };
                response = await Container.ReplaceItemAsync(document, document.Id, partitionKey, requestOptions, ct);
            }

            return cart with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            throw new ConcurrencyException("Cart changed.");
        }
    }

    public async Task DeleteAsync(string userId, CancellationToken ct)
    {
        try
        {
            await Container.DeleteItemAsync<CartDocument>(userId, new PartitionKey(userId), cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }
}
