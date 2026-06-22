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
    private readonly OnlineGameGateway _onlineGameGateway;
    private readonly ILogger<PlaySuggestCommand> _logger;

    /// <summary>
    /// Constructeur de retrocompatibilite (mode local).
    /// </summary>
    public PlaySuggestCommand(
        SuggestMovesUseCase suggestMovesUseCase,
        ILogger<PlaySuggestCommand> logger)
        : this(
            suggestMovesUseCase,
            new RuntimeModeService(),
            new OnlineGameGateway(new HttpClient(), new RuntimeModeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<OnlineGameGateway>.Instance),
            logger)
    {
    }

    /// <summary>
    /// Initialise la commande.
    /// </summary>
    public PlaySuggestCommand(
        SuggestMovesUseCase suggestMovesUseCase,
        RuntimeModeService runtimeMode,
        OnlineGameGateway onlineGameGateway,
        ILogger<PlaySuggestCommand> logger)
    {
        _suggestMovesUseCase = suggestMovesUseCase;
        _runtimeMode = runtimeMode;
        _onlineGameGateway = onlineGameGateway;
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
            if (_runtimeMode.IsOnline)
            {
                var payload = new
                {
                    top,
                    sort = sort.ToString().ToLowerInvariant()
                };

                var onlineResponse = await _onlineGameGateway.PlayCommandAsync(
                    context.GameId,
                    context.PlayerId,
                    "play.suggest",
                    payload,
                    cancellationToken);

                var suggestions = onlineResponse.Suggestions?
                    .Select(s => new SuggestedMoveCandidate(
                        Word: s.Word,
                        Position: s.Position,
                        Direction: s.Direction,
                        Score: s.Score,
                        Length: s.Length,
                        BalancedScore: s.BalancedScore))
                    .ToList() ?? [];

                return WriteOutput(context.OutputFormat, suggestions);
            }

            var localResponse = await _suggestMovesUseCase.ExecuteAsync(
                new SuggestMovesRequest(context.GameId, context.PlayerId, top, sort));

            return WriteOutput(context.OutputFormat, localResponse.Suggestions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "play.suggest online indisponible");
            global::System.Console.Error.WriteLine($"[play suggest] Erreur online : {ex.Message}");
            return ExitCodes.GeneralError;
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

