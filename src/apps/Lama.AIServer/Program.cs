using Lama.AIServer.Models;
using Lama.AIServer.Services;
using Lama.Contracts;
using Lama.Contracts.Lexicon;
using Lama.Domain.Engine;
using Lama.Infrastructure.Lexicon;

var builder = WebApplication.CreateBuilder(args);

// ── Langue configurée par variable d'environnement ───────────────────────────
var language = (Environment.GetEnvironmentVariable("LAMA_AI_LANGUAGE")
            ?? builder.Configuration["LAMA_AI_LANGUAGE"]
            ?? "fr").Trim().ToLowerInvariant();
var connectionString = Environment.GetEnvironmentVariable("LAMA_LEXICON_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("LamaServerDb")
    ?? "Host=localhost;Port=5432;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me";

builder.Services.AddSingleton<ILexiconReader>(_ => new PostgresLexiconReader(connectionString));
builder.Services.AddSingleton<ILanguageProviderRegistry>(sp =>
    new LanguageProviderRegistry(sp.GetRequiredService<ILexiconReader>(), AppContext.BaseDirectory));
builder.Services.AddSingleton<IGameLanguageProvider>(sp =>
{
    var registry = sp.GetRequiredService<ILanguageProviderRegistry>();
    if (!registry.IsSupported(language))
        throw new InvalidOperationException($"Langue non supportée : '{language}'.");
    return registry.GetProvider(language);
});
builder.Services.AddSingleton<MoveSuggestionEngine>(sp =>
{
    var langProvider = sp.GetRequiredService<IGameLanguageProvider>();
    return new MoveSuggestionEngine(langProvider.GetDictionary(), langProvider.GetLetterScores());
});

builder.Services.AddSingleton<SuggestionService>();

var app = builder.Build();

var lexicon = app.Services.GetRequiredService<ILexiconReader>();
await lexicon.EnsureSchemaAsync();

// ── Préchargement du dictionnaire au démarrage ───────────────────────────────
_ = app.Services.GetRequiredService<MoveSuggestionEngine>();
app.Logger.LogInformation("Lama.AIServer démarré — langue : {Language}", language);

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new
{
    status   = "healthy",
    language,
    service  = "Lama.AIServer"
}));

app.MapPost("/suggest", async (SuggestRequest request, SuggestionService svc, CancellationToken ct) =>
{
    var (busy, response) = await svc.SuggestAsync(request, ct);
    return busy
        ? Results.Json(response, statusCode: 503)
        : Results.Ok(response);
});

app.Run();

// Rend la classe Program accessible depuis les tests d'intégration (WebApplicationFactory).
public partial class Program { }
