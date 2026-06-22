using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play check &lt;case&gt; &lt;mot&gt; &lt;direction&gt;</c>
/// — vérifie la validité d'un coup sans le jouer (aide pour débutants).
/// Accessible uniquement en mode Casual (et aux admins).
/// Arguments : identiques à <c>play move</c>.
/// </summary>
public sealed class PlayCheckCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.check";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<PlayCheckCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlayCheckCommand(CreateGameUseCase createGameUseCase, ILogger<PlayCheckCommand> logger)
        : this(
            createGameUseCase,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public PlayCheckCommand(
        CreateGameUseCase createGameUseCase,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<PlayCheckCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var position  = context.GetArgument(0);
        var word      = context.GetArgument(1);
        var direction = context.GetArgument(2);

        if (string.IsNullOrWhiteSpace(position) ||
            string.IsNullOrWhiteSpace(word) ||
            string.IsNullOrWhiteSpace(direction))
        {
            global::System.Console.Error.WriteLine(
                "[play check] Arguments requis : <case> <mot> <direction>");
            return ExitCodes.InvalidArgument;
        }

        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[play check] Aucune partie active.");
            return ExitCodes.GameNotFound;
        }

        if (_runtimeMode.IsOnline)
        {
            if (context.PlayerId is null)
            {
                global::System.Console.Error.WriteLine("[play check] Session joueur invalide en mode online.");
                return ExitCodes.GameNotFound;
            }

            try
            {
                await _onlineGameGateway.EnsureAuthenticatedAsync(
                    context.PlayerName ?? "Joueur",
                    context.PlayerId,
                    cancellationToken);

                var payload = new
                {
                    position,
                    word,
                    direction = direction.ToUpperInvariant()
                };

                var response = await _onlineGameGateway.PlayCommandAsync(
                    context.GameId,
                    context.PlayerId,
                    "play.check",
                    payload,
                    cancellationToken);

                global::System.Console.WriteLine($"✓ Coup valide : {word.ToUpperInvariant()} en {position.ToUpperInvariant()} {direction.ToUpperInvariant()} — {response.Score} pts");
                _logger.LogInformation("{CommandId} online : coup valide verifie", CommandId);
                return ExitCodes.Success;
            }
            catch (HttpRequestException ex)
            {
                global::System.Console.Error.WriteLine($"[play check] Erreur online : {ex.Message}");
                return ExitCodes.InvalidPlacement;
            }
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine($"[play check] Partie introuvable : {context.GameId}");
            return ExitCodes.GameNotFound;
        }

        var movePosition = position;
        var moveWord = word;
        var moveDirection = direction;

        if (!TryParseMove(movePosition, moveWord, moveDirection, out var placements, out var error))
        {
            global::System.Console.Error.WriteLine($"[play check] {error}");
            return ExitCodes.InvalidArgument;
        }

        var (isValid, validationError, score) = engine.ValidateMove(placements);
        if (!isValid)
        {
            global::System.Console.Error.WriteLine($"[play check] Coup invalide : {validationError}");
            return ExitCodes.InvalidPlacement;
        }

        global::System.Console.WriteLine($"✓ Coup valide : {moveWord.ToUpperInvariant()} en {movePosition.ToUpperInvariant()} {moveDirection.ToUpperInvariant()} — {score} pts");
        _logger.LogInformation("{CommandId} : coup valide vérifié", CommandId);
        return ExitCodes.Success;
    }

    private static bool TryParseMove(
        string position,
        string word,
        string direction,
        out Dictionary<Position, char> placements,
        out string error)
    {
        placements = new Dictionary<Position, char>();
        error = string.Empty;

        var pos = position.Trim().ToUpperInvariant();
        var dir = direction.Trim().ToUpperInvariant();
        var letters = word.Trim().ToUpperInvariant();

        if (pos.Length < 2)
        {
            error = $"Position invalide : '{position}'";
            return false;
        }

        var colChar = pos[0];
        if (colChar < 'A' || colChar > 'O' || !int.TryParse(pos[1..], out var row) || row < 1 || row > 15)
        {
            error = $"Position invalide : '{position}'";
            return false;
        }

        if (dir is not ("H" or "V"))
        {
            error = "Direction invalide : utilisez H ou V";
            return false;
        }

        var startRow = row - 1;
        var startCol = colChar - 'A';
        for (var i = 0; i < letters.Length; i++)
        {
            var target = dir == "H"
                ? new Position(startRow, startCol + i)
                : new Position(startRow + i, startCol);

            placements[target] = letters[i];
        }

        return true;
    }
}
