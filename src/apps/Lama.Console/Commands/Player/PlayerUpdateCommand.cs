using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Player;

/// <summary>
/// Commande <c>lama player update [playerId]</c>.
/// Met a jour les informations optionnelles du profil joueur.
/// </summary>
public sealed class PlayerUpdateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "player.update";

    private readonly IPlayerProfileService _playerProfileService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<PlayerUpdateCommand> _logger;

    public PlayerUpdateCommand(
        IPlayerProfileService playerProfileService,
        ISessionService sessionService,
        ILogger<PlayerUpdateCommand> logger)
    {
        _playerProfileService = playerProfileService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var playerId = context.GetArgument(0) ?? context.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            global::System.Console.Error.WriteLine("[player update] PlayerId requis (argument ou session active).");
            return ExitCodes.InvalidArgument;
        }

        var birthYearRaw = context.GetOption("birth-year");
        int? birthYear = null;
        if (birthYearRaw is not null)
        {
            if (!int.TryParse(birthYearRaw, out var parsedBirthYear))
            {
                global::System.Console.Error.WriteLine("[player update] --birth-year doit etre un entier valide.");
                return ExitCodes.InvalidArgument;
            }

            birthYear = parsedBirthYear;
        }

        var existing = await _playerProfileService.GetByIdAsync(playerId);
        var session = _sessionService.LoadSession();

        var displayName = context.GetOption("name")
                          ?? existing?.DisplayName
                          ?? session?.PlayerName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            global::System.Console.Error.WriteLine("[player update] Nom requis: utilisez --name pour initialiser le profil.");
            return ExitCodes.InvalidArgument;
        }

        try
        {
            var updated = await _playerProfileService.SaveAsync(new PlayerProfile(
                PlayerId: playerId,
                DisplayName: displayName,
                Pseudo: context.GetOption("pseudo") ?? existing?.Pseudo,
                Country: context.GetOption("country") ?? existing?.Country,
                Region: context.GetOption("region") ?? existing?.Region,
                BirthYear: birthYear ?? existing?.BirthYear,
                CreatedAt: existing?.CreatedAt ?? DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow));

            // Si le profil courant de session est mis a jour, synchroniser le displayName en session.
            if (session?.PlayerId == updated.PlayerId)
            {
                _sessionService.SaveSession(session with
                {
                    PlayerName = updated.DisplayName,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            global::System.Console.WriteLine($"✓ Profil mis a jour : {updated.PlayerId}");
            global::System.Console.WriteLine($"  Nom      : {updated.DisplayName}");
            global::System.Console.WriteLine($"  Pseudo   : {updated.Pseudo ?? "(non renseigne)"}");
            global::System.Console.WriteLine($"  Pays     : {updated.Country ?? "(non renseigne)"}");
            global::System.Console.WriteLine($"  Region   : {updated.Region ?? "(non renseignee)"}");
            global::System.Console.WriteLine($"  Naissance: {(updated.BirthYear?.ToString() ?? "(non renseignee)")}");

            _logger.LogInformation("Profil mis a jour: {PlayerId}", updated.PlayerId);
            return ExitCodes.Success;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            global::System.Console.Error.WriteLine($"[player update] {ex.Message}");
            return ExitCodes.InvalidArgument;
        }
    }
}

