using Lama.AIServer.Models;
using Lama.AIServer.Services;
using Lama.Contracts;
using Lama.Domain.Engine;
using Lama.Languages.fr;

var builder = WebApplication.CreateBuilder(args);

// ── Langue configurée par variable d'environnement ───────────────────────────
var language = Environment.GetEnvironmentVariable("LAMA_AI_LANGUAGE")
            ?? builder.Configuration["LAMA_AI_LANGUAGE"]
            ?? "fr";

// ── Chargement du dictionnaire selon la langue ───────────────────────────────
IGameLanguageProvider langProvider = language.ToLowerInvariant() switch
{
    "fr" => new FrenchLanguageProvider(
        Path.Combine(AppContext.BaseDirectory, "assets", "languages", "fr")),
    _ => throw new InvalidOperationException(
        $"Langue non supportée : '{language}'. Langues disponibles : fr")
};

builder.Services.AddSingleton(langProvider);
builder.Services.AddSingleton<MoveSuggestionEngine>(_ =>
    new MoveSuggestionEngine(langProvider.GetDictionary(), langProvider.GetLetterScores()));

builder.Services.AddSingleton<SuggestionService>();

var app = builder.Build();

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
