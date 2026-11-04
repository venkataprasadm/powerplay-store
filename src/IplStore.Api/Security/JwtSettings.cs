using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace IplStore.Api.Security;

/// <summary>Bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtSettings
{
    public const int MinSigningKeyLength = 32;

    public string Issuer { get; set; } = "powerplay-store";

    public string Audience { get; set; } = "powerplay-store-api";

    /// <summary>A secret. Set it with the Jwt__SigningKey environment variable or a secret store. Never committed.</summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 60;

    public SymmetricSecurityKey CreateSigningKey()
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}
