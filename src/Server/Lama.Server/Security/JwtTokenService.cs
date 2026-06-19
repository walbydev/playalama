using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Lama.Server.Security;

/// <summary>
/// Service pour générer et valider les tokens JWT.
/// </summary>
public sealed class JwtTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _expirationTime;

    public JwtTokenService(
        string secretKey,
        string issuer = "lama-server",
        string audience = "lama-cli",
        TimeSpan? expirationTime = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
            throw new ArgumentException("Secret key must be at least 32 characters", nameof(secretKey));

        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
        _expirationTime = expirationTime ?? TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Génère un JWT pour un utilisateur (identifié par playerId + playerName).
    /// </summary>
    public string GenerateToken(string playerId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("PlayerId is required", nameof(playerId));
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("PlayerName is required", nameof(playerName));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, playerId),
            new(ClaimTypes.Name, playerName),
            new("playerId", playerId)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_expirationTime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Valide un JWT et extrait les claims.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var tokenHandler = new JwtSecurityTokenHandler();

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrait le PlayerId d'un token valide.
    /// </summary>
    public string? ExtractPlayerId(string token)
    {
        var principal = ValidateToken(token);
        return principal?.FindFirst("playerId")?.Value;
    }

    /// <summary>
    /// Extrait le PlayerName d'un token valide.
    /// </summary>
    public string? ExtractPlayerName(string token)
    {
        var principal = ValidateToken(token);
        return principal?.FindFirst(ClaimTypes.Name)?.Value;
    }
}

