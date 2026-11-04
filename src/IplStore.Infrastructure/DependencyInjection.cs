using IplStore.Application.Abstractions;
using IplStore.Application.Common;
using IplStore.Application.Shipping;
using IplStore.Infrastructure.Cosmos;
using IplStore.Infrastructure.InMemory;
using IplStore.Infrastructure.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IplStore.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// <c>Storage:Provider</c> is "Cosmos" or "InMemory" (the default). Everything above Infrastructure only sees the interfaces.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        services.Configure<StoreOptions>(configuration.GetSection("Store"));
        services.Configure<ShippingOptions>(configuration.GetSection("Shipping"));

        var useCosmos = string.Equals(configuration["Storage:Provider"], "Cosmos", StringComparison.OrdinalIgnoreCase);
        if (useCosmos)
        {
            AddCosmos(services, configuration);
        }
        else
        {
            AddInMemory(services);
        }

        services.AddHostedService<StoreBootstrapper>();
        return services;
    }

    private static void AddCosmos(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CosmosOptions>(configuration.GetSection("Cosmos"));
        services.AddSingleton(provider => CosmosClientFactory.Create(provider.GetRequiredService<IOptions<CosmosOptions>>().Value));

        // One store class serves three interfaces, so all three share the same instance.
        services.AddSingleton<CosmosStore>();
        services.AddSingleton<IProductRepository>(provider => provider.GetRequiredService<CosmosStore>());
        services.AddSingleton<ICheckoutStore>(provider => provider.GetRequiredService<CosmosStore>());
        services.AddSingleton<IOrderRepository>(provider => provider.GetRequiredService<CosmosStore>());
        services.AddSingleton<ICartRepository, CosmosCartRepository>();

        services.AddSingleton<CosmosHealthCheck>();
        services.AddHealthChecks().AddCheck<CosmosHealthCheck>("cosmos", tags: ["ready"]);
    }

    private static void AddInMemory(IServiceCollection services)
    {
        services.AddSingleton<InMemoryStore>();
        services.AddSingleton<IProductRepository>(provider => provider.GetRequiredService<InMemoryStore>());
        services.AddSingleton<ICheckoutStore>(provider => provider.GetRequiredService<InMemoryStore>());
        services.AddSingleton<IOrderRepository>(provider => provider.GetRequiredService<InMemoryStore>());
        services.AddSingleton<ICartRepository, InMemoryCartRepository>();
    }
}
