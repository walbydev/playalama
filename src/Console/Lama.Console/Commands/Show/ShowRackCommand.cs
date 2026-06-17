using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show rack [--with-values]</c>
/// — affiche le rack du joueur courant.
/// </summary>
public sealed class ShowRackCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.rack";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly IGameLanguageProvider _languageProvider;
    private readonly ILogger<ShowRackCommand> _logger;

    /// <summary>Initialise la commande.</summary>
    public ShowRackCommand(
        CreateGameUseCase  createGameUseCase,
        IGameLanguageProvider languageProvider,
        ILogger<ShowRackCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _languageProvider  = languageProvider;
        _logger            = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine("[show rack] Aucune session active.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine(
                "[show rack] Partie introuvable en mémoire.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var state       = engine.GetGameState();

        // Trouver le joueur par son nom (stocké dans la session)
        var playerName  = context.PlayerName ?? context.GetOption("player");
        var player      = playerName is not null
            ? state.Players.FirstOrDefault(p => p.Name == playerName)
            : state.Players.ElementAtOrDefault(state.CurrentPlayerIndex);

        if (player is null)
        {
            global::System.Console.Error.WriteLine("[show rack] Joueur introuvable.");
            return Task.FromResult(ExitCodes.GeneralError);
        }

        var rack        = player.Rack;
        var withValues  = context.HasOption("with-values");
        var scores      = _languageProvider.GetLetterScores();

        global::System.Console.WriteLine($"Rack de {player.Name} :");
        global::System.Console.Write("  ");

        foreach (var letter in rack)
        {
            if (withValues && scores.TryGetValue(letter, out var pts))
                global::System.Console.Write($"[{letter}({pts})] ");
            else
                global::System.Console.Write($"[{letter}] ");
        }

        global::System.Console.WriteLine();
        global::System.Console.WriteLine($"  {rack.Count} lettre(s)");

        return Task.FromResult(ExitCodes.Success);
    }
}
