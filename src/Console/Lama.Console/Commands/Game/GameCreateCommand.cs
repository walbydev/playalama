using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game create</c> — crée une nouvelle partie et initialise la session.
///
/// Options :
/// <list type="bullet">
///   <item><c>--size N</c>       Taille du plateau (défaut : 15, min : 15, max : 26)</item>
///   <item><c>--players N</c>    Nombre de joueurs (défaut : 2)</item>
///   <item><c>--time-limit N</c> Limite en secondes par tour (0 = illimité)</item>
///   <item><c>--max-turns N</c>  Nombre maximum de tours</item>
///   <item><c>--level</c>        Niveau : casual | standard | competitive | tournament (défaut : standard)</item>
/// </list>
///
/// Réservée aux administrateurs.
/// Écrit la session dans le fichier de session local après création.
/// </summary>
/// <remarks>
/// TODO: l'appel à Lama.Core (cas d'usage CreateGame) est commenté — non encore implémenté.
/// La gestion de session est déjà en place et sera activée dès que Lama.Core sera disponible.
/// </remarks>
public sealed class GameCreateCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.create";

    private readonly ISessionService _sessionService;
    private readonly ILogger<GameCreateCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameCreateCommand(ISessionService sessionService, ILogger<GameCreateCommand> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // Lecture des options
        var levelStr = context.GetOption("level") ?? "standard";
        if (!Enum.TryParse<GameLevel>(levelStr, ignoreCase: true, out var gameLevel))
        {
            global::System.Console.Error.WriteLine(
                $"[game create] Niveau invalide : '{levelStr}'. " +
                "Valeurs acceptées : casual, standard, competitive, tournament.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        // TODO: appeler le cas d'usage CreateGame de Lama.Core pour :
        //   - initialiser le plateau (--size)
        //   - créer le sac de lettres
        //   - préparer les racks
        //   - retourner un GameId généré
        // Exemple (à décommenter) :
        // var result = await _gameUseCase.CreateAsync(new CreateGameRequest(
        //     Size: int.TryParse(context.GetOption("size"), out var s) ? s : 15,
        //     MaxPlayers: int.TryParse(context.GetOption("players"), out var p) ? p : 2,
        //     TimeLimitSeconds: int.TryParse(context.GetOption("time-limit"), out var t) ? t : 0,
        //     MaxTurns: int.TryParse(context.GetOption("max-turns"), out var mt) ? (int?)mt : null,
        //     Level: gameLevel
        // ));
        // var gameId = result.GameId;

        _logger.LogWarning("{CommandId} : Lama.Core absent — session non créée", CommandId);
        global::System.Console.Error.WriteLine(
            "[game create] Non implémenté — Lama.Core absent. " +
            "La session ne peut pas être créée sans le moteur de jeu.");

        // ── Ce bloc sera activé quand Lama.Core sera implémenté ──────────────
        // var session = new SessionContext(
        //     GameId:     gameId,
        //     PlayerId:   Guid.NewGuid().ToString("N"),
        //     PlayerName: "Admin",
        //     Role:       Role.Admin,
        //     GameLevel:  gameLevel,
        //     CreatedAt:  DateTimeOffset.UtcNow,
        //     UpdatedAt:  DateTimeOffset.UtcNow);
        // _sessionService.SaveSession(session);
        // global::System.Console.WriteLine($"Partie créée : {gameId} (niveau : {gameLevel})");
        // global::System.Console.WriteLine($"Session sauvegardée : {_sessionService.SessionFilePath}");
        // return Task.FromResult(ExitCodes.Success);
        // ─────────────────────────────────────────────────────────────────────

        return Task.FromResult(ExitCodes.GeneralError);
    }
}
