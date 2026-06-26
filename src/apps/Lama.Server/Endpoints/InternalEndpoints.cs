using Lama.Server.Contracts.Api;
using Lama.Server.Runtime;

namespace Lama.Server.Endpoints;

public static class InternalEndpoints
{
    public static IEndpointRouteBuilder MapInternalEndpoints(this IEndpointRouteBuilder app, bool allowShutdown)
    {
        app.MapPost("/internal/shutdown", (IHostApplicationLifetime lifetime) =>
        {
            if (!allowShutdown)
                return Results.NotFound();

            lifetime.StopApplication();
            return Results.Ok(new
            {
                status = "stopping",
                utcNow = DateTimeOffset.UtcNow
            });
        });

        app.MapPost("/internal/games/clear", (GameHubState state) =>
        {
            if (!allowShutdown)
                return Results.NotFound();

            var cleared = state.ClearAll();
            return Results.Ok(new { cleared, utcNow = DateTimeOffset.UtcNow });
        });

        app.MapPost("/internal/games/{gameId}/close", (string gameId, GameHubState state) =>
        {
            if (!allowShutdown)
                return Results.NotFound();

            if (!state.TryGet(gameId, out var game))
                return Results.NotFound(new { error = $"Game '{gameId}' not found in memory." });

            lock (game)
            {
                game.IsClosed  = true;
                game.EndReason = "admin_reset";
            }

            state.Publish(gameId, new ServerEvent("game.ended", new
            {
                gameId,
                endedAt = DateTimeOffset.UtcNow,
                reason  = "admin_reset"
            }));

            return Results.Ok(new { gameId, closed = true, utcNow = DateTimeOffset.UtcNow });
        });

        return app;
    }
}

