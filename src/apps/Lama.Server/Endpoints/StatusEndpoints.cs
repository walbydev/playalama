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

        return app;
    }

    private static bool IsAuthorized(HttpContext ctx, IConfiguration config)
    {
        var expectedSecret = config["LAMA_ADMIN_SECRET"]
                          ?? Environment.GetEnvironmentVariable("LAMA_ADMIN_SECRET");

        if (string.IsNullOrWhiteSpace(expectedSecret))
            return false;

        ctx.Request.Headers.TryGetValue(AdminSecretHeader, out var providedSecret);
        return string.Equals(expectedSecret, providedSecret.ToString(), StringComparison.Ordinal);
    }
}
