using System.Net.Http.Json;
using System.Text.Json;

namespace IplStore.Tests.Support;

public static class HttpExtensions
{
    public static async Task<JsonElement> Json(this HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static HttpRequestMessage Req(
        HttpMethod method, string url, object? body = null, string? idempotencyKey = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    public static Task<HttpResponseMessage> AddToCart(this HttpClient client, string productId, int quantity, string? idempotencyKey = null)
    {
        var request = Req(HttpMethod.Post, "/api/v1/cart/items", new { productId, quantity }, idempotencyKey ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }
}
