using Lama.Server.Security;

namespace Lama.Server.Endpoints.Auth;

/// <summary>
/// DTOs pour les endpoints d'authentification.
/// </summary>
public sealed record LoginRequest(string PlayerName, string? PlayerId = null);

public sealed record LoginResponse(
    string Token,
    string PlayerId,
    string PlayerName,
    DateTime ExpiresAt);

public sealed record AuthStatusResponse(
    bool IsAuthenticated,
    string? PlayerId,
    string? PlayerName);

/// <summary>
/// Endpoints d'authentification (login, logout, status).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(
        this WebApplication app,
        JwtTokenService tokenService)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/login", Login(tokenService))
            .WithName("Login")
            .WithDescription("Authentifie un joueur et retourne un JWT token.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/status", Status())
            .WithName("AuthStatus")
            .WithDescription("Retourne le statut d'authentification du client.")
            .Produces<AuthStatusResponse>(StatusCodes.Status200OK);
    }

    private static Func<HttpContext, LoginRequest, Task<IResult>> Login(JwtTokenService tokenService)
    {
        return async (context, request) =>
        {
            if (string.IsNullOrWhiteSpace(request.PlayerName))
                return Results.BadRequest(new { error = "PlayerName is required" });

            // Générer un PlayerId s'il n'est pas fourni
            var playerId = request.PlayerId ?? Guid.NewGuid().ToString("N");

            // Générer le token JWT
            var token = tokenService.GenerateToken(playerId, request.PlayerName);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            return Results.Ok(new LoginResponse(
                Token: token,
                PlayerId: playerId,
                PlayerName: request.PlayerName,
                ExpiresAt: expiresAt));
        };
    }

    private static Func<HttpContext, Task<IResult>> Status()
    {
        return async context =>
        {
            var playerId = context.User?.FindFirst("playerId")?.Value;
            var playerName = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var isAuthenticated = !string.IsNullOrEmpty(playerId);

            return Results.Ok(new AuthStatusResponse(
                IsAuthenticated: isAuthenticated,
                PlayerId: playerId,
                PlayerName: playerName));
        };
    }
}

