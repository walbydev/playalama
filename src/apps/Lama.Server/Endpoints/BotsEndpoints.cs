using Lama.Server.Bots;
using Lama.Server.Security;

namespace Lama.Server.Endpoints;

/// <summary>
/// Endpoints publics pour le catalogue des joueurs IA.
/// </summary>
public static class BotsEndpoints
{
    public static IEndpointRouteBuilder MapBotsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/bots", ListBots)
            .WithName("ListBots")
            .Produces<dynamic>(StatusCodes.Status200OK);

        app.MapGet("/bots/{botId}", GetBot)
            .WithName("GetBot")
            .Produces<dynamic>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult ListBots() =>
        Results.Ok(new
        {
            bots = BotCatalog.All.Select(MapBot)
        });

    private static IResult GetBot(string botId)
    {
        var bot = BotCatalog.Find(botId);
        return bot is null
            ? Results.NotFound(new { error = $"bot not found: '{botId}'" })
            : Results.Ok(MapBot(bot));
    }

    private static object MapBot(BotProfile bot) => new
    {
        botId      = bot.BotId,
        name       = bot.Name,
        level      = bot.Level,
        initialElo = bot.InitialElo
    };
}
