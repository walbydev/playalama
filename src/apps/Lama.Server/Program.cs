using Lama.Contracts;
using Lama.Server.Data;
using Lama.Server.Endpoints;
using Lama.Server.Endpoints.Auth;
using Lama.Server.Endpoints.Players;
using Lama.Server.Runtime;
using Lama.Server.Security;
using Lama.Server.Services;
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

// ── Client IA (Lama.AIServer) ─────────────────────────────────────────────
var aiServerUrl = Environment.GetEnvironmentVariable("LAMA_AI_SERVER_URL")
               ?? builder.Configuration["LAMA_AI_SERVER_URL"];

if (!string.IsNullOrWhiteSpace(aiServerUrl))
{
    builder.Services.AddHttpClient<IAISuggestionClient, HttpAISuggestionClient>(client =>
    {
        client.BaseAddress = new Uri(aiServerUrl);
        client.Timeout     = TimeSpan.FromSeconds(30); // garde-fou global
    });
}
else
{
    builder.Services.AddSingleton<IAISuggestionClient, NullAISuggestionClient>();
}

// ── StatusCollector (scoped — partage le DbContext de la requête) ─────────
builder.Services.AddScoped<IStatusCollector, StatusCollector>();
builder.Services.AddHttpClient("status-aiserver");

// JWT Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("LAMA_JWT_SECRET")
    ?? "this_is_a_default_development_secret_key_change_in_production_12345";

var jwtService = new JwtTokenService(jwtSecret);
builder.Services.AddSingleton(jwtService);

var app = builder.Build();

var autoMigrate = builder.Configuration.GetValue<bool?>("Database:AutoMigrate") ?? false;
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LamaDbContext>();
    db.Database.Migrate();
}

var allowShutdown = string.Equals(
    Environment.GetEnvironmentVariable("LAMA_SERVER_ALLOW_SHUTDOWN"),
    "true",
    StringComparison.OrdinalIgnoreCase);

// Middleware
app.UseJwtMiddleware(jwtService);

app.MapHealthEndpoints();
app.MapInternalEndpoints(allowShutdown);
app.MapStatusEndpoints();

// Auth endpoints
app.MapAuthEndpoints(jwtService);

// Player profile endpoints
app.MapPlayerEndpoints();

var api = app.MapGroup("/api/v1");
api.MapGamesReadEndpoints();
api.MapGamesCommandEndpoints();

app.Run();
