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
    private readonly IPlayerProfileService? _playerProfileService;
    private readonly ILogger<PlayerCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayerCreateCommand(
        ISessionService sessionService,
        ILogger<PlayerCreateCommand> logger)
        : this(sessionService, playerProfileService: null, logger)
    {
    }

    /// <summary>Initialise la commande (avec persistance profil).</summary>
    public PlayerCreateCommand(
        ISessionService sessionService,
        IPlayerProfileService? playerProfileService,
        ILogger<PlayerCreateCommand> logger)
    {
        _sessionService = sessionService;
        _playerProfileService = playerProfileService;
        _logger         = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerName = context.GetArgument(0);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            global::System.Console.Error.WriteLine("[player create] Argument requis : <nom>");
            return ExitCodes.InvalidArgument;
        }

        var current = _sessionService.LoadSession();
        if (current?.GameId is not null)
        {
            global::System.Console.Error.WriteLine(
                "[player create] Impossible de changer de profil pendant une partie active.");
            return ExitCodes.InvalidArgument;
        }

        var birthYear = ParseBirthYear(context.GetOption("birth-year"));
        if (context.HasOption("birth-year") && birthYear is null)
        {
            global::System.Console.Error.WriteLine(
                "[player create] --birth-year doit etre un entier valide.");
            return ExitCodes.InvalidArgument;
        }

        var now = DateTimeOffset.UtcNow;
        var createdAt = current?.CreatedAt ?? now;
        var playerId = Guid.NewGuid().ToString("N");

        var newSession = new SessionContext(
            GameId:         null,
            PlayerId:       playerId,
            PlayerName:     playerName.Trim(),
            Role:           Role.Player,
            GameLevel:      null,
            AuthToken:      current?.AuthToken,
            TokenExpiresAt: current?.TokenExpiresAt,
            CreatedAt:      createdAt,
            UpdatedAt:      now);

        _sessionService.SaveSession(newSession);

        if (_playerProfileService is not null)
        {
            try
            {
                await _playerProfileService.SaveAsync(new PlayerProfile(
                    PlayerId: playerId,
                    DisplayName: newSession.PlayerName ?? playerName.Trim(),
                    Pseudo: context.GetOption("pseudo"),
                    Country: context.GetOption("country"),
                    Region: context.GetOption("region"),
                    BirthYear: birthYear,
                    CreatedAt: now,
                    UpdatedAt: now));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                global::System.Console.Error.WriteLine($"[player create] {ex.Message}");
                return ExitCodes.InvalidArgument;
            }
        }

        _logger.LogInformation("Profil joueur créé : {PlayerName}", newSession.PlayerName);
        global::System.Console.WriteLine($"✓ Profil joueur créé : {newSession.PlayerName}");
        global::System.Console.WriteLine($"  PlayerId  : {newSession.PlayerId}");
        if (!string.IsNullOrWhiteSpace(context.GetOption("pseudo")))
            global::System.Console.WriteLine($"  Pseudo    : {context.GetOption("pseudo")}");
        if (!string.IsNullOrWhiteSpace(context.GetOption("country")))
            global::System.Console.WriteLine($"  Pays      : {context.GetOption("country")}");
        if (!string.IsNullOrWhiteSpace(context.GetOption("region")))
            global::System.Console.WriteLine($"  Region    : {context.GetOption("region")}");
        if (birthYear is not null)
            global::System.Console.WriteLine($"  Naissance : {birthYear}");
        global::System.Console.WriteLine($"  Session   : {_sessionService.SessionFilePath}");
        return ExitCodes.Success;
    }

    private static int? ParseBirthYear(string? raw) =>
        raw is null ? null : int.TryParse(raw, out var year) ? year : null;
}
