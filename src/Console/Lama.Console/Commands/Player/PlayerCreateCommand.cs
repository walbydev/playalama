using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Player;

/// <summary>
/// Commande <c>lama player create &lt;nom&gt;</c> — crée un profil joueur.
/// Arguments : nom du joueur (positionnel, requis).
/// Accessible aux joueurs et aux admins.
/// </summary>
public sealed class PlayerCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "player.create";

    private readonly ISessionService _sessionService;
    private readonly ILogger<PlayerCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayerCreateCommand(
        ISessionService sessionService,
        ILogger<PlayerCreateCommand> logger)
    {
        _sessionService = sessionService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            global::System.Console.Error.WriteLine("[player create] Argument requis : <nom>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        var current = _sessionService.LoadSession();
        if (current?.GameId is not null)
        {
            global::System.Console.Error.WriteLine(
                "[player create] Impossible de changer de profil pendant une partie active.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        var now = DateTimeOffset.UtcNow;
        var createdAt = current?.CreatedAt ?? now;
        var newSession = new SessionContext(
            GameId:         null,
            PlayerId:       Guid.NewGuid().ToString("N"),
            PlayerName:     playerName.Trim(),
            Role:           Role.Player,
            GameLevel:      null,
            AuthToken:      current?.AuthToken,
            TokenExpiresAt: current?.TokenExpiresAt,
            CreatedAt:      createdAt,
            UpdatedAt:      now);

        _sessionService.SaveSession(newSession);

        _logger.LogInformation("Profil joueur créé : {PlayerName}", newSession.PlayerName);
        global::System.Console.WriteLine($"✓ Profil joueur créé : {newSession.PlayerName}");
        global::System.Console.WriteLine($"  PlayerId  : {newSession.PlayerId}");
        global::System.Console.WriteLine($"  Session   : {_sessionService.SessionFilePath}");
        return Task.FromResult(ExitCodes.Success);
    }
}
