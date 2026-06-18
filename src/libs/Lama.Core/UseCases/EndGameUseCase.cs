using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : terminer la partie.
/// Marque la partie comme terminée, calcule le classement final
/// et supprime la partie du repository.
/// </summary>
public sealed class EndGameUseCase
{
    private readonly CreateGameUseCase _createUseCase;
    private readonly IPlayerRatingService? _playerRatingService;

    /// <summary>Initialise le cas d'usage.</summary>
    public EndGameUseCase(CreateGameUseCase createUseCase)
        : this(createUseCase, playerRatingService: null)
    {
    }

    /// <summary>Initialise le cas d'usage avec service de rating optionnel.</summary>
    public EndGameUseCase(
        CreateGameUseCase createUseCase,
        IPlayerRatingService? playerRatingService)
    {
        _createUseCase = createUseCase;
        _playerRatingService = playerRatingService;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public async Task<EndGameResponse> ExecuteAsync(EndGameRequest request)
    {
        var session = _createUseCase.RequireSession(request.GameId);
        var engine  = session.Engine;

        engine.EndGame();
        var finalState = engine.GetGameState();

        var scores = finalState.Players
            .OrderByDescending(p => p.Score)
            .Select(p => (p.Name, p.Score))
            .ToList()
            .AsReadOnly();

        string? winner = null;
        if (scores.Count > 0)
        {
            var topScore   = scores[0].Score;
            var topPlayers = scores.Where(s => s.Score == topScore).ToList();
            if (topPlayers.Count == 1)
                winner = topPlayers[0].Name;
        }

        // Intégration scoring global: persiste les résultats et met à jour les ratings.
        if (_playerRatingService is not null)
        {
            var playedAt = DateTimeOffset.UtcNow;
            var queue = ResolveQueue(request.GameLevel);
            var isRanked = queue != RankingQueue.CasualUnranked;

            var participants = session.PlayerIndexById
                .Select(kv =>
                {
                    var statePlayer = finalState.Players[kv.Value];
                    return new
                    {
                        PlayerId = kv.Key,
                        PlayerName = statePlayer.Name,
                        Score = statePlayer.Score
                    };
                })
                .ToList();

            var ratings = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var participant in participants)
            {
                var rating = await _playerRatingService.GetRatingAsync(participant.PlayerId);
                ratings[participant.PlayerId] = queue switch
                {
                    RankingQueue.Tournament => rating.EloTournament,
                    RankingQueue.OpenRanked => rating.EloOpen,
                    _ => rating.GlobalPrestige
                };
            }

            var sortedByScore = participants
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.PlayerId, StringComparer.Ordinal)
                .ToList();

            var rankByPlayerId = new Dictionary<string, int>(StringComparer.Ordinal);
            var currentRank = 1;
            for (var i = 0; i < sortedByScore.Count; i++)
            {
                if (i > 0 && sortedByScore[i].Score < sortedByScore[i - 1].Score)
                    currentRank = i + 1;

                rankByPlayerId[sortedByScore[i].PlayerId] = currentRank;
            }

            var results = participants
                .Select(p => new GameResult(
                    GameId: request.GameId,
                    PlayerId: p.PlayerId,
                    PlayerName: p.PlayerName,
                    Rank: rankByPlayerId[p.PlayerId],
                    IsAbandoned: false,
                    Score: p.Score,
                    OpponentIds: participants
                        .Where(o => o.PlayerId != p.PlayerId)
                        .Select(o => o.PlayerId)
                        .ToList(),
                    OpponentRatings: participants
                        .Where(o => o.PlayerId != p.PlayerId)
                        .Select(o => ratings[o.PlayerId])
                        .ToList(),
                    PlayedAt: playedAt,
                    DurationSeconds: 0,
                    Queue: queue,
                    GameLevel: request.GameLevel ?? GameLevel.Standard,
                    BoardSize: request.BoardSize,
                    RackSize: request.RackSize,
                    MinWordLength: request.MinWordLength,
                    Language: request.Language,
                    IsRanked: isRanked,
                    TournamentId: request.TournamentId))
                .ToList();

            await _playerRatingService.UpdateRatingsAsync(results);
        }

        // Supprimer la partie du repository — elle est terminée
        _createUseCase.DeleteGame(request.GameId);

        return new EndGameResponse(
            FinalState: finalState,
            Winner:     winner,
            Scores:     scores);
    }

    private static RankingQueue ResolveQueue(GameLevel? level) =>
        level switch
        {
            GameLevel.Casual => RankingQueue.CasualUnranked,
            GameLevel.Tournament => RankingQueue.Tournament,
            _ => RankingQueue.OpenRanked
        };
}
