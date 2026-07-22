using Lama.Server.Data;
using Lama.Server.Data.Models.Rating;
using Lama.Server.Runtime;
using Lama.Server.Security;
using Lama.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Endpoints.Auth;

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Inscription compte Web.</summary>
public sealed record RegisterRequest(string Username, string Password, string? Email = null, string? CountryCode = null);

/// <summary>Connexion compte Web.</summary>
public sealed record AccountLoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    string PlayerId,
    string PlayerName,
    string? Email,
    bool IsAdmin,
    DateTime ExpiresAt);

public sealed record AuthStatusResponse(
    bool IsAuthenticated,
    bool IsAdmin,
    string? PlayerId,
    string? PlayerName);

// ── Endpoints ─────────────────────────────────────────────────────────────────

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(
        this WebApplication app,
        JwtTokenService tokenService)
    {
        var logger = app.Logger;
        var group  = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        // Compte Web — inscription
        group.MapPost("/register", async (RegisterRequest request, LamaDbContext db, GameHubState state, IOutboundNotifier notifier, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 2)
                return Results.BadRequest(new { error = "Le pseudo doit contenir au moins 2 caractères." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return Results.BadRequest(new { error = "Le mot de passe doit contenir au moins 6 caractères." });

            var username = request.Username.Trim();
            var countryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? null
                : request.CountryCode.Trim().ToUpperInvariant()[..Math.Min(2, request.CountryCode.Trim().Length)];

            try
            {
                if (await db.Players.AnyAsync(p => p.Username == username))
                    return Results.Conflict(new { error = "Ce pseudo est déjà utilisé." });

                var player = new PlayerEntity
                {
                    PlayerId = Guid.NewGuid(),
                    Username = username,
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                    PasswordHash = PasswordHasher.Hash(request.Password),
                    CountryCode = countryCode,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                db.Players.Add(player);
                await db.SaveChangesAsync();

                // Notifie HomeAssistant (fire-and-forget, ne bloque pas la réponse)
                var totalPlayers = await db.Players.CountAsync(cancellationToken);
                var activeGames = state.ListGames().Count(g => !g.IsClosed);
                _ = notifier.NotifyPlayerRegisteredAsync(player.Username, totalPlayers, activeGames, CancellationToken.None);

                var token = tokenService.GenerateToken(player.PlayerId.ToString("N"), player.Username);
                return Results.Created($"/api/v1/players/{player.PlayerId}", new LoginResponse(
                    Token: token,
                    PlayerId: player.PlayerId.ToString("N"),
                    PlayerName: player.Username,
                    Email: player.Email,
                    IsAdmin: player.IsAdmin,
                    ExpiresAt: DateTime.UtcNow.AddHours(24)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur lors de l'inscription du joueur");
                return Results.Json(new { error = "Service de comptes temporairement indisponible. Assurez-vous que PostgreSQL est en cours d'exécution." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
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

            try
            {
                var player = await db.Players.FirstOrDefaultAsync(p => p.Username == request.Username.Trim());

                if (player is null || player.PasswordHash is null || !PasswordHasher.Verify(request.Password, player.PasswordHash))
                    return Results.Unauthorized();

                player.LastLoginAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();

                var token = tokenService.GenerateToken(player.PlayerId.ToString("N"), player.Username);
                return Results.Ok(new LoginResponse(
                    Token: token,
                    PlayerId: player.PlayerId.ToString("N"),
                    PlayerName: player.Username,
                    Email: player.Email,
                    IsAdmin: player.IsAdmin,
                    ExpiresAt: DateTime.UtcNow.AddHours(24)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur lors de la connexion du joueur");
                return Results.Json(new { error = "Service de comptes temporairement indisponible." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .WithName("AccountLogin")
            .WithDescription("Connexion compte Web: username + mot de passe.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/status", async (HttpContext context, LamaDbContext db, CancellationToken cancellationToken) =>
        {
            var playerId = context.User?.FindFirst("playerId")?.Value;
            var playerName = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var isAuthenticated = !string.IsNullOrEmpty(playerId);

            var isAdmin = false;
            if (isAuthenticated && Guid.TryParse(playerId, out var pid))
                isAdmin = await db.Players.AnyAsync(p => p.PlayerId == pid && p.IsAdmin, cancellationToken);

            return Results.Ok(new AuthStatusResponse(
                IsAuthenticated: isAuthenticated,
                IsAdmin: isAdmin,
                PlayerId: playerId,
                PlayerName: playerName));
        })
            .WithName("AuthStatus")
            .Produces<AuthStatusResponse>(StatusCodes.Status200OK);
    }
}

