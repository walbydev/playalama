using System.Net;
using System.Text;
using Lama.WebApp.Services;

namespace Lama.WebApp.UnitTests;

public sealed class LamaApiClientTests
{
    [Fact]
    public async Task ListGamesAsync_Should_Map_Games_From_Api()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                                        {
                                          "total": 1,
                                          "games": [
                                            {
                                              "id": "g1",
                                              "gameName": "Salon test",
                                              "status": "waiting",
                                              "players": 1,
                                              "maxPlayers": 4,
                                              "queue": "open",
                                              "isJoinable": true
                                            }
                                          ]
                                        }
                                        """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);

        var games = await client.ListGamesAsync();

        games.Should().HaveCount(1);
        games[0].Id.Should().Be("g1");
        games[0].GameName.Should().Be("Salon test");
        games[0].IsJoinable.Should().BeTrue();
    }

    [Fact]
    public async Task ListGamesAsync_Should_Accept_Numeric_Queue()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "total": 1,
                  "games": [
                    {
                      "id": "g-num",
                      "gameName": "Salon ranked",
                      "status": "waiting",
                      "players": 1,
                      "maxPlayers": 4,
                      "queue": 1,
                      "isJoinable": true
                    }
                  ]
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);

        var games = await client.ListGamesAsync();

        games.Should().HaveCount(1);
        games[0].Queue.Should().Be("Ranked");
    }

    [Fact]
    public async Task CreateGameAsync_Should_Send_Request_And_Parse_Response()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"gameId\":\"g2\",\"hostPlayerId\":\"p1\"}", Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var response = await client.CreateGameAsync(new CreateGameForm { Mode = "multi", MaxPlayers = 4 }, "Alice");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/api/v1/games");
        capturedBody.Should().Contain("\"hostName\":\"Alice\"");
        response.GameId.Should().Be("g2");
        response.HostPlayerId.Should().Be("p1");
    }

    [Fact]
    public async Task GetGameAsync_Should_Map_Last_Move_And_Bot_Flag()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "id": "g1",
                  "gameName": "Partie test",
                  "isGameOver": false,
                  "hasStarted": true,
                  "usesLobby": false,
                  "isClosed": false,
                  "currentPlayerIndex": 1,
                  "turnNumber": 4,
                  "bagCount": 67,
                  "maxPlayers": 2,
                  "boardSize": 15,
                  "rackSize": 7,
                  "language": "fr",
                  "players": [
                    { "playerId": "p1", "playerName": "Mathias", "score": 12, "isHost": true, "isBot": false, "rack": ["A"], "rackCount": 1 },
                    { "playerId": "bot-karim", "playerName": "B'Karim", "score": 9, "isHost": false, "isBot": true, "rack": ["B"], "rackCount": 1 }
                  ],
                  "board": [],
                  "lastMoveId": "m3",
                  "lastMovePlayerName": "B'Karim",
                  "lastMoveTurnNumber": 3,
                  "abandonedPlayerIds": [],
                  "endReason": null,
                  "abandonedByName": null
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var snapshot = await client.GetGameAsync("g1");

        snapshot.LastMoveId.Should().Be("m3");
        snapshot.LastMovePlayerName.Should().Be("B'Karim");
        snapshot.LastMoveTurnNumber.Should().Be(3);
        snapshot.Players.Should().HaveCount(2);
        snapshot.Players[1].IsBot.Should().BeTrue();
    }

    [Fact]
    public async Task SuggestMovesAsync_Should_Parse_Suggestions_From_Api()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "gameId": "g1",
                  "suggestions": [
                    { "word": "LAMA", "position": "H8", "direction": "H", "score": 12, "length": 4, "balancedScore": 13.5, "category": "score" },
                    { "word": "MALADE", "position": "G4", "direction": "V", "score": 8, "length": 6, "balancedScore": 9.0, "category": "length" }
                  ],
                  "message": "2 suggestion(s) trouvées."
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var suggestions = await client.SuggestMovesAsync("g1", "p1", topPerCategory: 2, token: null);

        suggestions.Should().HaveCount(2);
        suggestions[0].Word.Should().Be("LAMA");
        suggestions[0].Score.Should().Be(12);
        suggestions[0].Category.Should().Be("score");
        suggestions[1].Word.Should().Be("MALADE");
        suggestions[1].Length.Should().Be(6);
        suggestions[1].Category.Should().Be("length");
    }

    [Fact]
    public async Task SuggestMovesAsync_Should_Return_Empty_When_No_Suggestions_In_Response()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"gameId\":\"g1\",\"suggestions\":[],\"message\":\"Aucune suggestion.\"}",
                Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var suggestions = await client.SuggestMovesAsync("g1", "p1");

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchWordsAsync_Should_Return_Words_From_Lexicon_Endpoint()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "lang": "fr",
                  "query": "LAM",
                  "words": ["LAMA", "LAMAIRE", "LAMANEUR"]
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var words = await client.SearchWordsAsync("fr", "lam", 20);

        words.Should().Equal("LAMA", "LAMAIRE", "LAMANEUR");
    }

    [Fact]
    public async Task SearchWordsAsync_Should_Return_Empty_When_Endpoint_Fails()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var client = CreateClient(handler);
        var words = await client.SearchWordsAsync("fr", "l");

        words.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyProfileAsync_Should_Map_Accessibility_From_Api()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "playerId": "p1",
                  "username": "Alice",
                  "email": "alice@example.com",
                  "countryCode": "FR",
                  "accessibility": {
                    "theme": "highcontrast",
                    "fontSize": 150,
                    "boardScale": 1.8
                  },
                  "createdAt": "2026-07-03T10:00:00Z"
                }
                """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);
        var profile = await client.GetMyProfileAsync("token");

        profile.Should().NotBeNull();
        profile!.Accessibility.Should().NotBeNull();
        profile.Accessibility!.Theme.Should().Be("highcontrast");
        profile.Accessibility.FontSize.Should().Be(150);
        profile.Accessibility.BoardScale.Should().Be(1.8);
    }

    [Fact]
    public async Task UpdateMyAccessibilityAsync_Should_Send_Accessibility_Payload()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "playerId": "p1",
                      "username": "Alice",
                      "email": "alice@example.com",
                      "countryCode": "FR",
                      "accessibility": {
                        "theme": "deuteranopia",
                        "fontSize": 125,
                        "boardScale": 1.5
                      },
                      "createdAt": "2026-07-03T10:00:00Z"
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var profile = await client.UpdateMyAccessibilityAsync("token", new WebAccessibilityPreferences("deuteranopia", 125, 1.5));

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Put);
        captured.RequestUri!.AbsolutePath.Should().Be("/api/v1/players/me");
        capturedBody.Should().Contain("\"accessibility\":");
        capturedBody.Should().Contain("\"theme\":\"deuteranopia\"");
        capturedBody.Should().Contain("\"fontSize\":125");
        capturedBody.Should().Contain("\"boardScale\":1.5");
        profile.Should().NotBeNull();
        profile!.Accessibility!.Theme.Should().Be("deuteranopia");
    }

    private static LamaApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5201")
        };

        return new LamaApiClient(httpClient);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
