using System.Text;
using System.Text.Json;

namespace Lama.Server.Services;

/// <summary>
/// Envoie un webhook HTTP vers HomeAssistant lors des événements serveur.
/// Configuré via la variable d'environnement <c>LAMA_HA_WEBHOOK_URL</c>.
/// Exemple HA : Settings → Automations → Trigger: Webhook → webhook_id = lama-events
/// </summary>
public sealed class HomeAssistantNotifier : IOutboundNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _webhookUrl;
    private readonly ILogger<HomeAssistantNotifier> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HomeAssistantNotifier(
        IHttpClientFactory httpClientFactory,
        string webhookUrl,
        ILogger<HomeAssistantNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _webhookUrl = webhookUrl;
        _logger = logger;
    }

    public async Task NotifyPlayerRegisteredAsync(
        string playerName,
        int totalPlayers,
        int activeGames,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            @event = "player.registered",
            playerName,
            totalPlayers,
            activeGames,
            timestamp = DateTimeOffset.UtcNow
        };

        await PostAsync(payload, cancellationToken);
    }

    private async Task PostAsync(object payload, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ha-notifier");
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(_webhookUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("HomeAssistant webhook a retourné {Status} pour {Url}", (int)response.StatusCode, _webhookUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fire-and-forget : on logue sans faire échouer la requête principale.
            _logger.LogWarning(ex, "Échec de l'envoi du webhook HomeAssistant vers {Url}", _webhookUrl);
        }
    }
}

/// <summary>
/// Implémentation no-op utilisée quand <c>LAMA_HA_WEBHOOK_URL</c> n'est pas configuré.
/// </summary>
public sealed class NullOutboundNotifier : IOutboundNotifier
{
    public Task NotifyPlayerRegisteredAsync(string playerName, int totalPlayers, int activeGames, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
