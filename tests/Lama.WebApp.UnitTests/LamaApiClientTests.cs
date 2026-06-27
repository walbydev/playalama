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
