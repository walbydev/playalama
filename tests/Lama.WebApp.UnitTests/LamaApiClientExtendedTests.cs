using System.Net;
using System.Text;
using Lama.WebApp.Services;

namespace Lama.WebApp.UnitTests;

public class LamaApiClientExtendedTests
{
    [Fact]
    public async Task JoinGameAsync_Should_Send_Request_And_Parse_Response()
    {
        var handler = new StubHandler(request =>
        {
            request.RequestUri!.AbsolutePath.Should().Be("/api/v1/games/g1/join");
            request.Method.Should().Be(HttpMethod.Post);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"gameId\":\"g1\",\"playerId\":\"p2\",\"rack\":[\"A\",\"B\"]}",
                    Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var response = await client.JoinGameAsync("g1", "Bob", null);

        response.GameId.Should().Be("g1");
        response.PlayerId.Should().Be("p2");
        response.Rack.Should().ContainInOrder(['A', 'B']);
    }

    [Fact]
    public async Task StartGameAsync_Should_Send_Post_To_Start_Endpoint()
    {
        var captured = (HttpRequestMessage?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"started\":true}", Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.StartGameAsync("g1", "token");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/games/g1/start");
        captured.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task PlayAsync_Should_Send_Play_Command()
    {
        var captured = (HttpRequestMessage?)null;
        var capturedBody = (string?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"gameId\":\"g1\",\"isGameOver\":false,\"currentPlayerIndex\":1,\"turnNumber\":2,\"score\":12}",
                    Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var form = new PlayForm
        {
            PlayerId = "p1",
            Command = "move",
            Position = "H8",
            Word = "LA",
            Direction = "H"
        };
        var response = await client.PlayAsync("g1", form, "token");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/games/g1/moves");
        capturedBody.Should().Contain("\"command\":\"move\"");
        response.GameId.Should().Be("g1");
        response.Score.Should().Be(12);
    }

    [Fact]
    public async Task AbandonAsync_Should_Send_Abandon_Request()
    {
        var captured = (HttpRequestMessage?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"abandoned\":true}", Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.AbandonAsync("g1", "p1", "token");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/games/g1/abandon");
        captured.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetLeaderboardAsync_Should_Map_Leaderboard_Entries()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                [
                    { "playerId": "p1", "username": "Alice", "countryCode": "FR", "level": 5, "elo": 1850, "wins": 42, "games": 80 },
                    { "playerId": "p2", "username": "Bob", "countryCode": "DE", "level": 4, "elo": 1450, "wins": 20, "games": 50 }
                ]
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var entries = await client.GetLeaderboardAsync("tournament", null, 50);

        entries.Should().HaveCount(2);
        entries[0].Rank.Should().Be(1);
        entries[0].Username.Should().Be("Alice");
        entries[0].Elo.Should().Be(1850);
        entries[1].Rank.Should().Be(2);
        entries[1].Username.Should().Be("Bob");
    }

    [Fact]
    public async Task GetLeaderboardAsync_WithCountryCode_AppendsCountryParam()
    {
        var captured = (HttpRequestMessage?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.GetLeaderboardAsync("open", "FR", 20);

        captured.Should().NotBeNull();
        captured!.RequestUri!.Query.Should().Contain("country=FR");
    }

    [Fact]
    public async Task GetLeaderboardAsync_OnFailure_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);
        var entries = await client.GetLeaderboardAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWordInfoAsync_Should_Map_WordInfo_From_Api()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                    "word": "LAMA",
                    "lang": "fr",
                    "definitions": [
                        { "senseIndex": 1, "partOfSpeech": "nom", "text": "Animal de la famille des camélidés" }
                    ]
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var info = await client.GetWordInfoAsync("fr", "LAMA");

        info.Should().NotBeNull();
        info!.Word.Should().Be("LAMA");
        info.Definitions.Should().HaveCount(1);
        info.Definitions[0].Text.Should().Contain("camélidés");
    }

    [Fact]
    public async Task GetWordInfoAsync_WhenNotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);
        var info = await client.GetWordInfoAsync("fr", "XYZW");
        info.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_Should_Send_Registration_And_Parse_Token()
    {
        var captured = (HttpRequestMessage?)null;
        var capturedBody = (string?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"token\":\"jwt-token\",\"playerId\":\"p1\",\"playerName\":\"Alice\",\"email\":\"a@b.com\",\"countryCode\":\"FR\",\"expiresAt\":\"2026-07-05T10:00:00Z\"}",
                    Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var response = await client.RegisterAsync("Alice", "password123", "a@b.com", "FR");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/auth/register");
        capturedBody.Should().Contain("\"username\":\"Alice\"");
        response.Token.Should().Be("jwt-token");
        response.PlayerId.Should().Be("p1");
        response.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public async Task AccountLoginAsync_Should_Send_Login_And_Parse_Token()
    {
        var captured = (HttpRequestMessage?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"token\":\"jwt-token\",\"playerId\":\"p1\",\"playerName\":\"Alice\",\"email\":\"a@b.com\",\"countryCode\":\"FR\",\"expiresAt\":\"2026-07-05T10:00:00Z\"}",
                    Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var response = await client.AccountLoginAsync("Alice", "password123");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/auth/login/account");
        response.Token.Should().Be("jwt-token");
        response.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetMyGamesAsync_Should_Return_GameHistory()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                [
                    { "gameId": "g1", "gameLevel": "Standard", "queue": "OpenRanked", "status": "won", "endedAt": "2026-07-01T10:00:00Z", "durationSeconds": 600, "isWinner": true },
                    { "gameId": "g2", "gameLevel": "Casual", "queue": "CasualUnranked", "status": "lost", "endedAt": "2026-07-02T15:00:00Z", "durationSeconds": 300, "isWinner": false }
                ]
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var games = await client.GetMyGamesAsync("token");

        games.Should().HaveCount(2);
        games[0].GameId.Should().Be("g1");
        games[0].IsWinner.Should().BeTrue();
        games[1].GameId.Should().Be("g2");
        games[1].IsWinner.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyGamesAsync_OnUnauthorized_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);
        var games = await client.GetMyGamesAsync("bad-token");
        games.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_Should_Return_Status_Snapshot()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                    "collectedAt": "2026-07-04T10:00:00Z",
                    "server": { "uptime": "01:23:45", "uptimeSeconds": 5025, "memoryMb": 128.5, "threadCount": 8, "environment": "Development", "version": "0.1.6" },
                    "games": { "activeCount": 3, "activePlayers": 5, "isDraining": false },
                    "players": { "totalRegistered": 50 },
                    "history": { "totalCompleted": 100 },
                    "database": { "status": "ok", "provider": "Npgsql", "migrationPending": false },
                    "aiServer": { "status": "ok", "url": null, "language": null, "responseTimeMs": null }
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var status = await client.GetStatusAsync("admin-token");

        status.Should().NotBeNull();
        status!.Server.Should().NotBeNull();
        status.Server!.Version.Should().Be("0.1.6");
        status.Games!.ActiveCount.Should().Be(3);
        status.Database!.Status.Should().Be("ok");
    }

    [Fact]
    public async Task GetBotsAsync_Should_Return_Bot_List()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                    "bots": [
                        { "botId": "bot-karim", "name": "B'Karim", "level": 1450, "initialElo": 1450 },
                        { "botId": "bot-lina", "name": "Lina", "level": 1850, "initialElo": 1850 }
                    ]
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var bots = await client.GetBotsAsync();

        bots.Should().HaveCount(2);
        bots[0].BotId.Should().Be("bot-karim");
        bots[0].Name.Should().Be("B'Karim");
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Stats()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                    "activePlayers": 10,
                    "gamesPlayed": 1000,
                    "languages": 3
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var stats = await client.GetStatsAsync();

        stats.Should().NotBeNull();
        stats!.ActivePlayers.Should().Be(10);
        stats.GamesPlayed.Should().Be(1000);
    }

    [Fact]
    public async Task DeleteMyGameAsync_Should_Send_Delete_Request()
    {
        var captured = (HttpRequestMessage?)null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var client = CreateClient(handler);
        await client.DeleteMyGameAsync("token", "g1");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/players/me/games/g1");
        captured.Method.Should().Be(HttpMethod.Delete);
    }

    private static LamaApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5201") };
        return new LamaApiClient(httpClient);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
