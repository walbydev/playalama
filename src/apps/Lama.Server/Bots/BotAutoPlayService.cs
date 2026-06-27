using Lama.Contracts;
using Lama.Server.Contracts.Api;
using Lama.Server.Services;

namespace Lama.Server.Bots;

/// <summary>
/// Orchestre le tour automatique d'un joueur IA :
/// interroge le moteur de suggestions, choisit le meilleur coup
/// (ou passe selon le taux configuré), et retourne le move créé.
/// </summary>
public sealed class BotAutoPlayService(ILogger<BotAutoPlayService> logger)
{
    private static readonly Random Rng = Random.Shared;

    /// <summary>
    /// Joue un coup pour le bot et retourne le <see cref="OnlineMove"/> créé,
    /// ou <c>null</c> si le bot ne peut/doit pas jouer (partie terminée, pas son tour…).
    /// </summary>
    public async Task<(OnlineMove? Move, List<char>? NewRack)> AutoPlayAsync(
        OnlineGame game,
        BotProfile bot,
        IAISuggestionClient aiClient,
        CancellationToken ct = default)
    {
        // ── Lecture de l'état sous le verrou ────────────────────────────────
        int botIndex;
        List<char> rack;
        BoardState board;
        int turnNumber;

        lock (game)
        {
            var state = game.Engine.GetGameState();

            if (state.IsGameOver || game.IsClosed)
                return (null, null);

            if (!game.PlayerIndexById.TryGetValue(bot.BotId, out botIndex))
                return (null, null);

            if (state.CurrentPlayerIndex != botIndex)
                return (null, null);

            rack      = state.Players[botIndex].Rack.ToList();
            board     = state.Board;
            turnNumber = state.TurnNumber;
        }

        // ── Demande de suggestions au moteur IA ─────────────────────────────
        var isFirstMove = !HasAnyTile(board);

        var suggestions = await aiClient.SuggestAsync(
            rack, board, isFirstMove,
            topPerCategory: bot.BeamWidth,
            timeoutSeconds: 5,
            ct);

        // Sélection du coup selon le profil du bot (potentiellement sous-optimal).
        var chosen = SelectSuggestion(suggestions, bot);

        // ── Jeu du coup (sous le verrou) ─────────────────────────────────────
        lock (game)
        {
            var state = game.Engine.GetGameState();

            // Re-vérification : le tour a pu changer pendant l'appel async
            if (state.IsGameOver || game.IsClosed)
                return (null, null);

            if (state.CurrentPlayerIndex != botIndex)
                return (null, null);

            // Passe intentionnelle (difficulté) ou absence de suggestion
            if (chosen is null || Rng.NextDouble() < bot.PassRate)
            {
                game.Engine.PassTurn();
                var passedState = game.Engine.GetGameState();
                var passRack    = passedState.Players[botIndex].Rack.ToList();
                return (BuildMove(game, bot, "play.pass", [], 0, turnNumber), passRack);
            }

            // Reconstruction des placements (lettres nouvelles seulement, hors plateau)
            var placements = BuildPlacements(chosen, board);

            if (placements.Count == 0)
            {
                // Coup vide après exclusion des lettres déjà posées → passer
                game.Engine.PassTurn();
                var passedState = game.Engine.GetGameState();
                var passRack    = passedState.Players[botIndex].Rack.ToList();
                return (BuildMove(game, bot, "play.pass", [], 0, turnNumber), passRack);
            }

            var validation = game.Engine.ValidateMove(placements);
            if (!validation.IsValid)
            {
                logger.LogWarning("Bot {BotId} suggestion invalide ({Word}) : {Error}",
                    bot.BotId, chosen.Word, validation.ErrorMessage);

                game.Engine.PassTurn();
                var passedState = game.Engine.GetGameState();
                var passRack    = passedState.Players[botIndex].Rack.ToList();
                return (BuildMove(game, bot, "play.pass", [], 0, turnNumber), passRack);
            }

            var stateAfter = game.Engine.PlayMove(placements);
            var entry      = stateAfter.History.LastOrDefault();
            var movedRack  = stateAfter.Players[botIndex].Rack.ToList();

            var movePlacements = placements
                .Select(kv => new OnlineMovePlacement(kv.Key.Row, kv.Key.Column, char.ToUpperInvariant(kv.Value)))
                .ToList();

            return (BuildMove(game, bot, "play.move", movePlacements,
                entry?.Score ?? 0, entry?.TurnNumber ?? turnNumber), movedRack);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static OnlineMove BuildMove(
        OnlineGame game,
        BotProfile bot,
        string command,
        IReadOnlyList<OnlineMovePlacement> placements,
        int score,
        int turnNumber)
    {
        var botPlayer = game.Players.FirstOrDefault(p =>
            string.Equals(p.PlayerId, bot.BotId, StringComparison.Ordinal));

        return new OnlineMove(
            MoveId:     Guid.NewGuid().ToString("N"),
            PlayerId:   bot.BotId,
            PlayerName: botPlayer?.PlayerName ?? bot.Name,
            Command:    command,
            Payload:    null,
            PlayedAt:   DateTimeOffset.UtcNow,
            TurnNumber: turnNumber,
            Placements: placements,
            Score:      score);
    }

    /// <summary>
    /// Reconstruit le dictionnaire Position→lettre en excluant les cases déjà occupées sur le plateau.
    /// </summary>
    private static Dictionary<Position, char> BuildPlacements(AISuggestion suggestion, BoardState board)
    {
        var result = new Dictionary<Position, char>();

        for (var i = 0; i < suggestion.Word.Length; i++)
        {
            var row = suggestion.IsHorizontal ? suggestion.StartRow : suggestion.StartRow + i;
            var col = suggestion.IsHorizontal ? suggestion.StartCol + i : suggestion.StartCol;

            if (row < 0 || row >= board.Grid.GetLength(0)) continue;
            if (col < 0 || col >= board.Grid.GetLength(1)) continue;

            if (board.Grid[row, col] is null)
                result[new Position(row, col)] = char.ToUpperInvariant(suggestion.Word[i]);
        }

        return result;
    }

    private static bool HasAnyTile(BoardState board)
    {
        var rows = board.Grid.GetLength(0);
        var cols = board.Grid.GetLength(1);
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
            if (board.Grid[r, c] is not null) return true;
        return false;
    }

    private static AISuggestion? SelectSuggestion(IReadOnlyList<AISuggestion> suggestions, BotProfile bot)
    {
        if (suggestions.Count == 0)
            return null;

        var ranked = suggestions
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Length)
            .ToList();

        var windowSize = Math.Clamp(bot.CandidateWindow, 1, ranked.Count);
        var candidates = ranked.Take(windowSize).ToList();

        // Bot "humain": choisit parfois un coup moins rentable et plus court.
        if (bot.WeakMoveRate > 0 && Rng.NextDouble() < bot.WeakMoveRate)
        {
            var weakPoolSize = Math.Clamp(bot.WeakPoolSize, 1, candidates.Count);
            var weakerPool = candidates
                .OrderBy(s => s.Length)
                .ThenBy(s => s.Score)
                .Take(weakPoolSize)
                .ToList();

            return weakerPool[Rng.Next(weakerPool.Count)];
        }

        return candidates[0];
    }
}
