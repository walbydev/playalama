using FluentAssertions;
using Lama.Contracts;
using Lama.Domain.Engine;
using Lama.Server.Contracts.Api;
using Lama.Server.Runtime;

namespace Lama.Server.UnitTests;

public class GameHubStateTests
{
    private static GameHubState CreateSut()
    {
        var dict = new HashSet<string> { "LA", "LAMA", "MA", "AS" };
        var scores = new Dictionary<char, int> { ['A'] = 1, ['L'] = 1, ['M'] = 2, ['S'] = 1 };
        var dist = new Dictionary<char, int> { ['A'] = 9, ['L'] = 5, ['M'] = 3, ['S'] = 6, ['*'] = 2 };
        var provider = new StubLanguageProvider(dict, scores, dist);
        return new GameHubState(provider);
    }

    private static OnlineGame CreateOnlineGame(string id, GameHubState sut)
    {
        var engine = sut.CreateEngine();
        ((GameEngine)engine).InitializeGame(["Alice", "Bob"], 0);
        return new OnlineGame(
            Id: id,
            GameLevel: GameLevel.Standard,
            BoardSize: 15,
            RackSize: 7,
            MinWordLength: 2,
            Language: "fr",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Players: new List<OnlinePlayer>
            {
                new("p1", "Alice", true),
                new("p2", "Bob", false)
            },
            PlayerIndexById: new Dictionary<string, int> { ["p1"] = 0, ["p2"] = 1 },
            Moves: new List<OnlineMove>(),
            TournamentId: null,
            Queue: RankingQueue.CasualUnranked,
            Engine: engine,
            Mode: OnlineGameMode.Multi,
            MaxPlayers: 2);
    }

    [Fact]
    public void CreateEngine_WithDefaultProvider_ReturnsGameEngine()
    {
        var sut = CreateSut();
        var engine = sut.CreateEngine();
        engine.Should().NotBeNull();
        engine.Should().BeAssignableTo<IGameEngine>();
    }

    [Fact]
    public void Create_StoresGame_AndExists()
    {
        var sut = CreateSut();
        var game = CreateOnlineGame("g1", sut);

        sut.Create(game);

        sut.Exists("g1").Should().BeTrue();
    }

    [Fact]
    public void Exists_UnknownId_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.Exists("unknown").Should().BeFalse();
    }

    [Fact]
    public void TryGet_ExistingGame_ReturnsTrueAndGame()
    {
        var sut = CreateSut();
        var game = CreateOnlineGame("g1", sut);
        sut.Create(game);

        var found = sut.TryGet("g1", out var retrieved);

        found.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("g1");
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        var sut = CreateSut();
        var found = sut.TryGet("unknown", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void ListGames_ReturnsAllCreatedGames()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.Create(CreateOnlineGame("g2", sut));

        var games = sut.ListGames();

        games.Should().HaveCount(2);
        games.Select(g => g.Id).Should().Contain(["g1", "g2"]);
    }

    [Fact]
    public void ListGames_Empty_ReturnsEmptyList()
    {
        var sut = CreateSut();
        sut.ListGames().Should().BeEmpty();
    }

    [Fact]
    public void ClearAll_RemovesAllGames_AndReturnsCount()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.Create(CreateOnlineGame("g2", sut));

        var count = sut.ClearAll();

        count.Should().Be(2);
        sut.ListGames().Should().BeEmpty();
    }

    [Fact]
    public void ClearAll_OnEmpty_ReturnsZero()
    {
        var sut = CreateSut();
        sut.ClearAll().Should().Be(0);
    }

    [Fact]
    public void ClearAll_MarksGamesAsClosed()
    {
        var sut = CreateSut();
        var game = CreateOnlineGame("g1", sut);
        sut.Create(game);

        sut.ClearAll();

        game.IsClosed.Should().BeTrue();
        game.EndReason.Should().Be("admin_reset");
    }

    [Fact]
    public void Subscribe_ReturnsTokenWithReader()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));

        var token = sut.Subscribe("g1");

        token.Id.Should().NotBeNullOrEmpty();
        token.Reader.Should().NotBeNull();
    }

    [Fact]
    public void Subscribe_ToUnknownGame_CreatesSubscribers()
    {
        var sut = CreateSut();
        var token = sut.Subscribe("unknown");
        token.Should().NotBeNull();
    }

    [Fact]
    public void Publish_DeliversEventToSubscribers()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        var token = sut.Subscribe("g1");

        sut.Publish("g1", new ServerEvent("game.started", new { gameId = "g1" }));

        token.Reader.TryRead(out var evt).Should().BeTrue();
        evt.Type.Should().Be("game.started");
    }

    [Fact]
    public void Unsubscribe_StopsReceivingEvents()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        var token = sut.Subscribe("g1");

        sut.Unsubscribe("g1", token.Id);
        sut.Publish("g1", new ServerEvent("test", new { }));

        token.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void TryReservePlayerForGame_FirstReservation_Succeeds()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));

        var result = sut.TryReservePlayerForGame("p1", "g1", out var blocking);

        result.Should().BeTrue();
        blocking.Should().BeNull();
    }

    [Fact]
    public void TryReservePlayerForGame_SamePlayerSameGame_ReturnsTrue()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.TryReservePlayerForGame("p1", "g1", out _);

        var result = sut.TryReservePlayerForGame("p1", "g1", out var blocking);

        result.Should().BeTrue();
        blocking.Should().BeNull();
    }

    [Fact]
    public void TryReservePlayerForGame_PlayerAlreadyInAnotherGame_ReturnsFalseWithBlockingId()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.Create(CreateOnlineGame("g2", sut));
        sut.TryReservePlayerForGame("p1", "g1", out _);

        var result = sut.TryReservePlayerForGame("p1", "g2", out var blocking);

        result.Should().BeFalse();
        blocking.Should().Be("g1");
    }

    [Fact]
    public void ReleasePlayerReservation_RemovesReservation()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.TryReservePlayerForGame("p1", "g1", out _);

        sut.ReleasePlayerReservation("p1", "g1");

        sut.ActivePlayerCount.Should().Be(0);
    }

    [Fact]
    public void ReleasePlayerReservation_DifferentGame_DoesNotRemove()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.Create(CreateOnlineGame("g2", sut));
        sut.TryReservePlayerForGame("p1", "g1", out _);

        sut.ReleasePlayerReservation("p1", "g2");

        sut.ActivePlayerCount.Should().Be(1);
    }

    [Fact]
    public void ReleaseAllPlayerReservations_RemovesAllAndClosesGameIfEmpty()
    {
        var sut = CreateSut();
        var game = CreateOnlineGame("g1", sut);
        sut.Create(game);
        sut.TryReservePlayerForGame("p1", "g1", out _);
        sut.TryReservePlayerForGame("p2", "g1", out _);

        sut.ReleaseAllPlayerReservations("g1", new[] { "p1", "p2" });

        sut.ActivePlayerCount.Should().Be(0);
        game.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void GetActivePlayerIds_ReturnsParsedGuids()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        sut.TryReservePlayerForGame(guid1, "g1", out _);
        sut.TryReservePlayerForGame(guid2, "g1", out _);

        var ids = sut.GetActivePlayerIds();

        ids.Should().HaveCount(2);
        ids.Should().Contain(Guid.Parse(guid1));
        ids.Should().Contain(Guid.Parse(guid2));
    }

    [Fact]
    public void GetActivePlayerIds_WithNonGuidPlayerIds_FiltersThemOut()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        var guid = Guid.NewGuid().ToString();
        sut.TryReservePlayerForGame(guid, "g1", out _);
        sut.TryReservePlayerForGame("not-a-guid", "g1", out _);

        var ids = sut.GetActivePlayerIds();

        ids.Should().HaveCount(1);
        ids.Should().Contain(Guid.Parse(guid));
    }

    [Fact]
    public void IsDraining_DefaultFalse()
    {
        var sut = CreateSut();
        sut.IsDraining.Should().BeFalse();
    }

    [Fact]
    public void ActivePlayerCount_ReflectsReservations()
    {
        var sut = CreateSut();
        sut.Create(CreateOnlineGame("g1", sut));
        sut.TryReservePlayerForGame("p1", "g1", out _);
        sut.TryReservePlayerForGame("p2", "g1", out _);

        sut.ActivePlayerCount.Should().Be(2);
    }

    private sealed class StubLanguageProvider : IGameLanguageProvider
    {
        private readonly IReadOnlySet<string> _dict;
        private readonly IReadOnlyDictionary<char, int> _scores;
        private readonly IReadOnlyDictionary<char, int> _dist;

        public StubLanguageProvider(IReadOnlySet<string> dict, IReadOnlyDictionary<char, int> scores, IReadOnlyDictionary<char, int> dist)
        {
            _dict = dict;
            _scores = scores;
            _dist = dist;
        }

        public IReadOnlySet<string> GetDictionary() => _dict;
        public IReadOnlyDictionary<char, int> GetLetterScores() => _scores;
        public IReadOnlyDictionary<char, int> GetTileDistribution() => _dist;
        public IReadOnlyDictionary<char, int> GetTileDistribution(TileDistributionProfile profile) => _dist;
        public string GetLanguageName() => "Test";
        public string GetLocale() => "test-XX";
    }
}
