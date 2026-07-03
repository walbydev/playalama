using Lama.Contracts;
using Lama.Server.Bots;
using Lama.Server.Data;
using Lama.Server.Endpoints;
using Lama.Server.Endpoints.Auth;
using Lama.Server.Endpoints.Players;
using Lama.Server.Runtime;
using Lama.Server.Security;
using Lama.Server.Services;
using Lama.Contracts.Lexicon;
using Lama.Infrastructure.Lexicon;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LamaServerDb")
    ?? "Host=localhost;Port=5432;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me";

builder.Services.AddDbContext<LamaDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<ILexiconReader>(_ => new PostgresLexiconReader(connectionString,
    null));
builder.Services.AddSingleton<ILanguageProviderRegistry>(sp =>
    new LanguageProviderRegistry(sp.GetRequiredService<ILexiconReader>(), AppContext.BaseDirectory));

builder.Services.AddSingleton<IGameLanguageProvider>(sp =>
    sp.GetRequiredService<ILanguageProviderRegistry>().GetProvider("fr"));
builder.Services.AddSingleton<GameHubState>();
builder.Services.AddSingleton<BotAutoPlayService>();
builder.Services.AddSingleton<LocalAISuggestionClient>();

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

// ── Rating service (scoped — partage le DbContext) ────────────────────────
builder.Services.AddScoped<IPlayerRatingService, PlayerRatingService>();

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
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LamaDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Server: migration DB échouée au démarrage (démarrage maintenu).");
    }
}

// ── Schéma lexicon (créé si absent, idempotent) ───────────────────────────
try
{
    var lexicon = app.Services.GetRequiredService<ILexiconReader>();
    await lexicon.EnsureSchemaAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Server: impossible d'initialiser le schéma lexicon (démarrage maintenu).");
}

// ── Seeding des bots IA ───────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LamaDbContext>();

    var rootPlayer = await db.Players.FirstOrDefaultAsync(p => p.Username.ToLower() == "root");
    if (rootPlayer is null)
    {
        db.Players.Add(new Lama.Server.Data.Models.Rating.PlayerEntity
        {
            PlayerId = Guid.NewGuid(),
            Username = "root",
            PasswordHash = PasswordHasher.Hash("root"),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
    else if (rootPlayer.PasswordHash is null || !PasswordHasher.Verify("root", rootPlayer.PasswordHash))
    {
        rootPlayer.PasswordHash = PasswordHasher.Hash("root");
    }

    foreach (var bot in BotCatalog.All)
    {
        // PlayerEntity
        if (!await db.Players.AnyAsync(p => p.PlayerId == bot.BotGuid))
        {
            db.Players.Add(new Lama.Server.Data.Models.Rating.PlayerEntity
            {
                PlayerId  = bot.BotGuid,
                Username  = bot.Name,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        // PlayerRatingEntity (file "open")
        if (!await db.PlayerRatings.AnyAsync(r => r.PlayerId == bot.BotGuid && r.Queue == "open"))
        {
            db.PlayerRatings.Add(new Lama.Server.Data.Models.Rating.PlayerRatingEntity
            {
                PlayerId  = bot.BotGuid,
                Queue     = "open",
                EloRating = (decimal)bot.InitialElo,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    await db.SaveChangesAsync();
}
catch (Exception ex)
{
    // La DB peut être absente en dev — on dégrade sans bloquer le démarrage
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Impossible de seeder les bots IA (DB indisponible ?)");
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
app.MapAdminEndpoints();

// Auth endpoints
app.MapAuthEndpoints(jwtService);

// Player profile endpoints
app.MapPlayerEndpoints();

var api = app.MapGroup("/api/v1");
api.MapGamesReadEndpoints();
api.MapGamesCommandEndpoints();
api.MapBotsEndpoints();
api.MapLexiconEndpoints();
api.MapStatsEndpoints();

app.Run();
