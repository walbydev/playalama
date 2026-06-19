using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play swap &lt;lettres&gt;</c> — échange des lettres avec le sac.
/// Arguments : lettres à échanger (ex: AEI), ou --all pour échanger tout le rack.
/// Accessible aux joueurs et aux admins.
/// </summary>
public sealed class PlaySwapCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.swap";

    private readonly SwapLettersUseCase _swapLettersUseCase;
    private readonly ISessionService _sessionService;
    private readonly RuntimeModeService _runtimeMode;
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<PlaySwapCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public PlaySwapCommand(
        SwapLettersUseCase swapLettersUseCase,
        ISessionService sessionService,
        ILogger<PlaySwapCommand> logger)
        : this(
            swapLettersUseCase,
            sessionService,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>Initialise la commande.</summary>
    public PlaySwapCommand(
        SwapLettersUseCase swapLettersUseCase,
        ISessionService sessionService,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<PlaySwapCommand> logger)
    {
        _swapLettersUseCase = swapLettersUseCase;
        _sessionService = sessionService;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var swapAll = context.HasOption("all");
        var letters = context.GetArgument(0);

        if (!swapAll && string.IsNullOrWhiteSpace(letters))
        {
            global::System.Console.Error.WriteLine(
                "[play swap] Argument requis : <lettres> ou --all");
            global::System.Console.Error.WriteLine(
                "  Exemple : lama play swap AEI");
            return ExitCodes.InvalidArgument;
        }

        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine(
                "[play swap] Aucune session active. Creez/rejoignez une partie d'abord.");
            return ExitCodes.GameNotFound;
        }

        try
        {
            if (_runtimeMode.IsOnline)
            {
                await _onlineGameGateway.EnsureAuthenticatedAsync(
                    context.PlayerName ?? "Joueur",
                    context.PlayerId,
                    cancellationToken);

                object payload = swapAll
                    ? new { swapAll = true }
                    : new { letters = letters!.ToUpperInvariant(), swapAll = false };

                var onlineResponse = await _onlineGameGateway.PlayCommandAsync(
                    context.GameId,
                    context.PlayerId,
                    "play.swap",
                    payload,
                    cancellationToken);

                var snapshot = await _onlineGameGateway.GetGameAsync(context.GameId, cancellationToken);
                var nextPlayerName = snapshot.CurrentPlayerIndex >= 0 && snapshot.CurrentPlayerIndex < snapshot.Players.Count
                    ? snapshot.Players[snapshot.CurrentPlayerIndex].PlayerName
                    : onlineResponse.NextPlayerId ?? "inconnu";

                global::System.Console.WriteLine(
                    $"✓ Echange effectue (online) ({(swapAll ? "tout le rack" : letters!.ToUpperInvariant())})");
                if (onlineResponse.NewRack is not null)
                    global::System.Console.WriteLine($"  Nouveau rack : {string.Join(" ", onlineResponse.NewRack)}");
                global::System.Console.WriteLine($"  Tour suivant : {nextPlayerName}");

                _logger.LogInformation("{CommandId} execute online par {Player}", CommandId, context.PlayerName);
                return ExitCodes.Success;
            }

            var request = new SwapLettersRequest(
                GameId: context.GameId,
                PlayerId: context.PlayerId,
                Letters: swapAll ? null : letters!.ToUpperInvariant().ToCharArray(),
                SwapAll: swapAll);

            var response = await _swapLettersUseCase.ExecuteAsync(request);

            global::System.Console.WriteLine(
                $"✓ Echange effectue ({(swapAll ? "tout le rack" : letters!.ToUpperInvariant())})");
            global::System.Console.WriteLine($"  Nouveau rack : {string.Join(" ", response.NewRack)}");
            global::System.Console.WriteLine(
                $"  Tour suivant : {response.GameState.Players[response.GameState.CurrentPlayerIndex].Name}");

            var session = _sessionService.LoadSession();
            if (session is not null)
                _sessionService.SaveSession(session with { UpdatedAt = DateTimeOffset.UtcNow });

            _logger.LogInformation("{CommandId} execute par {Player}", CommandId, context.PlayerName);
            return ExitCodes.Success;
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[play swap] Erreur : {ex.Message}");
            return ExitCodes.GeneralError;
        }
        catch (HttpRequestException ex)
        {
            global::System.Console.Error.WriteLine($"[play swap] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
