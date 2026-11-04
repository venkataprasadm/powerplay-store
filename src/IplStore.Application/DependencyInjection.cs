using IplStore.Application.Common;
using IplStore.Application.Services;
using IplStore.Application.Shipping;
using Microsoft.Extensions.DependencyInjection;

namespace IplStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<StoreOptions>();
        services.AddOptions<ShippingOptions>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<StoreMetrics>();
        services.AddSingleton<IShippingPolicy, ThresholdShippingPolicy>();

        services.AddScoped<CatalogService>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderService>();

        return services;
    }
}
