namespace IplStore.Infrastructure.Cosmos;

/// <summary>Bound from the <c>Cosmos</c> configuration section.</summary>
public sealed class CosmosOptions
{
    /// <summary>A secret. Comes from environment variables, user secrets or Key Vault, never from source control.</summary>
    public string ConnectionString { get; set; } = "";

    public string Database { get; set; } = "iplstore";

    /// <summary>Products, the audit trail and orders. Partition key <c>/tenantId</c>.</summary>
    public string StoreContainer { get; set; } = "store";

    /// <summary>One document per user. Partition key <c>/userId</c>.</summary>
    public string CartContainer { get; set; } = "carts";

    /// <summary>A single logical store today. Becomes the franchise id in the multi-tenant design.</summary>
    public string TenantId { get; set; } = "ipl";

    public bool CreateIfNotExists { get; set; } = true;

    public int Throughput { get; set; } = 400;

    public int RequestTimeoutSeconds { get; set; } = 10;

    public bool AllowInsecureEmulatorCertificate { get; set; }

    public bool UseGatewayMode { get; set; }
}
