using Microsoft.AspNetCore.Http.Timeouts;

namespace IplStore.Api.Hosting;

public static class RequestTimeoutExtensions
{
    /// <summary>30 seconds for every request, 20 seconds for checkout. A timed out request gets 504.</summary>
    public static IServiceCollection AddApiRequestTimeouts(this IServiceCollection services)
    {
        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(30),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            };

            options.AddPolicy(RequestTimeoutPolicies.Checkout, new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(20),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            });
        });

        return services;
    }
}
