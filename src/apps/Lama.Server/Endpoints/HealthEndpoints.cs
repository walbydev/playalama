using Lama.Server.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lama.Server.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            utcNow = DateTimeOffset.UtcNow
        }));

        app.MapGet("/health/db", async (LamaDbContext db, CancellationToken cancellationToken) =>
        {
            try
            {
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Database unavailable",
                        detail: "PostgreSQL is configured but not reachable.");

                return Results.Ok(new
                {
                    status = "ok",
                    provider = db.Database.ProviderName,
                    utcNow = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Database healthcheck failed",
                    detail: ex.Message);
            }
        });

        return app;
    }
}

