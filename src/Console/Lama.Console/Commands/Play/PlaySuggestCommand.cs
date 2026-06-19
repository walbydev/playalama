using System.Text.Json;
using Lama.Console.Services;
using Lama.Core.Models;
using Lama.Core.UseCases;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Play;

/// <summary>
/// Commande <c>lama play suggest</c>.
/// Propose des coups au joueur courant (stub local pour l'instant).
/// </summary>
public sealed class PlaySuggestCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "play.suggest";

    private readonly SuggestMovesUseCase _suggestMovesUseCase;
    private readonly RuntimeModeService _runtimeMode;
    private readonly ILogger<PlaySuggestCommand> _logger;

    /// <summary>
    /// Constructeur de retrocompatibilite (mode local).
    /// </summary>
    public PlaySuggestCommand(
        SuggestMovesUseCase suggestMovesUseCase,
        ILogger<PlaySuggestCommand> logger)
        : this(suggestMovesUseCase, new RuntimeModeService(), logger)
    {
    }

    /// <summary>
    /// Initialise la commande.
    /// </summary>
    public PlaySuggestCommand(
        SuggestMovesUseCase suggestMovesUseCase,
        RuntimeModeService runtimeMode,
        ILogger<PlaySuggestCommand> logger)
    {
        _suggestMovesUseCase = suggestMovesUseCase;
        _runtimeMode = runtimeMode;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (!context.HasActiveSession || context.GameId is null || context.PlayerId is null)
        {
            global::System.Console.Error.WriteLine("[play suggest] Aucune partie active.");
            return ExitCodes.GameNotFound;
        }

        if (_runtimeMode.IsOnline)
        {
            global::System.Console.Error.WriteLine("[play suggest] Cette commande est disponible en mode local uniquement (stub).");
            return ExitCodes.GeneralError;
        }

        if (!TryParseTop(context.GetOption("top"), out var top))
        {
            global::System.Console.Error.WriteLine("[play suggest] --top doit etre un entier >= 1.");
            return ExitCodes.InvalidArgument;
        }

        if (!TryParseSort(context.GetOption("sort"), out var sort))
        {
            global::System.Console.Error.WriteLine("[play suggest] --sort doit valoir score, length ou balanced.");
            return ExitCodes.InvalidArgument;
        }

        try
        {
            var response = await _suggestMovesUseCase.ExecuteAsync(
                new SuggestMovesRequest(context.GameId, context.PlayerId, top, sort));

            return WriteOutput(context.OutputFormat, response.Suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "play.suggest a echoue");
            global::System.Console.Error.WriteLine($"[play suggest] {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }

    private static int WriteOutput(string format, IReadOnlyList<SuggestedMoveCandidate> suggestions)
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                global::System.Console.WriteLine(JsonSerializer.Serialize(suggestions));
                return ExitCodes.Success;

            case "csv":
                global::System.Console.WriteLine("word,position,direction,score,length,balancedScore");
                foreach (var suggestion in suggestions)
                {
                    global::System.Console.WriteLine(
                        $"{suggestion.Word},{suggestion.Position},{suggestion.Direction},{suggestion.Score},{suggestion.Length},{suggestion.BalancedScore:0.###}");
                }

                return ExitCodes.Success;

            default:
                if (suggestions.Count == 0)
                {
                    global::System.Console.WriteLine("Aucune suggestion disponible (stub). ");
                    return ExitCodes.Success;
                }

                global::System.Console.WriteLine($"{suggestions.Count} suggestion(s):");
                for (var i = 0; i < suggestions.Count; i++)
                {
                    var s = suggestions[i];
                    global::System.Console.WriteLine(
                        $"  {i + 1}. {s.Word} en {s.Position} {s.Direction} - {s.Score} pts (len={s.Length}, balanced={s.BalancedScore:0.###})");
                }

                return ExitCodes.Success;
        }
    }

    private static bool TryParseTop(string? raw, out int top)
    {
        top = 2;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!int.TryParse(raw, out top))
            return false;

        if (top < 1)
            return false;

        return true;
    }

    private static bool TryParseSort(string? raw, out MoveSuggestionSort sort)
    {
        sort = MoveSuggestionSort.Score;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "score":
                sort = MoveSuggestionSort.Score;
                return true;
            case "length":
                sort = MoveSuggestionSort.Length;
                return true;
            case "balanced":
                sort = MoveSuggestionSort.Balanced;
                return true;
            default:
                return false;
        }
    }
}

