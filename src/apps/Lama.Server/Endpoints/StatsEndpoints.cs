using Lama.Contracts;
using Lama.Server.Data;
using Lama.Server.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Endpoints;

/// <summary>
/// Statistiques publiques agrégées pour la page d'accueil : joueurs, parties, langues.
/// </summary>
public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/stats", async (
            LamaDbContext db,
            GameHubState state,
            ILanguageProviderRegistry languages,
            CancellationToken cancellationToken) =>
        {
            var activePlayers = 0;
            var gamesPlayed = 0;

            try
            {
                if (await db.Database.CanConnectAsync(cancellationToken))
                {
                    activePlayers = await db.Players.CountAsync(cancellationToken);
                    gamesPlayed = await db.CompletedGames.CountAsync(cancellationToken);
                }
            }
            catch
            {
                // Base indisponible → on retombe sur les compteurs mémoire uniquement.
            }

            var languageCount = languages.SupportedLanguages.Count;
            var activeGames = state.ListGames().Count(g => !g.IsClosed);

            // gamesPlayed = parties terminées en DB uniquement (pas les parties en cours)
            return Results.Ok(new
            {
                activePlayers,
                gamesPlayed,
                activeGames,
                languages = languageCount
            });
        })
        .WithName("PublicStats")
        .WithDescription("Compteurs publics : joueurs inscrits, parties jouées, langues disponibles.")
        .Produces(StatusCodes.Status200OK);

        return app;
    }
}
