using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

/// <summary>
/// Encapsule l'état d'une partie en cours (plateau, rack, scores, actions).
/// Instancié directement dans Game.razor — pas via DI.
/// </summary>
public sealed class GamePlayViewModel
{
    // ── BonusMap (miroir de BonusMap.cs côté domaine) ────────────────────────

    private static readonly HashSet<(int, int)> TwCells =
    [
        (0,0),(0,7),(0,14),(7,0),(7,14),(14,0),(14,7),(14,14)
    ];

    private static readonly HashSet<(int, int)> DwCells =
    [
        (1,1),(2,2),(3,3),(4,4),(10,10),(11,11),(12,12),(13,13),
        (1,13),(2,12),(3,11),(4,10),(10,4),(11,3),(12,2),(13,1)
    ];

    private static readonly HashSet<(int, int)> TlCells =
    [
        (1,5),(1,9),(5,1),(5,5),(5,9),(5,13),(9,1),(9,5),(9,9),(9,13),(13,5),(13,9)
    ];

    private static readonly HashSet<(int, int)> DlCells =
    [
        (0,3),(0,11),(2,6),(2,8),(3,0),(3,7),(3,14),
        (6,2),(6,6),(6,8),(6,12),(7,3),(7,11),
        (8,2),(8,6),(8,8),(8,12),(11,0),(11,7),(11,14),
        (12,6),(12,8),(14,3),(14,11)
    ];

    private static readonly Dictionary<char, int> TileValues = new()
    {
        ['A']=1,['B']=3,['C']=3,['D']=2,['E']=1,['F']=4,['G']=2,['H']=4,
        ['I']=1,['J']=8,['K']=10,['L']=1,['M']=2,['N']=1,['O']=1,['P']=3,
        ['Q']=8,['R']=1,['S']=1,['T']=1,['U']=1,['V']=4,['W']=10,['X']=10,
        ['Y']=10,['Z']=10,['*']=0
    };

    // ── État ─────────────────────────────────────────────────────────────────

    private string? _myPlayerId;

    public string GameId { get; private set; } = string.Empty;
    public WebGameSnapshot? Snapshot { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    // Formulaire action
    public string Command { get; set; } = "play.pass";
    public string Position { get; set; } = "H8";
    public string Word { get; set; } = string.Empty;
    public string Direction { get; set; } = "H";

    // ── Propriétés calculées ─────────────────────────────────────────────────

    public bool IsMyTurn => Snapshot is { HasStarted: true, IsGameOver: false }
        && _myPlayerId is not null
        && Snapshot.CurrentPlayerIndex >= 0
        && Snapshot.CurrentPlayerIndex < Snapshot.Players.Count
        && string.Equals(
            Snapshot.Players[Snapshot.CurrentPlayerIndex].PlayerId,
            _myPlayerId, StringComparison.Ordinal);

    public IReadOnlyList<char> MyRack
    {
        get
        {
            if (Snapshot is null || _myPlayerId is null) return [];
            var me = Snapshot.Players.FirstOrDefault(p =>
                string.Equals(p.PlayerId, _myPlayerId, StringComparison.Ordinal));
            return me?.Rack ?? [];
        }
    }

    // ── Init ─────────────────────────────────────────────────────────────────

    public void Initialize(string gameId, string? myPlayerId)
    {
        GameId = gameId;
        _myPlayerId = myPlayerId;
    }

    // ── API calls ─────────────────────────────────────────────────────────────

    public async Task LoadAsync(LamaApiClient api)
    {
        IsLoading = true;
        Error = null;
        try { Snapshot = await api.GetGameAsync(GameId); }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task<bool> PlayAsync(LamaApiClient api, string token)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var form = new PlayForm
            {
                PlayerId = _myPlayerId ?? string.Empty,
                Command = Command,
                Position = Position,
                Word = Word,
                Direction = Direction
            };
            await api.PlayAsync(GameId, form, token);
            await LoadAsync(api);
            return true;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
        finally { IsLoading = false; }
    }

    // ── BonusMap helpers ─────────────────────────────────────────────────────

    /// <summary>Retourne le code CSS de la case bonus (tw/dw/tl/dl/st) ou "".</summary>
    public string GetCellBonus(int row, int col)
    {
        if (row == 7 && col == 7) return "st";
        if (TwCells.Contains((row, col))) return "tw";
        if (DwCells.Contains((row, col))) return "dw";
        if (TlCells.Contains((row, col))) return "tl";
        if (DlCells.Contains((row, col))) return "dl";
        return string.Empty;
    }

    /// <summary>Retourne la tuile posée à (row, col) ou null.</summary>
    public char? GetTile(int row, int col) =>
        Snapshot?.Board.FirstOrDefault(x => x.Row == row && x.Column == col)?.Letter;

    /// <summary>Valeur FR d'une tuile.</summary>
    public static int GetTileValue(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        return TileValues.TryGetValue(upper, out var val) ? val : 0;
    }

    /// <summary>Libellé de la colonne (A..O).</summary>
    public static string ColLabel(int col) => ((char)('A' + col)).ToString();

    /// <summary>Libellé de la ligne (1..15).</summary>
    public static string RowLabel(int row) => (row + 1).ToString();
}
