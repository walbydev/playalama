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

    private static LamaApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5000")
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

