using Lama.Contracts;
using Lama.Core.Models;
using Lama.Domain.Engine;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage: proposer des coups pour le joueur courant (stub).
/// </summary>
public sealed class SuggestMovesUseCase
{
    private readonly CreateGameUseCase _createUseCase;
    private readonly MoveSuggestionEngine _suggestionEngine;

    public SuggestMovesUseCase(CreateGameUseCase createUseCase)
        : this(createUseCase, new MoveSuggestionEngine())
    {
    }

    public SuggestMovesUseCase(CreateGameUseCase createUseCase, MoveSuggestionEngine suggestionEngine)
    {
        _createUseCase = createUseCase;
        _suggestionEngine = suggestionEngine;
    }

    /// <summary>
    /// Execute le cas d'usage de suggestion.
    /// </summary>
    public Task<SuggestMovesResponse> ExecuteAsync(SuggestMovesRequest request)
    {
        var session = _createUseCase.RequireSession(request.GameId);
        var state = session.Engine.GetGameState();

        EnsureCurrentPlayer(state, request.GameId, request.PlayerId);

        var playerIndex = _createUseCase.GetPlayerIndex(request.GameId, request.PlayerId);
        var currentPlayer = state.Players[playerIndex];

        var strategy = request.Sort switch
        {
            MoveSuggestionSort.Length => MoveSuggestionStrategy.Length,
            MoveSuggestionSort.Balanced => MoveSuggestionStrategy.Balanced,
            _ => MoveSuggestionStrategy.Score
        };

        var top = Math.Clamp(request.Top, 1, 20);
        var suggestions = _suggestionEngine.SuggestTopMoves(state, currentPlayer, top, strategy)
            .Take(top)
            .Select(MapSuggestion)
            .ToList();

        return Task.FromResult(new SuggestMovesResponse(suggestions));
    }

    private SuggestedMoveCandidate MapSuggestion(MoveSuggestion suggestion)
    {
        // Le mapping position/direction sera derive des placements dans l'implementation finale.
        return new SuggestedMoveCandidate(
            Word: suggestion.Word,
            Position: "H8",
            Direction: "H",
            Score: suggestion.Score,
            Length: suggestion.Length,
            BalancedScore: suggestion.HeuristicScore);
    }

    private void EnsureCurrentPlayer(GameState state, string gameId, string playerId)
    {
        var playerIndex = _createUseCase.GetPlayerIndex(gameId, playerId);
        if (state.CurrentPlayerIndex != playerIndex)
            throw new GameException(
                "Ce n'est pas votre tour. " +
                $"C'est au tour de {state.Players[state.CurrentPlayerIndex].Name}.");
    }
}

