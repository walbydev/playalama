using System.Diagnostics;
using Lama.Contracts;
using Lama.Domain.Engine;
using Lama.Infrastructure.Lexicon;

var iterations = ParseIntArg(args, "--iterations", 60);
var warmup = ParseIntArg(args, "--warmup", 10);
var top = ParseIntArg(args, "--top", 8);

var connectionString = Environment.GetEnvironmentVariable("LAMA_LEXICON_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me";
var reader = new PostgresLexiconReader(connectionString);
await reader.EnsureSchemaAsync();
var registry = new LanguageProviderRegistry(reader, AppContext.BaseDirectory);
var provider = registry.GetProvider("fr");
var dictionary = provider.GetDictionary();
var scores = provider.GetLetterScores();

var engine = new MoveSuggestionEngine(dictionary, scores);

var firstMoveState = CreateFirstMoveState();
var midGameState = CreateMidGameState();

Console.WriteLine("MoveSuggestion benchmark");
Console.WriteLine($"Dictionary size : {dictionary.Count}");
Console.WriteLine($"Iterations      : {iterations}");
Console.WriteLine($"Warmup          : {warmup}");
Console.WriteLine($"Top             : {top}");
Console.WriteLine();

RunScenario("first-move", engine, firstMoveState, top, warmup, iterations);
RunScenario("mid-game", engine, midGameState, top, warmup, iterations);

return;

static void RunScenario(
    string name,
    MoveSuggestionEngine engine,
    (GameState State, Player Player) scenario,
    int top,
    int warmup,
    int iterations)
{
    for (var i = 0; i < warmup; i++)
        _ = engine.SuggestTopMoves(scenario.State, scenario.Player, top, MoveSuggestionStrategy.Balanced);

    var samples = new List<double>(iterations);
    var maxSuggestions = 0;

    for (var i = 0; i < iterations; i++)
    {
        var sw = Stopwatch.StartNew();
        var suggestions = engine.SuggestTopMoves(scenario.State, scenario.Player, top, MoveSuggestionStrategy.Balanced);
        sw.Stop();

        maxSuggestions = Math.Max(maxSuggestions, suggestions.Count);
        samples.Add(sw.Elapsed.TotalMilliseconds);
    }

    samples.Sort();
    var avg = samples.Average();
    var min = samples.First();
    var max = samples.Last();
    var p50 = Percentile(samples, 0.50);
    var p95 = Percentile(samples, 0.95);

    Console.WriteLine($"[{name}]");
    Console.WriteLine($"  min ms   : {min:F2}");
    Console.WriteLine($"  p50 ms   : {p50:F2}");
    Console.WriteLine($"  p95 ms   : {p95:F2}");
    Console.WriteLine($"  avg ms   : {avg:F2}");
    Console.WriteLine($"  max ms   : {max:F2}");
    Console.WriteLine($"  max hits : {maxSuggestions}");
    Console.WriteLine();
}

static double Percentile(IReadOnlyList<double> sorted, double percentile)
{
    if (sorted.Count == 0)
        return 0;

    var rank = (sorted.Count - 1) * percentile;
    var lo = (int)Math.Floor(rank);
    var hi = (int)Math.Ceiling(rank);
    if (lo == hi)
        return sorted[lo];

    var weight = rank - lo;
    return sorted[lo] * (1 - weight) + sorted[hi] * weight;
}

static int ParseIntArg(string[] args, string key, int fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (!args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
            continue;

        if (int.TryParse(args[i + 1], out var value) && value > 0)
            return value;
    }

    return fallback;
}

static (GameState State, Player Player) CreateFirstMoveState()
{
    var player = new Player("Alice", 0, ['L', 'A', 'M', 'A', 'I', 'S', '*']);
    var state = new GameState
    {
        Board = new BoardState(),
        Players = [player],
        CurrentPlayerIndex = 0,
        TurnNumber = 1,
        IsGameOver = false,
        History = []
    };

    return (state, player);
}

static (GameState State, Player Player) CreateMidGameState()
{
    var grid = new Tile?[15, 15];

    // Mot central LAMA en H8 horizontal
    grid[7, 7] = new Tile('L');
    grid[7, 8] = new Tile('A');
    grid[7, 9] = new Tile('M');
    grid[7, 10] = new Tile('A');

    // Extension verticale pour créer des ancres
    grid[6, 8] = new Tile('R');
    grid[8, 8] = new Tile('I');
    grid[9, 8] = new Tile('E');

    var player = new Player("Alice", 42, ['T', 'E', 'S', 'O', 'N', 'U', '*']);
    var state = new GameState
    {
        Board = new BoardState(grid),
        Players = [player],
        CurrentPlayerIndex = 0,
        TurnNumber = 7,
        IsGameOver = false,
        History = []
    };

    return (state, player);
}
