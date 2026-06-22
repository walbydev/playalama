using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game start</c> — démarre explicitement une partie en attente (lobby).
/// </summary>
public sealed class GameStartCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.start";

    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<GameStartCommand> _logger;

    public GameStartCommand(
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<GameStartCommand> logger)
    {
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!_runtimeMode.IsOnline)
        {
            global::System.Console.Error.WriteLine("[game start] Disponible uniquement en mode online.");
            return ExitCodes.InvalidArgument;
        }

        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[game start] Aucune partie active dans la session.");
            return ExitCodes.GameNotFound;
        }

        try
        {
            await _onlineGameGateway.EnsureAuthenticatedAsync(
                context.PlayerName ?? "Joueur",
                context.PlayerId,
                cancellationToken);

            var response = await _onlineGameGateway.StartGameAsync(context.GameId, context.PlayerId, cancellationToken);

            global::System.Console.WriteLine("✓ Partie démarrée.");
            global::System.Console.WriteLine($"  Partie        : {response.GameId}");
            global::System.Console.WriteLine($"  Max joueurs   : {response.MaxPlayers}");
            global::System.Console.WriteLine($"  Slots IA      : {response.ReservedAiSlots}");

            _logger.LogInformation("Partie démarrée (online): {GameId}", response.GameId);
            return ExitCodes.Success;
        }
        catch (HttpRequestException ex)
        {
            global::System.Console.Error.WriteLine($"[game start] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}

