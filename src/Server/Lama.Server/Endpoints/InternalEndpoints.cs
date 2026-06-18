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

        return app;
    }
}

