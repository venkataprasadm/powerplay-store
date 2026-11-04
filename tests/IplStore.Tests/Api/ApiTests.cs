using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static IplStore.Tests.Support.HttpExtensions;

namespace IplStore.Tests.Api;

public class ApiTests
{
    // A fresh store per test keeps these independent.
    private static ApiFactory Fresh(Action<Dictionary<string, string?>>? tweak = null)
    {
        var f = new ApiFactory();
        tweak?.Invoke(f.Settings);
        return f;
    }

    [Fact]
    public async Task Product_list_honours_the_v1_contract_including_the_additive_field()
    {
        using var f = Fresh();
        var res = await f.CreateClient().GetAsync("/api/v1/products?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var root = await res.Json();
        Assert.Equal(6, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(JsonValueKind.String, root.GetProperty("nextPageToken").ValueKind);
        var item = root.GetProperty("items")[0];
        // Fields that existed in the first v1 release: removing or retyping any of them would break existing clients.
        foreach (var (name, kind) in new[] { ("id", JsonValueKind.String), ("name", JsonValueKind.String), ("type", JsonValueKind.String), ("franchiseId", JsonValueKind.String),
                     ("franchiseName", JsonValueKind.String), ("description", JsonValueKind.String), ("price", JsonValueKind.Number), ("stock", JsonValueKind.Number),
                     ("inStock", JsonValueKind.True), ("etag", JsonValueKind.String) })
            Assert.Equal(kind, item.GetProperty(name).ValueKind);
        // The backwards compatible addition, optional for clients to read.
        Assert.Equal("InStock", item.GetProperty("stockStatus").GetString());
        Assert.Equal(1999.00m, item.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task Filters_combine_over_http_and_unknown_product_is_a_404_problem()
    {
        using var f = Fresh();
        var c = f.CreateClient();
        var items = (await (await c.GetAsync("/api/v1/products?type=Cap&franchise=csk&q=cs")).Json()).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("CSK Cap", items[0].GetProperty("name").GetString());

        var missing = await c.GetAsync("/api/v1/products/ghost");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType!.MediaType);
        var p = await missing.Json();
        Assert.Equal("not_found", p.GetProperty("code").GetString());
        Assert.Equal(404, p.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(p.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task Validation_failures_are_400_problems_with_field_errors()
    {
        using var f = Fresh();
        var c = f.CreateClient();
        var res = await c.GetAsync("/api/v1/products?pageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var p = await res.Json();
        Assert.Equal("validation_failed", p.GetProperty("code").GetString());
        Assert.True(p.GetProperty("errors").TryGetProperty("pageSize", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/products?type=hat")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/products?pageToken=garbage")).StatusCode);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_when_valid_and_replaced_when_not()
    {
        using var f = Fresh();
        var c = f.CreateClient();
        var ok = new HttpRequestMessage(HttpMethod.Get, "/health/live"); ok.Headers.Add("X-Correlation-Id", "trace-abc.123");
        Assert.Equal("trace-abc.123", (await c.SendAsync(ok)).Headers.GetValues("X-Correlation-Id").Single());
        var bad = new HttpRequestMessage(HttpMethod.Get, "/health/live"); bad.Headers.Add("X-Correlation-Id", "bad value\twith spaces");
        var echoed = (await c.SendAsync(bad)).Headers.GetValues("X-Correlation-Id").Single();
        Assert.DoesNotContain(' ', echoed);
        Assert.NotEqual("bad value\twith spaces", echoed);
    }

    [Fact]
    public async Task Auth_is_enforced_401_unauthenticated_403_wrong_role()
    {
        using var f = Fresh();
        var anon = f.CreateClient();
        var res = await anon.GetAsync("/api/v1/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("unauthorized", (await res.Json()).GetProperty("code").GetString());

        var shopper = await f.ClientFor("asha");
        Assert.Equal(HttpStatusCode.Forbidden, (await shopper.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/csk-cap", new { price = 1, reason = "nope" }, ifMatch: "\"x\""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await shopper.GetAsync("/api/v1/cart")).StatusCode);
    }

    [Fact]
    public async Task Tokens_signed_with_another_key_are_rejected()
    {
        using var f = Fresh();
        var c = f.CreateClient();
        var forged = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "powerplay-store", Audience = "powerplay-store-api", Claims = new Dictionary<string, object> { ["sub"] = "mallory", ["role"] = "admin" },
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('k', 40))), "HS256"),
        });
        c.DefaultRequestHeaders.Authorization = new("Bearer", forged);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/cart")).StatusCode);
    }

    [Fact]
    public async Task Dev_tokens_are_off_unless_explicitly_enabled()
    {
        using var f = Fresh(s => s["Auth:EnableDevTokens"] = "false");
        Assert.Equal(HttpStatusCode.NotFound, (await f.CreateClient().PostAsJsonAsync("/api/v1/dev/token", new { userId = "asha", role = "admin" })).StatusCode);
    }

    [Fact]
    public async Task Cart_requires_an_idempotency_key_and_validates_quantity()
    {
        using var f = Fresh();
        var c = await f.ClientFor("asha");
        var noKey = await c.SendAsync(Req(HttpMethod.Post, "/api/v1/cart/items", new { productId = "csk-cap", quantity = 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal("idempotency_key_required", (await noKey.Json()).GetProperty("code").GetString());

        var tooMany = await c.AddToCart("csk-cap", 11);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("quantity_limit", (await tooMany.Json()).GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(Req(HttpMethod.Post, "/api/v1/cart/items", "{broken", Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.AddToCart("ghost", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.AddToCart("kkr-signed-photo", 3)).StatusCode);
    }

    [Fact]
    public async Task Admin_update_needs_if_match_428_stale_412_current_200()
    {
        using var f = Fresh();
        var admin = await f.ClientFor("boss", "admin");
        var get = await admin.GetAsync("/api/v1/products/csk-cap");
        var etag = get.Headers.ETag!.Tag;

        var noHeader = await admin.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/csk-cap", new { price = 549.00m, reason = "festival" }));
        Assert.Equal((HttpStatusCode)428, noHeader.StatusCode);

        var ok = await admin.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/csk-cap", new { price = 549.00m, reason = "festival" }, ifMatch: etag));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.NotEqual(etag, ok.Headers.ETag!.Tag);
        Assert.Equal(549.00m, (await ok.Json()).GetProperty("price").GetDecimal());

        var stale = await admin.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/csk-cap", new { price = 600.00m, reason = "late" }, ifMatch: etag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("precondition_failed", (await stale.Json()).GetProperty("code").GetString());
        Assert.Equal(549.00m, (await (await admin.GetAsync("/api/v1/products/csk-cap")).Json()).GetProperty("price").GetDecimal());

        var bad = await admin.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/csk-cap", new { price = 12.345m, reason = "bad" }, ifMatch: ok.Headers.ETag!.Tag));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var audit = await (await admin.GetAsync("/api/v1/admin/products/csk-cap/audit")).Json();
        var entry = audit.GetProperty("items")[0];
        Assert.Equal(("Price", 499.00m, 549.00m, "boss"), (entry.GetProperty("field").GetString(), entry.GetProperty("oldValue").GetDecimal(), entry.GetProperty("newValue").GetDecimal(), entry.GetProperty("actor").GetString()));
    }

    [Fact]
    public async Task Full_walkthrough_over_http()
    {
        using var f = Fresh();
        var asha = await f.ClientFor("asha"); var ravi = await f.ClientFor("ravi"); var admin = await f.ClientFor("boss", "admin");

        // Ravi holds the last photo, so make the stock match the brief's scenario.
        var photo = await admin.GetAsync("/api/v1/products/kkr-signed-photo");
        await admin.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/kkr-signed-photo", new { stock = 1, reason = "one left" }, ifMatch: photo.Headers.ETag!.Tag));

        await asha.AddToCart("mi-jersey", 2);
        var afterCaps = await (await asha.AddToCart("csk-cap", 3)).Json();
        Assert.Equal(5495.00m, afterCaps.GetProperty("total").GetDecimal());
        var key = Guid.NewGuid().ToString();
        await asha.AddToCart("kkr-signed-photo", 1, key);
        var dup = await (await asha.AddToCart("kkr-signed-photo", 1, key)).Json();                         // double click
        Assert.Equal(10494.00m, dup.GetProperty("total").GetDecimal());
        Assert.Equal(1, dup.GetProperty("lines").GetArrayLength() - 2);
        Assert.Equal(0.00m, dup.GetProperty("shipping").GetDecimal());                                  // above the free threshold
        Assert.Equal(10494.00m, dup.GetProperty("grandTotal").GetDecimal());
        await ravi.AddToCart("kkr-signed-photo", 1);

        var ashaOrder = Guid.NewGuid();
        var results = await Task.WhenAll(
            Task.Run(() => asha.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{ashaOrder}", new { expectedTotal = 10494.00m }))),
            Task.Run(() => ravi.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{Guid.NewGuid()}", new { expectedTotal = 4999.00m }))));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var problem = await loser.Json();
        Assert.Equal("out_of_stock", problem.GetProperty("code").GetString());
        Assert.Equal("kkr-signed-photo", problem.GetProperty("failedLines")[0].GetProperty("productId").GetString());

        // Whoever lost keeps a cart, and the winner's cart is empty.
        var ashaWon = results[0].StatusCode == HttpStatusCode.Created;
        var (winner, loserClient) = ashaWon ? (asha, ravi) : (ravi, asha);
        Assert.Equal(0, (await (await winner.GetAsync("/api/v1/cart")).Json()).GetProperty("lines").GetArrayLength());
        Assert.True((await (await loserClient.GetAsync("/api/v1/cart")).Json()).GetProperty("lines").GetArrayLength() > 0);
        Assert.Equal(0, (await (await asha.GetAsync("/api/v1/products/kkr-signed-photo")).Json()).GetProperty("stock").GetInt32());

        if (ashaWon)
        {
            var retry = await asha.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{ashaOrder}", new { expectedTotal = 10494.00m }));
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());

            var cap = await admin.GetAsync("/api/v1/products/csk-cap");
            await admin.SendAsync(Req(HttpMethod.Patch, "/api/v1/admin/products/csk-cap", new { price = 549.00m, reason = "raise" }, ifMatch: cap.Headers.ETag!.Tag));
            var history = await (await asha.GetAsync("/api/v1/orders")).Json();
            var order = history.GetProperty("items")[0];
            Assert.Equal(10494.00m, order.GetProperty("total").GetDecimal());
            Assert.EndsWith("Z", order.GetProperty("createdUtc").GetString());
            Assert.Equal(499.00m, order.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("productId").GetString() == "csk-cap").GetProperty("unitPrice").GetDecimal());
            Assert.Equal(549.00m, (await (await asha.GetAsync("/api/v1/products/csk-cap")).Json()).GetProperty("price").GetDecimal());
            Assert.Equal(HttpStatusCode.NotFound, (await ravi.GetAsync($"/api/v1/orders/{ashaOrder}")).StatusCode);   // not readable by another user
        }
    }

    [Fact]
    public async Task Bad_order_requests_are_rejected_cleanly()
    {
        using var f = Fresh();
        var c = await f.ClientFor("asha");
        Assert.Equal(HttpStatusCode.NotFound, (await c.SendAsync(Req(HttpMethod.Put, "/api/v1/orders/not-a-guid", new { expectedTotal = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{Guid.NewGuid()}", new { }))).StatusCode);
        var empty = await c.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{Guid.NewGuid()}", new { expectedTotal = 0 }));
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        Assert.Equal("cart_empty", (await empty.Json()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/orders?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/orders?from=yesterday")).StatusCode);
    }

    [Fact]
    public async Task Search_is_rate_limited_per_client_with_retry_after()
    {
        using var f = Fresh(s => { s["RateLimit:Enabled"] = "true"; s["RateLimit:Search:PermitLimit"] = "3"; s["RateLimit:Search:WindowSeconds"] = "60"; });
        var c = f.CreateClient();
        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/products")).StatusCode);
        var limited = await c.GetAsync("/api/v1/products");
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.True(int.Parse(limited.Headers.GetValues("Retry-After").Single()) > 0);
        Assert.Equal("rate_limited", (await limited.Json()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await (await f.ClientFor("someone-else")).GetAsync("/api/v1/products")).StatusCode);   // other client, own bucket (keyed by user)
    }

    [Fact]
    public async Task Checkout_is_rate_limited_too()
    {
        using var f = Fresh(s => { s["RateLimit:Enabled"] = "true"; s["RateLimit:Checkout:PermitLimit"] = "2"; });
        var c = await f.ClientFor("asha");
        for (var i = 0; i < 2; i++) await c.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{Guid.NewGuid()}", new { expectedTotal = 0 }));
        var third = await c.SendAsync(Req(HttpMethod.Put, $"/api/v1/orders/{Guid.NewGuid()}", new { expectedTotal = 0 }));
        Assert.Equal((HttpStatusCode)429, third.StatusCode);
        Assert.True(third.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Health_endpoints_respond_and_leak_nothing()
    {
        using var f = Fresh();
        var c = f.CreateClient();
        Assert.Equal("Healthy", await (await c.GetAsync("/health/live")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/ready")).StatusCode);
    }
}
