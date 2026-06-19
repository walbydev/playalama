using Lama.Contracts;
using Lama.Server.Data;
using Lama.Server.Endpoints;
using Lama.Server.Runtime;
using Lama.Languages.fr;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LamaServerDb")
    ?? "Host=localhost;Port=5432;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me";

builder.Services.AddDbContext<LamaDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IGameLanguageProvider>(_ =>
{
    var basePath = Path.Combine(AppContext.BaseDirectory, "assets", "languages", "fr");
    return new FrenchLanguageProvider(basePath);
});
builder.Services.AddSingleton<GameHubState>();

var app = builder.Build();

var allowShutdown = string.Equals(
    Environment.GetEnvironmentVariable("LAMA_SERVER_ALLOW_SHUTDOWN"),
    "true",
    StringComparison.OrdinalIgnoreCase);

app.MapHealthEndpoints();
app.MapInternalEndpoints(allowShutdown);

var api = app.MapGroup("/api");
api.MapGamesReadEndpoints();
api.MapGamesCommandEndpoints();

app.Run();
