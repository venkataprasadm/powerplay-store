using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace IplStore.Infrastructure.Cosmos;

public static class CosmosClientFactory
{
    public static CosmosClient Create(CosmosOptions cosmosOptions)
    {
        var clientOptions = new CosmosClientOptions
        {
            ApplicationName = "powerplay-store-api",

            // Session is the default and the right fit: cheap reads, and a client reads its own writes. Correctness never depends on it,
            // because every read-modify-write is guarded by an ETag, so a stale read can only cause a retry, never a wrong write.
            ConsistencyLevel = ConsistencyLevel.Session,

            RequestTimeout = TimeSpan.FromSeconds(cosmosOptions.RequestTimeoutSeconds),

            // The SDK retries 429 responses using the wait time the server sends back.
            MaxRetryAttemptsOnRateLimitedRequests = 5,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10),

            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            },
        };

        if (cosmosOptions.UseGatewayMode)
        {
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
        }

        if (cosmosOptions.AllowInsecureEmulatorCertificate)
        {
            // Only for the local HTTPS emulator, which uses a self-signed certificate.
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
            clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
        }

        return new CosmosClient(cosmosOptions.ConnectionString, clientOptions);
    }
}
