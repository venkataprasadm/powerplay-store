using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IplStore.Tests.Support;

/// <summary>Boots the real API in-process on the in-memory store. Each factory has a fresh store, so tests are isolated.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Jwt:SigningKey"] = "test-signing-key-test-signing-key-123456",
        ["RateLimit:Enabled"] = "false",
    };

    /// <summary>A client signed in as <paramref name="userId"/>, or an anonymous client when it is null.</summary>
    public async Task<HttpClient> ClientFor(string? userId = null, string role = "shopper")
    {
        var client = CreateClient();
        if (userId is null)
        {
            return client;
        }

        var response = await client.PostAsJsonAsync("/api/v1/dev/token", new { userId, role });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings));
    }
}
