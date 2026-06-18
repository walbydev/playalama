using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play move &lt;case&gt; &lt;mot&gt; &lt;direction&gt;</c>
/// — pose un mot sur le plateau.
///
/// Exemples :
///   lama play move H8 LAMA H              (horizontal depuis H8, premier mot)
///   lama play move H8 LAMA V              (vertical depuis H8)
///   lama play move A1 ZEN H --dry-run     (simulation sans jouer)
///   lama play move H8 MAISON H            (croisement : 'I' existe déjà)
///
/// Pour indiquer un croisement (lettre déjà existante) :
/// - Posez simplement le mot complet en incluant la lettre existante
/// - Par exemple, si 'I' est déjà en J8, tapez : lama play move H8 MAISON H
/// - Le système valide que la même lettre 'I' se trouve à la position calculée
///
/// Jokers :
///   lama play move H8 lAMA H      (le 'l' minuscule = joker représentant 'L')
/// </summary>
public sealed class PlayMoveCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.move";

    private readonly PlayMoveUseCase _playMoveUseCase;
    private readonly ISessionService _sessionService;
    private readonly ILogger<PlayMoveCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayMoveCommand(
        PlayMoveUseCase  playMoveUseCase,
        ISessionService  sessionService,
        ILogger<PlayMoveCommand> logger)
    {
        _playMoveUseCase = playMoveUseCase;
        _sessionService  = sessionService;
        _logger          = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var posStr    = context.GetArgument(0);
        var word      = context.GetArgument(1);
        var dirStr    = context.GetArgument(2)?.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(posStr) ||
            string.IsNullOrWhiteSpace(word)   ||
            string.IsNullOrWhiteSpace(dirStr))
        {
            global::System.Console.Error.WriteLine(
                "[play move] Usage : lama play move <case> <mot> <direction>");
            global::System.Console.Error.WriteLine(
                "");
            global::System.Console.Error.WriteLine(
                "  Exemples :");
            global::System.Console.Error.WriteLine(
                "    lama play move H8 LAMA H           — placer LAMA horizontalement en H8");
            global::System.Console.Error.WriteLine(
                "    lama play move H8 LAMA V           — placer LAMA verticalement en H8");
            global::System.Console.Error.WriteLine(
                "");
            global::System.Console.Error.WriteLine(
                "  Croisements (mots qui partagent des lettres):");
            global::System.Console.Error.WriteLine(
                "    lama play move H8 MAISON H         — si 'I' existe en J8, placer le mot complet");
            global::System.Console.Error.WriteLine(
                "    (la lettre 'I' doit correspondre à celle déjà posée)");
            global::System.Console.Error.WriteLine(
                "");
            global::System.Console.Error.WriteLine(
                "  Jokers (lettre minuscule = joker):");
            global::System.Console.Error.WriteLine(
                "    lama play move H8 lAMA H           — 'l' minuscule = joker pour 'L'");
            return ExitCodes.InvalidArgument;
        }

        if (dirStr != "H" && dirStr != "V")
        {
            global::System.Console.Error.WriteLine(
                "[play move] Direction invalide. Utilisez H (horizontal) ou V (vertical).");
            return ExitCodes.InvalidArgument;
        }

        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine(
                "[play move] Aucune session active. Créez/rejoignez une partie d'abord.");
            return ExitCodes.GameNotFound;
        }

        // Parser la position : ex. "H8" → colonne H (index 7), ligne 8 (index 7)
        if (!TryParsePosition(posStr, out var position))
        {
            global::System.Console.Error.WriteLine(
                $"[play move] Position invalide : '{posStr}'. Exemples : H8, A1, O15");
            return ExitCodes.InvalidArgument;
        }

        // Construire le dictionnaire des lettres placées
        var letters = BuildLetterPlacements(position, word, dirStr == "H");

        // Mode simulation (--dry-run) : valider sans jouer
        var isDryRun = context.HasOption("dry-run");
        if (isDryRun)
        {
            global::System.Console.WriteLine(
                $"[dry-run] Simulation de {ToDisplayWord(word)} en {posStr} direction {dirStr}");
            global::System.Console.WriteLine("(le coup n'est pas joué)");
            return ExitCodes.Success;
        }

        try
        {
            var request  = new PlayMoveRequest(context.GameId, context.PlayerId, letters);
            var response = await _playMoveUseCase.ExecuteAsync(request);

            var wildcardCount = word.Count(char.IsLower);
            global::System.Console.WriteLine(
                $"✓ {ToDisplayWord(word)} joué en {posStr} {dirStr} — {response.Score} pts");
            if (wildcardCount > 0)
                global::System.Console.WriteLine($"  Jokers forcés: {wildcardCount}");
            global::System.Console.WriteLine(
                $"  Nouveau rack : {string.Join(" ", response.NewRack)}");
            global::System.Console.WriteLine(
                $"  Score total  : {response.GameState.Players.Find(p => p.Rack == response.NewRack || true)?.Score ?? 0}");
            global::System.Console.WriteLine(
                $"  Tour suivant : {response.GameState.Players[response.GameState.CurrentPlayerIndex].Name}");

            _logger.LogInformation("{Player} a joué {Word} en {Pos}{Dir}",
                context.PlayerName, ToDisplayWord(word), posStr, dirStr);

            // Mettre à jour la session (rack mis à jour)
            var session = _sessionService.LoadSession();
            if (session is not null)
            {
                _sessionService.SaveSession(session with { UpdatedAt = DateTimeOffset.UtcNow });
            }

            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[play move] Coup invalide : {ex.Message}");
            return ExitCodes.InvalidPlacement;
        }
    }

    private static string ToDisplayWord(string word) => word.ToUpperInvariant();

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse une position Scrabble (ex: "H8") en <see cref="Position"/> (row, col).
    /// Colonne : A=0 .. O=14, Ligne : 1=0 .. 15=14.
    /// </summary>
    private static bool TryParsePosition(string input, out Position position)
    {
        position = new Position(0, 0);
        input = input.Trim().ToUpperInvariant();

        if (input.Length < 2) return false;

        var colChar = input[0];
        if (colChar < 'A' || colChar > 'O') return false;

        if (!int.TryParse(input[1..], out var row) || row < 1 || row > 15)
            return false;

        position = new Position(row - 1, colChar - 'A');
        return true;
    }

    /// <summary>
    /// Construit le dictionnaire Position → lettre pour un mot posé sur le plateau.
    /// </summary>
    private static Dictionary<Position, char> BuildLetterPlacements(
        Position start, string word, bool isHorizontal)
    {
        var placements = new Dictionary<Position, char>();
        for (var i = 0; i < word.Length; i++)
        {
            var pos = isHorizontal
                ? new Position(start.Row, start.Column + i)
                : new Position(start.Row + i, start.Column);
            placements[pos] = word[i];
        }
        return placements;
    }
}
