using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lama.Console.Commands.Game;

/// <summary>
/// Commande <c>lama game save</c> — sauvegarde l'état de la partie courante.
/// Accessible aux admins et aux joueurs (pas aux spectateurs).
/// Options : --file (chemin de sauvegarde optionnel).
/// </summary>
public sealed class GameSaveCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "game.save";

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ISessionService _sessionService;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<GameSaveCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public GameSaveCommand(
        CreateGameUseCase createGameUseCase,
        ISessionService sessionService,
        IGameRepository gameRepository,
        ILogger<GameSaveCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _sessionService = sessionService;
        _gameRepository = gameRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine("[game save] Aucune partie active a sauvegarder.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        try
        {
            var engine = _createGameUseCase.GetEngine(context.GameId);
            if (engine is null)
            {
                global::System.Console.Error.WriteLine(
                    $"[game save] Partie introuvable : {context.GameId}");
                return Task.FromResult(ExitCodes.GameNotFound);
            }

            var state = engine.GetGameState();
            var gameLevel = context.GameLevel ?? GameLevel.Standard;
            var isFirstMove = state.TurnNumber == 1 && IsBoardEmpty(state.Board);

            _createGameUseCase.SaveGame(context.GameId, gameLevel, isFirstMove);

            var fileOption = context.GetOption("file");
            if (!string.IsNullOrWhiteSpace(fileOption))
            {
                var persisted = _gameRepository.Load(context.GameId);
                if (persisted is null)
                {
                    global::System.Console.Error.WriteLine(
                        $"[game save] Impossible d'exporter, partie introuvable : {context.GameId}");
                    return Task.FromResult(ExitCodes.GeneralError);
                }

                var exportPath = Path.GetFullPath(fileOption);
                var exportDir = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrWhiteSpace(exportDir))
                    Directory.CreateDirectory(exportDir);

                File.WriteAllText(exportPath, JsonSerializer.Serialize(persisted, ExportJsonOptions));
                global::System.Console.WriteLine($"✓ Export realise : {exportPath}");
            }

            var session = _sessionService.LoadSession();
            if (session is not null)
                _sessionService.SaveSession(session with { UpdatedAt = DateTimeOffset.UtcNow });

            global::System.Console.WriteLine($"✓ Partie sauvegardee ({context.GameId})");
            _logger.LogInformation("Partie sauvegardee : {GameId}", context.GameId);
            return Task.FromResult(ExitCodes.Success);
        }
        catch (GameException ex)
        {
            global::System.Console.Error.WriteLine($"[game save] Erreur : {ex.Message}");
            return Task.FromResult(ExitCodes.GeneralError);
        }
    }

    private static bool IsBoardEmpty(BoardState board)
    {
        for (var row = 0; row < 15; row++)
            for (var col = 0; col < 15; col++)
                if (board.Grid[row, col] is not null)
                    return false;

        return true;
    }
}
