namespace Lama.Server.Services;

/// <summary>
/// Notifie des systèmes externes (ex. HomeAssistant) lors d'événements serveur clés.
/// </summary>
public interface IOutboundNotifier
{
    /// <summary>Appelé dès qu'un nouveau joueur s'inscrit avec succès.</summary>
    Task NotifyPlayerRegisteredAsync(string playerName, int totalPlayers, int activeGames, CancellationToken cancellationToken = default);
}
