using Lama.Server.Contracts.Api;
using Lama.Server.Runtime;
using Lama.Server.Services;

namespace Lama.Server.Endpoints;

public static class StatusEndpoints
{
    private const string AdminSecretHeader = "X-Admin-Secret";

    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/status", async (
            HttpContext httpContext,
            IStatusCollector collector,
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            if (!IsAuthorized(httpContext, config))
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

            var snapshot = await collector.CollectAsync(cancellationToken);
            return Results.Ok(snapshot);
        })
        .WithName("ServerStatus")
        .WithDescription("Tableau de bord — métriques serveur, parties, joueurs, DB, AIServer.")
        .Produces<ServerStatusSnapshot>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/v1/admin/games/terminate-all", (
            HttpContext httpContext,
            GameHubState state,
            IConfiguration config) =>
        {
            if (!IsAuthorized(httpContext, config))
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

            var games = state.ListGames();
            var terminated = 0;
            var endedAt = DateTimeOffset.UtcNow;

            foreach (var game in games)
            {
                lock (game)
                {
                    if (game.IsClosed || game.Engine.GetGameState().IsGameOver)
                        continue;

                    game.Engine.EndGame();
                    game.IsClosed     = true;
                    game.EndReason    = "admin_terminated";
                    game.UpdatedAt    = endedAt;
                    terminated++;
                }

                state.Publish(game.Id, new ServerEvent("game.ended", new
                {
                    gameId  = game.Id,
                    endedAt,
                    reason  = "admin_terminated",
                    scores  = Array.Empty<object>(),
                    winner  = (string?)null
                }));
            }

            return Results.Ok(new { terminated });
        })
        .WithName("AdminTerminateAllGames")
        .WithDescription("Force la clôture de toutes les parties actives en mémoire.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static bool IsAuthorized(HttpContext ctx, IConfiguration config)
    {
        var expectedSecret = config["LAMA_ADMIN_SECRET"]
                          ?? Environment.GetEnvironmentVariable("LAMA_ADMIN_SECRET");
        var adminPlayers   = config["LAMA_ADMIN_PLAYERS"]
                          ?? Environment.GetEnvironmentVariable("LAMA_ADMIN_PLAYERS");

        // Option 1 : X-Admin-Secret header (scripts, CLI)
        if (!string.IsNullOrWhiteSpace(expectedSecret))
        {
            ctx.Request.Headers.TryGetValue(AdminSecretHeader, out var provided);
            if (string.Equals(expectedSecret, provided.ToString(), StringComparison.Ordinal))
                return true;
        }

        // Option 2 : JWT Bearer
        if (ctx.IsAuthenticated())
        {
            // LAMA_ADMIN_PLAYERS est configuré → filtre par liste
            if (!string.IsNullOrWhiteSpace(adminPlayers))
            {
                if (adminPlayers.Trim() == "*") return true;
                var playerId = ctx.GetPlayerId();
                if (playerId is not null)
                {
                    var allowed = adminPlayers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return allowed.Contains(playerId, StringComparer.OrdinalIgnoreCase);
                }
                return false;
            }

            // Aucune restriction configurée → tout joueur authentifié peut accéder
            // (comportement par défaut dev/petites installations)
            return true;
        }

        return false;
    }
}
