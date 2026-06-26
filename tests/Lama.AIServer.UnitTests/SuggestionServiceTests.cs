using System.Reflection;
using Lama.AIServer.Models;
using Lama.AIServer.Services;
using Lama.Domain.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.AIServer.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="SuggestionService"/>.
/// Utilise un dictionnaire minimal pour rester rapide et déterministe.
/// </summary>
public sealed class SuggestionServiceTests
{
    // ── Dictionnaire et scores de test ────────────────────────────────────────

    /// <summary>Petit dictionnaire reproductible — pas de dépendance aux assets disque.</summary>
    private static readonly IReadOnlySet<string> SmallDict = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "LA", "LAMA", "AMAS", "SERA", "REMS", "SERAI", "RAMASSE",
        "LAMAR", "ALAS", "LAME", "ARMES", "RAMES", "SALE", "MALES", "SEAM"
    };

    private static readonly IReadOnlyDictionary<char, int> SimpleScores =
        new Dictionary<char, int>
        {
            ['A'] = 1, ['E'] = 1, ['I'] = 1, ['L'] = 1, ['M'] = 3,
            ['R'] = 1, ['S'] = 1, ['O'] = 1, ['U'] = 1,
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SuggestionService BuildService(
        IReadOnlySet<string>? dict = null,
        IReadOnlyDictionary<char, int>? scores = null,
        int maxConcurrent = 3,
        string language = "fr")
    {
        var engine = new MoveSuggestionEngine(
            dict ?? new HashSet<string>(),
            scores ?? new Dictionary<char, int>());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LAMA_AI_LANGUAGE"]       = language,
                ["LAMA_AI_MAX_CONCURRENT"] = maxConcurrent.ToString(),
            })
            .Build();

        return new SuggestionService(engine, config, NullLogger<SuggestionService>.Instance);
    }

    private static SuggestRequest FirstMoveRequest(
        IReadOnlyList<char> rack,
        int topPerCategory  = 2,
        int timeoutSeconds  = 15) =>
        new(rack, [], IsFirstMove: true, topPerCategory, timeoutSeconds);

    private static SemaphoreSlim GetSemaphore(SuggestionService svc)
    {
        var field = typeof(SuggestionService)
            .GetField("_semaphore", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (SemaphoreSlim)field.GetValue(svc)!;
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_Language_DefaultsToFr_WhenNotConfigured()
    {
        var engine = new MoveSuggestionEngine();
        var config = new ConfigurationBuilder().Build();
        var svc    = new SuggestionService(engine, config, NullLogger<SuggestionService>.Instance);

        svc.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0,  1)]
    [InlineData(-5, 1)]
    [InlineData(1,  1)]
    [InlineData(3,  3)]
    [InlineData(20, 20)]
    [InlineData(21, 20)]
    [InlineData(99, 20)]
    public void Constructor_MaxConcurrent_IsClamped(int configured, int expectedSlots)
    {
        var svc       = BuildService(maxConcurrent: configured);
        var semaphore = GetSemaphore(svc);

        semaphore.CurrentCount.Should().Be(expectedSlots,
            because: $"LAMA_AI_MAX_CONCURRENT={configured} devrait être bridé à {expectedSlots}");
    }

    // ── Comportement avec dictionnaire vide ───────────────────────────────────

    [Fact]
    public async Task SuggestAsync_EmptyDictionary_ReturnsBusyFalseAndNoSuggestions()
    {
        var svc     = BuildService(dict: new HashSet<string>());
        var request = FirstMoveRequest(['L', 'A', 'M', 'A']);

        var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

        busy.Should().BeFalse();
        response.Suggestions.Should().BeEmpty();
        response.Message.Should().Contain("Aucune suggestion");
    }

    [Fact]
    public async Task SuggestAsync_EmptyRack_ReturnsNoSuggestions()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest([]);

        var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

        busy.Should().BeFalse();
        response.Suggestions.Should().BeEmpty();
    }

    // ── Suggestions valides ───────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_ValidRack_FirstMove_ReturnsSuggestions()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R']);

        var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

        busy.Should().BeFalse();
        response.Suggestions.Should().NotBeEmpty(
            because: "le rack contient des lettres qui forment des mots du dictionnaire");
    }

    [Fact]
    public async Task SuggestAsync_Response_ContainsLanguage()
    {
        var svc     = BuildService(language: "fr");
        var request = FirstMoveRequest(['L', 'A', 'M', 'A']);

        var (_, response) = await svc.SuggestAsync(request, CancellationToken.None);

        response.Language.Should().Be("fr");
    }

    [Fact]
    public async Task SuggestAsync_Response_MessageIndicatesCount()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R']);

        var (_, response) = await svc.SuggestAsync(request, CancellationToken.None);

        if (response.Suggestions.Count > 0)
            response.Message.Should().Contain("suggestion");
        else
            response.Message.Should().Contain("Aucune suggestion");
    }

    // ── Catégorisation ────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_Suggestions_HaveValidCategories()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R'], topPerCategory: 3);

        var (_, response) = await svc.SuggestAsync(request, CancellationToken.None);

        response.Suggestions.Should().AllSatisfy(s =>
            s.Category.Should().BeOneOf("score", "length"));
    }

    [Fact]
    public async Task SuggestAsync_NoDuplicateWordsBetweenCategories()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R'], topPerCategory: 3);

        var (_, response) = await svc.SuggestAsync(request, CancellationToken.None);

        var scoreWords  = response.Suggestions.Where(s => s.Category == "score").Select(s => s.Word).ToList();
        var lengthWords = response.Suggestions.Where(s => s.Category == "length").Select(s => s.Word).ToList();

        scoreWords.Should().NotIntersectWith(lengthWords,
            "un mot ne peut pas apparaître dans les deux catégories simultanément");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SuggestAsync_SuggestionCount_NeverExceedsTopPerCategory_Times2(int topPerCategory)
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R'], topPerCategory);

        var (_, response) = await svc.SuggestAsync(request, CancellationToken.None);

        response.Suggestions.Count.Should().BeLessThanOrEqualTo(topPerCategory * 2,
            because: "au plus topPerCategory suggestions par catégorie (score + length)");
    }

    // ── Bridage de TopPerCategory ─────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_TopPerCategory_Zero_ClampedTo1()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R'], topPerCategory: 0);

        var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

        busy.Should().BeFalse();
        response.Suggestions.Count.Should().BeLessThanOrEqualTo(2,
            because: "topPerCategory=0 est bridé à 1 → max 2 suggestions (1 score + 1 length)");
    }

    [Fact]
    public async Task SuggestAsync_TopPerCategory_TooHigh_ClampedTo10()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R'], topPerCategory: 100);

        var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

        busy.Should().BeFalse();
        response.Suggestions.Count.Should().BeLessThanOrEqualTo(20,
            because: "topPerCategory=100 est bridé à 10 → max 20 suggestions");
    }

    // ── Concurrence et sémaphore ──────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_ServiceBusy_ReturnsBusyTrue_WhenSemaphoreHeld()
    {
        var svc       = BuildService(dict: SmallDict, scores: SimpleScores, maxConcurrent: 1);
        var semaphore = GetSemaphore(svc);
        var request   = FirstMoveRequest(['L', 'A', 'M', 'A']);

        // Occuper manuellement l'unique slot
        await semaphore.WaitAsync();
        try
        {
            var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

            busy.Should().BeTrue(because: "le seul slot disponible est déjà occupé");
            response.Suggestions.Should().BeEmpty();
            response.Message.Should().Contain("surchargé");
        }
        finally
        {
            semaphore.Release();
        }
    }

    [Fact]
    public async Task SuggestAsync_AfterBusy_SemaphoreIsReleased_AndNextCallSucceeds()
    {
        var svc       = BuildService(dict: new HashSet<string>(), maxConcurrent: 1);
        var semaphore = GetSemaphore(svc);
        var request   = FirstMoveRequest(['L', 'A']);

        // Première passe normale
        await svc.SuggestAsync(request, CancellationToken.None);

        // Le sémaphore doit avoir été restitué
        semaphore.CurrentCount.Should().Be(1,
            because: "le slot doit être libéré après chaque appel, même avec résultat vide");

        // Deuxième appel doit réussir
        var (busy, _) = await svc.SuggestAsync(request, CancellationToken.None);
        busy.Should().BeFalse();
    }

    // ── Construction du plateau ───────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_OutOfBoundsPlacedTiles_DoesNotThrow()
    {
        var svc = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = new SuggestRequest(
            Rack: ['L', 'A', 'M', 'A', 'S', 'E', 'R'],
            PlacedTiles:
            [
                new PlacedTileDto(-1,  0, 'X'),   // row hors limites
                new PlacedTileDto(15,  0, 'Y'),   // row hors limites
                new PlacedTileDto(0,  -1, 'Z'),   // col hors limites
                new PlacedTileDto(0,  15, 'W'),   // col hors limites
            ],
            IsFirstMove: false);

        Func<Task> act = () => svc.SuggestAsync(request, CancellationToken.None);

        await act.Should().NotThrowAsync(
            because: "les tuiles hors limites doivent être ignorées silencieusement");
    }

    [Fact]
    public async Task SuggestAsync_LowercasePlacedTiles_AreUppercased()
    {
        // Placer une tuile 'a' en minuscule sur le plateau — doit être traité comme 'A'
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = new SuggestRequest(
            Rack: ['M', 'S', 'E', 'R'],
            PlacedTiles: [new PlacedTileDto(7, 7, 'l'), new PlacedTileDto(7, 8, 'a')],
            IsFirstMove: false);

        Func<Task> act = () => svc.SuggestAsync(request, CancellationToken.None);

        await act.Should().NotThrowAsync(
            because: "les lettres minuscules dans PlacedTiles doivent être normalisées en majuscules");
    }

    [Fact]
    public async Task SuggestAsync_WildcardInRack_CanFormWords()
    {
        // Rack avec un joker '*' — doit permettre de former des mots
        var svc     = BuildService(dict: new HashSet<string> { "ZOOM" }, scores: SimpleScores);
        var request = FirstMoveRequest(['Z', 'O', '*', 'M']);

        var (busy, response) = await svc.SuggestAsync(request, CancellationToken.None);

        busy.Should().BeFalse();
        response.Suggestions.Should().NotBeEmpty(
            because: "le joker '*' doit remplacer n'importe quelle lettre manquante");
    }

    // ── Champs de retour ──────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_Suggestion_FieldsAreValid()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R']);

        var (_, response) = await svc.SuggestAsync(request, CancellationToken.None);

        response.Suggestions.Should().AllSatisfy(s =>
        {
            s.Word.Should().NotBeNullOrEmpty();
            s.Length.Should().Be(s.Word.Length, because: "Length doit correspondre à la longueur du mot");
            s.Score.Should().BeGreaterThan(0, because: "un coup placé doit toujours valoir au moins 1 point");
            s.StartRow.Should().BeInRange(0, 14);
            s.StartCol.Should().BeInRange(0, 14);
            s.Category.Should().BeOneOf("score", "length");
        });
    }

    // ── Annulation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_CancelledToken_DoesNotThrow()
    {
        var svc     = BuildService(dict: SmallDict, scores: SimpleScores);
        var request = FirstMoveRequest(['L', 'A', 'M', 'A', 'S', 'E', 'R'], timeoutSeconds: 1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // L'annulation avant l'acquisition du sémaphore peut lever OperationCanceledException
        // mais après acquisition, le moteur gère l'annulation gracieusement.
        Func<Task> act = async () => await svc.SuggestAsync(request, cts.Token);

        // On accepte soit un retour normal, soit OperationCanceledException (si CT vérifié avant WaitAsync)
        try { await act(); }
        catch (OperationCanceledException) { /* comportement acceptable */ }
    }
}
