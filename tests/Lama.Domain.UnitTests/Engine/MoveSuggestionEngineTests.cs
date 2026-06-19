using FluentAssertions;
using Lama.Contracts;
using Lama.Domain.Engine;

namespace Lama.Domain.UnitTests.Engine;

public sealed class MoveSuggestionEngineTests
{
    private static readonly IReadOnlyDictionary<char, int> Scores = new Dictionary<char, int>
    {
        ['A'] = 1,
        ['L'] = 1,
        ['M'] = 2,
        ['*'] = 0
    };

    [Fact]
    public void SuggestTopMoves_FirstMove_ReturnsMovesCrossingCenter()
    {
        var engine = new MoveSuggestionEngine(
            new HashSet<string> { "LA", "LAMA" },
            Scores);

        var state = CreateState(['L', 'A', 'M', 'A', 'S', 'T', 'O']);
        var player = state.Players[0];

        var suggestions = engine.SuggestTopMoves(state, player, top: 5, MoveSuggestionStrategy.Score);

        suggestions.Should().NotBeEmpty();
        suggestions.Should().OnlyContain(s => s.Placements.Keys.Contains(new Position(7, 7)));
    }

    [Fact]
    public void SuggestTopMoves_LengthStrategy_SortsByLengthFirst()
    {
        var engine = new MoveSuggestionEngine(
            new HashSet<string> { "LA", "LAMA" },
            Scores);

        var state = CreateState(['L', 'A', 'M', 'A', 'S', 'T', 'O']);
        var player = state.Players[0];

        var suggestions = engine.SuggestTopMoves(state, player, top: 2, MoveSuggestionStrategy.Length);

        suggestions.Should().HaveCount(2);
        suggestions[0].Length.Should().BeGreaterOrEqualTo(suggestions[1].Length);
    }

    [Fact]
    public void SuggestTopMoves_NonFirstMove_ReturnsEmptyForV1()
    {
        var engine = new MoveSuggestionEngine(
            new HashSet<string> { "LA", "LAMA" },
            Scores);

        var grid = new Tile?[15, 15];
        grid[7, 7] = new Tile('L');
        var state = CreateState(['L', 'A', 'M', 'A', 'S', 'T', 'O'], new BoardState(grid));

        var suggestions = engine.SuggestTopMoves(state, state.Players[0], top: 5, MoveSuggestionStrategy.Score);

        suggestions.Should().BeEmpty();
    }

    private static GameState CreateState(List<char> rack, BoardState? board = null)
    {
        return new GameState
        {
            Board = board ?? new BoardState(),
            Players = [new Player("Alice", 0, rack)],
            CurrentPlayerIndex = 0,
            TurnNumber = 1,
            IsGameOver = false,
            History = []
        };
    }
}

