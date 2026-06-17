using Lama.Console.Services;
using Lama.Contracts;
using Lama.Core.UseCases;
using Lama.Domain.Board;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Lama.Console.Commands.Show;

/// <summary>
/// Commande <c>lama show board</c> — affiche le plateau de jeu courant via Spectre.Console.
/// </summary>
public sealed class ShowBoardCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "show.board";

    private readonly CreateGameUseCase _createGameUseCase;
    private readonly ILogger<ShowBoardCommand> _logger;

    // Codes couleur des cases bonus pour l'affichage Spectre.Console
    private static readonly Dictionary<BonusType, (string bg, string label)> BonusColors = new()
    {
        [BonusType.TripleWord]   = ("red",          "TM"),
        [BonusType.DoubleWord]   = ("deeppink3",    "DM"),
        [BonusType.TripleLetter] = ("dodgerblue1",  "TL"),
        [BonusType.DoubleLetter] = ("cadetblue",    "DL"),
        [BonusType.Start]        = ("deeppink3",    "★ "),
        [BonusType.None]         = ("grey23",       "  ")
    };

    /// <summary>Initialise la commande.</summary>
    public ShowBoardCommand(CreateGameUseCase createGameUseCase, ILogger<ShowBoardCommand> logger)
    {
        _createGameUseCase = createGameUseCase;
        _logger            = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.HasActiveSession || context.GameId is null)
        {
            global::System.Console.Error.WriteLine(
                "[show board] Aucune partie active.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var engine = _createGameUseCase.GetEngine(context.GameId);
        if (engine is null)
        {
            global::System.Console.Error.WriteLine(
                "[show board] Partie introuvable en mémoire. " +
                "Note : les parties ne survivent pas au redémarrage du processus.");
            return Task.FromResult(ExitCodes.GameNotFound);
        }

        var state = engine.GetGameState();
        RenderBoard(state.Board, context.NoColor);

        return Task.FromResult(ExitCodes.Success);
    }

    private static void RenderBoard(BoardState board, bool noColor)
    {
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn(new TableColumn("  ").Centered()); // numéros de ligne

        // En-têtes colonnes A..O
        for (var c = 0; c < 15; c++)
            table.AddColumn(new TableColumn(((char)('A' + c)).ToString()).Centered());

        // Lignes 1..15
        for (var r = 0; r < 15; r++)
        {
            var cells = new List<Markup> { new Markup($"[bold]{r + 1,2}[/]") };

            for (var c = 0; c < 15; c++)
            {
                var tile  = board.Grid[r, c];
                string cell;

                if (tile is not null)
                {
                    // Lettre posée — affichage en blanc sur fond gris
                    cell = noColor
                        ? $" {tile.Letter} "
                        : $"[bold white on grey35] {tile.Letter} [/]";
                }
                else
                {
                    var bonus = BonusMap.GetBonus(r, c);
                    if (noColor)
                    {
                        cell = bonus.Type == BonusType.None ? "   " :
                               BonusColors[bonus.Type].label + " ";
                    }
                    else
                    {
                        var (bg, label) = BonusColors[bonus.Type];
                        cell = $"[on {bg}] {label}[/]";
                    }
                }

                cells.Add(new Markup(cell));
            }

            table.AddRow(cells.ToArray());
        }

        AnsiConsole.Write(table);
    }
}
