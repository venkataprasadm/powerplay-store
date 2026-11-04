using IplStore.Api.Contracts.Requests;
using IplStore.Api.Contracts.Responses;
using IplStore.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IplStore.Api.Controllers;

/// <summary>
/// A stand-in identity provider so the demo runs without one. It returns 404 unless Auth:EnableDevTokens is true,
/// which only appsettings.Development.json and the compose file set. In production, tokens come from a real identity provider.
/// </summary>
[Route("api/v1/dev/token")]
public sealed class DevTokenController : ApiControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly JwtSettings _jwtSettings;
    private readonly TimeProvider _clock;

    public DevTokenController(IConfiguration configuration, IOptions<JwtSettings> jwtSettings, TimeProvider clock)
    {
        _configuration = configuration;
        _jwtSettings = jwtSettings.Value;
        _clock = clock;
    }

    [HttpPost]
    public ActionResult<DevTokenResponse> Issue([FromBody] DevTokenRequest request)
    {
        if (!_configuration.GetValue<bool>("Auth:EnableDevTokens"))
        {
            return NotFound();
        }

        var userId = request.UserId!;
        var role = request.Role!;
        var expiresAt = _clock.GetUtcNow().AddMinutes(_jwtSettings.AccessTokenMinutes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId,
                ["role"] = role,
            },
            SigningCredentials = new SigningCredentials(_jwtSettings.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return Ok(new DevTokenResponse(token, userId, role, expiresAt));
    }
}
