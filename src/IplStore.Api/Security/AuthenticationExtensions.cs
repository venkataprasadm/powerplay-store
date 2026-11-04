using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IplStore.Api.Security;

public static class AuthenticationExtensions
{
    /// <summary>JWT bearer authentication, and an Admin policy that requires the "admin" role.</summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtSettings>().BindConfiguration("Jwt");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from options at first use, so the key comes from the final configuration
        // (environment variables, secrets and test overrides all behave the same).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearerOptions, jwtOptions) => ConfigureBearer(bearerOptions, jwtOptions.Value));

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole("admin"));

        return services;
    }

    private static void ConfigureBearer(JwtBearerOptions options, JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SigningKey) || settings.SigningKey.Length < JwtSettings.MinSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be configured with at least {JwtSettings.MinSigningKeyLength} characters " +
                "(use the Jwt__SigningKey environment variable or a secret store).");
        }

        // Keep the claim names as they are in the token ("sub", "role") instead of mapping them to long URIs.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = settings.CreateSigningKey(),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };
    }
}
