using Lama.Contracts;
using Lama.Contracts.Lexicon;

namespace Lama.Server.Endpoints;

/// <summary>
/// Endpoints lecture du lexique : définitions, synonymes, lien Wiktionnaire.
/// </summary>
public static class LexiconEndpoints
{
    public static IEndpointRouteBuilder MapLexiconEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/lexicon/{lang}/{word}", GetWordInfoAsync);
        return app;
    }

    private static async Task<IResult> GetWordInfoAsync(
        string lang, string word, ILexiconReader reader, ILanguageProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        var code = lang.Trim().ToLowerInvariant();
        if (!registry.IsSupported(code))
            return Results.BadRequest(new { error = $"unsupported language: '{lang}'" });

        if (string.IsNullOrWhiteSpace(word))
            return Results.BadRequest(new { error = "word is required" });

        var info = await reader.GetWordInfoAsync(code, word, cancellationToken);
        if (info is null)
            return Results.NotFound(new { word, lang = code });

        return Results.Ok(new
        {
            word = info.Lemma,
            lang = info.LanguageCode,
            wiktionaryUrl = info.WiktionaryUrl,
            definitions = info.Definitions.Select(d => new { d.SenseIndex, d.PartOfSpeech, d.Text }),
            synonyms = info.Synonyms
        });
    }
}
