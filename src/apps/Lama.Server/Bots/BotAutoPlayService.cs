using Lama.Contracts;
using Lama.Server.Contracts.Api;
using Lama.Server.Services;

namespace Lama.Server.Bots;

/// <summary>
/// Orchestre le tour automatique d'un joueur IA :
/// interroge le moteur de suggestions, choisit le meilleur coup
/// (ou échange/passe selon le profil configuré), et retourne le move créé.
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
            languageCode: game.Language,
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

            var bagCount = game.Engine.GetBagCount();
            var rackNow = state.Players[botIndex].Rack.ToList();

            // Aucune suggestion jouable : le bot peut privilégier un échange.
            if (chosen is null)
            {
                if (ShouldAttemptSwapWhenNoSuggestion(bot) &&
                    TrySwap(game, bot, botIndex, turnNumber, rackNow, bagCount, out var swapMove, out var swapRack))
                {
                    return (swapMove, swapRack);
                }

                game.Engine.PassTurn();
                var passedState = game.Engine.GetGameState();
                var passRack    = passedState.Players[botIndex].Rack.ToList();
                return (BuildMove(game, bot, "play.pass", [], 0, turnNumber), passRack);
            }

            // Coup faible : selon le niveau, le bot peut échanger au lieu de poser.
            if (ShouldAttemptSwapForWeakMove(chosen, bot) &&
                TrySwap(game, bot, botIndex, turnNumber, rackNow, bagCount, out var weakSwapMove, out var weakSwapRack))
            {
                return (weakSwapMove, weakSwapRack);
            }

            // Passe intentionnelle (difficulté)
            if (Rng.NextDouble() < bot.PassRate)
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
                // Coup vide après exclusion des lettres déjà posées : échange possible sinon passe.
                if (ShouldAttemptSwapWhenNoSuggestion(bot) &&
                    TrySwap(game, bot, botIndex, turnNumber, rackNow, bagCount, out var emptySwapMove, out var emptySwapRack))
                {
                    return (emptySwapMove, emptySwapRack);
                }

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

                if (ShouldAttemptSwapWhenNoSuggestion(bot) &&
                    TrySwap(game, bot, botIndex, turnNumber, rackNow, bagCount, out var invalidSwapMove, out var invalidSwapRack))
                {
                    return (invalidSwapMove, invalidSwapRack);
                }

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

        // ── Filtre "gros points" : les bots faibles écartent les coups trop rentables ──
        if (bot.BigMoveScoreThreshold > 0 && bot.BigMoveSkipRate > 0)
        {
            var modest = ranked.Where(s => s.Score < bot.BigMoveScoreThreshold).ToList();
            // On n'applique le filtre que s'il reste au moins une alternative jouable.
            if (modest.Count > 0 && Rng.NextDouble() < bot.BigMoveSkipRate)
                ranked = modest;
        }

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

    private static bool ShouldAttemptSwapWhenNoSuggestion(BotProfile bot) =>
        bot.SwapOnNoSuggestionRate > 0 && Rng.NextDouble() < bot.SwapOnNoSuggestionRate;

    private static bool ShouldAttemptSwapForWeakMove(AISuggestion suggestion, BotProfile bot) =>
        bot.SwapOnWeakMoveRate > 0
        && suggestion.Score <= bot.WeakMoveScoreThreshold
        && Rng.NextDouble() < bot.SwapOnWeakMoveRate;

    private bool TrySwap(
        OnlineGame game,
        BotProfile bot,
        int botIndex,
        int turnNumber,
        IReadOnlyList<char> rack,
        int bagCount,
        out OnlineMove? move,
        out List<char>? newRack)
    {
        move = null;
        newRack = null;

        if (bagCount <= 0 || rack.Count == 0 || bot.SwapMaxLetters <= 0)
            return false;

        var lettersToSwap = SelectLettersToSwap(rack, bagCount, bot.SwapMaxLetters);
        if (lettersToSwap.Count == 0)
            return false;

        try
        {
            game.Engine.SwapLetters(lettersToSwap);
            var swappedState = game.Engine.GetGameState();
            newRack = swappedState.Players[botIndex].Rack.ToList();
            move = BuildMove(game, bot, "play.swap", [], 0, turnNumber);
            return true;
        }
        catch (GameException ex)
        {
            logger.LogDebug(ex, "Swap impossible pour le bot {BotId}", bot.BotId);
            return false;
        }
    }

    private static List<char> SelectLettersToSwap(
        IReadOnlyList<char> rack,
        int bagCount,
        int maxLetters)
    {
        var cap = Math.Min(Math.Min(maxLetters, rack.Count), bagCount);
        if (cap <= 0)
            return [];

        var count = cap == 1 ? 1 : Rng.Next(1, cap + 1);

        return rack
            .Select(letter => new { letter, priority = GetSwapPriority(letter) })
            .Where(x => x.priority > int.MinValue)
            .OrderByDescending(x => x.priority)
            .ThenBy(_ => Rng.Next())
            .Take(count)
            .Select(x => x.letter)
            .ToList();
    }

    private static int GetSwapPriority(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        if (upper == '*')
            return int.MinValue;
        if ("JQKWXYZ".Contains(upper))
            return 100;
        if ("VFHBGMP".Contains(upper))
            return 70;
        if ("DC".Contains(upper))
            return 55;
        if ("RSTLNEAIOU".Contains(upper))
            return 20;
        return 35;
    }
}
