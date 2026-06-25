using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

/// <summary>Représente une tuile posée provisoirement sur le plateau (avant soumission).</summary>
public sealed record PendingPlacement(int Row, int Col, char Letter, bool IsJoker);

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
    private readonly List<int> _usedRackIndices = [];

    public string GameId { get; private set; } = string.Empty;
    public string? MyPlayerId => _myPlayerId;
    public WebGameSnapshot? Snapshot { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    // Placements provisoires (drag-and-drop)
    public List<PendingPlacement> PendingPlacements { get; } = [];

    // Formulaire action (mode texte)
    public string Command { get; set; } = "play.pass";
    public string Position { get; set; } = "H8";
    public string Word { get; set; } = string.Empty;
    public string Direction { get; set; } = "H";
    public bool SwapAll { get; set; } = false;

    // ── Propriétés calculées ─────────────────────────────────────────────────

    public bool IsMyTurn => Snapshot is { HasStarted: true, IsGameOver: false }
        && _myPlayerId is not null
        && Snapshot.CurrentPlayerIndex >= 0
        && Snapshot.CurrentPlayerIndex < Snapshot.Players.Count
        && string.Equals(
            Snapshot.Players[Snapshot.CurrentPlayerIndex].PlayerId,
            _myPlayerId, StringComparison.Ordinal)
        && !IsAbandoned;

    /// <summary>Vrai si le joueur courant a abandonné la partie (mode spectateur).</summary>
    public bool IsAbandoned => _myPlayerId is not null
        && Snapshot?.AbandonedPlayerIds.Contains(_myPlayerId) == true;

    /// <summary>Noms des joueurs qui ont abandonné, pour notification.</summary>
    public IReadOnlyList<string> AbandonedPlayerNames =>
        Snapshot?.Players
            .Where(p => Snapshot.AbandonedPlayerIds.Contains(p.PlayerId))
            .Select(p => p.PlayerName)
            .ToList() ?? [];

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

    /// <summary>Tuiles du rack disponibles (hors celles déjà posées provisoirement).</summary>
    public IReadOnlyList<(int Index, char Letter)> AvailableRackTiles
    {
        get
        {
            var rack = MyRack;
            return Enumerable.Range(0, rack.Count)
                .Where(i => !_usedRackIndices.Contains(i))
                .Select(i => (i, rack[i]))
                .ToList();
        }
    }

    /// <summary>Vérifie si un index de rack est déjà utilisé dans un placement provisoire.</summary>
    public bool IsRackIndexUsed(int idx) => _usedRackIndices.Contains(idx);

    /// <summary>Retourne le placement provisoire à (row, col) ou null.</summary>
    public PendingPlacement? GetPending(int row, int col) =>
        PendingPlacements.FirstOrDefault(p => p.Row == row && p.Col == col);

    // ── Drag-and-drop ────────────────────────────────────────────────────────

    /// <summary>Pose une tuile provisoirement sur le plateau. Retourne false si la case est occupée.</summary>
    public bool PlaceTile(int rackIndex, char letter, int row, int col)
    {
        // Case déjà occupée par une tuile confirmée
        if (Snapshot?.Board.Any(t => t.Row == row && t.Column == col) == true)
            return false;
        // Case déjà occupée par un placement provisoire
        if (PendingPlacements.Any(p => p.Row == row && p.Col == col))
            return false;

        var isJoker = letter == '*';
        PendingPlacements.Add(new PendingPlacement(row, col, letter, isJoker));
        _usedRackIndices.Add(rackIndex);
        return true;
    }

    /// <summary>Rappelle une tuile provisoire du plateau vers le rack.</summary>
    public bool RecallTile(int row, int col)
    {
        var idx = PendingPlacements.FindIndex(p => p.Row == row && p.Col == col);
        if (idx < 0) return false;

        // Retrouve l'index rack correspondant (dernier ajouté pour cette position)
        // Les indices _usedRackIndices et PendingPlacements sont ajoutés en parallèle
        _usedRackIndices.RemoveAt(idx);
        PendingPlacements.RemoveAt(idx);
        return true;
    }

    /// <summary>Déplace une tuile provisoire d'une case vers une autre.</summary>
    public bool MovePendingTile(int fromRow, int fromCol, int toRow, int toCol)
    {
        if (Snapshot?.Board.Any(t => t.Row == toRow && t.Column == toCol) == true) return false;
        if (PendingPlacements.Any(p => p.Row == toRow && p.Col == toCol)) return false;

        var idx = PendingPlacements.FindIndex(p => p.Row == fromRow && p.Col == fromCol);
        if (idx < 0) return false;

        var old = PendingPlacements[idx];
        PendingPlacements[idx] = old with { Row = toRow, Col = toCol };
        return true;
    }


    /// <summary>Rappelle toutes les tuiles provisoires.</summary>
    public void RecallAll()
    {
        PendingPlacements.Clear();
        _usedRackIndices.Clear();
    }

    /// <summary>Remplace la lettre d'un placement joker (après saisie du joueur).</summary>
    public void AssignJokerLetter(int row, int col, char chosenLetter)
    {
        var idx = PendingPlacements.FindIndex(p => p.Row == row && p.Col == col);
        if (idx < 0) return;
        var old = PendingPlacements[idx];
        // Convention : lettre minuscule = joker
        PendingPlacements[idx] = old with { Letter = char.ToLowerInvariant(chosenLetter) };
    }

    /// <summary>Retourne l'index rack d'un placement provisoire (par son ordre d'ajout).</summary>
    public int GetUsedRackIndex(int pendingIdx) =>
        pendingIdx < _usedRackIndices.Count ? _usedRackIndices[pendingIdx] : -1;

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
                Direction = Direction,
                SwapAll = SwapAll,
                // Priorité aux placements visuels si présents
                Placements = PendingPlacements.Count > 0
                    ? PendingPlacements.Select(p => new PlacementDto(p.Row, p.Col, p.Letter)).ToList()
                    : null
            };
            await api.PlayAsync(GameId, form, token);
            RecallAll(); // Vider les placements provisoires après soumission réussie
            await LoadAsync(api);
            return true;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
        finally { IsLoading = false; }
    }

    public async Task<bool> AbandonAsync(LamaApiClient api, string token)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var isGameOver = await api.AbandonAsync(GameId, _myPlayerId ?? string.Empty, token);
            await LoadAsync(api);
            return isGameOver;
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
