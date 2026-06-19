using Lama.Server.Data;
using Lama.Server.Data.Models.Rating;
using Lama.Server.Security;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Endpoints.Auth;

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Login CLI legacy: PlayerName + PlayerId optionnel (sans mot de passe).</summary>
public sealed record LoginRequest(string PlayerName, string? PlayerId = null);

/// <summary>Inscription compte Web.</summary>
public sealed record RegisterRequest(string Username, string Password, string? Email = null);

/// <summary>Connexion compte Web.</summary>
public sealed record AccountLoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    string PlayerId,
    string PlayerName,
    string? Email,
    DateTime ExpiresAt);

public sealed record AuthStatusResponse(
    bool IsAuthenticated,
    string? PlayerId,
    string? PlayerName);

// ── Endpoints ─────────────────────────────────────────────────────────────────

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(
        this WebApplication app,
        JwtTokenService tokenService)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        // CLI legacy — rétrocompatible
        group.MapPost("/login", async (LoginRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.PlayerName))
                return Results.BadRequest(new { error = "PlayerName is required" });

            var playerId = request.PlayerId ?? Guid.NewGuid().ToString("N");
            var token = tokenService.GenerateToken(playerId, request.PlayerName);

            return Results.Ok(new LoginResponse(
                Token: token,
                PlayerId: playerId,
                PlayerName: request.PlayerName,
                Email: null,
                ExpiresAt: DateTime.UtcNow.AddHours(24)));
        })
            .WithName("Login")
            .WithDescription("Login CLI: PlayerName + PlayerId optionnel, retourne JWT.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        // Compte Web — inscription
        group.MapPost("/register", async (RegisterRequest request, LamaDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 2)
                return Results.BadRequest(new { error = "Le pseudo doit contenir au moins 2 caractères." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return Results.BadRequest(new { error = "Le mot de passe doit contenir au moins 6 caractères." });

            var username = request.Username.Trim();
            if (await db.Players.AnyAsync(p => p.Username == username))
                return Results.Conflict(new { error = "Ce pseudo est déjà utilisé." });

            var player = new PlayerEntity
            {
                PlayerId = Guid.NewGuid(),
                Username = username,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                PasswordHash = PasswordHasher.Hash(request.Password),
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.Players.Add(player);
            await db.SaveChangesAsync();

            var token = tokenService.GenerateToken(player.PlayerId.ToString("N"), player.Username);
            return Results.Created($"/api/v1/players/{player.PlayerId}", new LoginResponse(
                Token: token,
                PlayerId: player.PlayerId.ToString("N"),
                PlayerName: player.Username,
                Email: player.Email,
                ExpiresAt: DateTime.UtcNow.AddHours(24)));
        })
            .WithName("Register")
            .WithDescription("Inscription compte Web.")
            .Produces<LoginResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        // Compte Web — connexion avec mot de passe
        group.MapPost("/login/account", async (AccountLoginRequest request, LamaDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Username et mot de passe requis." });

            var player = await db.Players.FirstOrDefaultAsync(p => p.Username == request.Username.Trim());

            if (player is null || player.PasswordHash is null || !PasswordHasher.Verify(request.Password, player.PasswordHash))
                return Results.Unauthorized();

            var token = tokenService.GenerateToken(player.PlayerId.ToString("N"), player.Username);
            return Results.Ok(new LoginResponse(
                Token: token,
                PlayerId: player.PlayerId.ToString("N"),
                PlayerName: player.Username,
                Email: player.Email,
                ExpiresAt: DateTime.UtcNow.AddHours(24)));
        })
            .WithName("AccountLogin")
            .WithDescription("Connexion compte Web: username + mot de passe.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/status", (HttpContext context) =>
        {
            var playerId = context.User?.FindFirst("playerId")?.Value;
            var playerName = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            return Results.Ok(new AuthStatusResponse(
                IsAuthenticated: !string.IsNullOrEmpty(playerId),
                PlayerId: playerId,
                PlayerName: playerName));
        })
            .WithName("AuthStatus")
            .Produces<AuthStatusResponse>(StatusCodes.Status200OK);
    }
}


