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
    public void ClearError() => Error = null;

    // ── Aperçu du coup (play.check) ──────────────────────────────────────────
    public bool IsCheckLoading { get; private set; }
    public int? CheckScore { get; private set; }
    public string? CheckMessage { get; private set; }
    public void ClearCheck() { CheckScore = null; CheckMessage = null; }

    // ── Suggestions (aide-moi) ───────────────────────────────────────────────
    public bool IsSuggestLoading { get; private set; }
    public bool SuggestPanelOpen { get; private set; }
    public List<WebSuggestedMove> Suggestions { get; } = [];
    public string? SuggestError { get; private set; }
    /// <summary>Indique qu'une suggestion a été utilisée : la partie ne compte
    /// plus pour l'Elo (hors mode Tournament). Affiché à l'utilisateur.</summary>
    public bool SuggestionsUsed { get; private set; }

    public void ToggleSuggestPanel() => SuggestPanelOpen = !SuggestPanelOpen;
    public void CloseSuggestPanel() { SuggestPanelOpen = false; }

    /// <summary>Pré-remplit le formulaire de jeu avec une suggestion.</summary>
    public void UseSuggestion(WebSuggestedMove s)
    {
        Command = "play.move";
        Position = s.Position;
        Word = s.Word;
        Direction = s.Direction;
        SuggestPanelOpen = false;
    }

    /// <summary>
    /// Applique une suggestion directement en placements visuels sur le plateau (sans soumettre).
    /// Utilise les lettres disponibles du rack, avec joker si nécessaire.
    /// </summary>
    public void ApplySuggestionToPlacements(WebSuggestedMove s)
    {
        Error = null;
        if (Snapshot is null)
        {
            Error = "Partie indisponible.";
            return;
        }

        if (string.IsNullOrWhiteSpace(s.Position) || string.IsNullOrWhiteSpace(s.Word))
        {
            Error = "Suggestion invalide.";
            return;
        }

        var pos = s.Position.Trim().ToUpperInvariant();
        var col = pos[0] - 'A';
        if (col < 0 || col > 14 || !int.TryParse(pos[1..], out var row1) || row1 < 1 || row1 > 15)
        {
            Error = "Position de suggestion invalide.";
            return;
        }

        var row = row1 - 1;
        var stepRow = string.Equals(s.Direction, "V", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var stepCol = stepRow == 1 ? 0 : 1;

        RecallAll();

        var rack = MyRack.ToList();
        var available = Enumerable.Range(0, rack.Count).ToList();
        var word = s.Word.Trim().ToUpperInvariant();

        for (var i = 0; i < word.Length; i++)
        {
            var r = row + (stepRow * i);
            var c = col + (stepCol * i);
            if (r < 0 || r > 14 || c < 0 || c > 14)
            {
                RecallAll();
                Error = "Suggestion hors plateau.";
                return;
            }

            var expected = word[i];
            var boardLetter = Snapshot.Board.FirstOrDefault(t => t.Row == r && t.Column == c)?.Letter;
            if (boardLetter.HasValue)
            {
                if (char.ToUpperInvariant(boardLetter.Value) != expected)
                {
                    RecallAll();
                    Error = "Suggestion incompatible avec le plateau.";
                    return;
                }
                continue;
            }

            var rackIndex = available.FirstOrDefault(idx => char.ToUpperInvariant(rack[idx]) == expected);
            var hasExact = available.Any(idx => char.ToUpperInvariant(rack[idx]) == expected);

            if (!hasExact)
            {
                var jokerIdx = available.FirstOrDefault(idx => rack[idx] == '*');
                var hasJoker = available.Any(idx => rack[idx] == '*');
                if (!hasJoker)
                {
                    RecallAll();
                    Error = "Suggestion non jouable avec le rack actuel.";
                    return;
                }

                rackIndex = jokerIdx;
                PendingPlacements.Add(new PendingPlacement(r, c, char.ToLowerInvariant(expected), true));
                _usedRackIndices.Add(rackIndex);
                available.Remove(rackIndex);
                continue;
            }

            PendingPlacements.Add(new PendingPlacement(r, c, expected, false));
            _usedRackIndices.Add(rackIndex);
            available.Remove(rackIndex);
        }

        Command = "play.move";
        Position = string.Empty;
        Word = string.Empty;
        Direction = "H";
        SuggestPanelOpen = false;
    }

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

    /// <summary>Vrai si le joueur courant participe à cette partie (pas un observateur externe).</summary>
    public bool IsPlayerInGame => _myPlayerId is not null
        && Snapshot?.Players.Any(p => string.Equals(p.PlayerId, _myPlayerId, StringComparison.Ordinal)) == true;

    /// <summary>Vrai si le joueur courant est un observateur (non-joueur ou avoir abandonné).</summary>
    public bool IsObserver => !IsPlayerInGame || IsAbandoned;

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
        StopKeyboardMode();
    }

    // ── Mode clavier ─────────────────────────────────────────────────────────

    /// <summary>Vrai quand le mode clavier est actif (frappe directe pour poser des lettres).</summary>
    public bool KeyboardModeActive { get; private set; }

    /// <summary>Position du curseur clavier courant (case à remplir).</summary>
    public int KeyboardCursorRow { get; private set; }
    public int KeyboardCursorCol { get; private set; }

    /// <summary>Direction du mode clavier : true = horizontal, false = vertical.</summary>
    public bool KeyboardIsHorizontal { get; private set; }

    /// <summary>Nombre de tuiles posées depuis le mode clavier (pour le retour arrière).</summary>
    private int _keyboardPlacedCount;

    /// <summary>
    /// Active le mode clavier depuis la case (row, col), direction déduite des placements existants.
    /// </summary>
    public void StartKeyboardMode(int row, int col)
    {
        // Déterminer la direction depuis les placements provisoires déjà en place
        bool isHorizontal;
        if (PendingPlacements.Count > 0)
        {
            var firstPending = PendingPlacements[0];
            isHorizontal = firstPending.Row == row || PendingPlacements.All(p => p.Row == firstPending.Row);
            // Si la case cliquée est sur la même ligne → horizontal, sinon vertical
            if (firstPending.Row == row) isHorizontal = true;
            else if (firstPending.Col == col) isHorizontal = false;
            else isHorizontal = true; // fallback
        }
        else
        {
            isHorizontal = true; // par défaut horizontal si aucun placement
        }

        KeyboardModeActive = true;
        KeyboardCursorRow = row;
        KeyboardCursorCol = col;
        KeyboardIsHorizontal = isHorizontal;
        _keyboardPlacedCount = 0;
    }

    /// <summary>Place une lettre au curseur clavier, avance vers la prochaine case libre.</summary>
    /// <returns>True si la lettre a pu être posée.</returns>
    public bool HandleKeyboardLetter(char letter)
    {
        if (!KeyboardModeActive || Snapshot is null) return false;

        // Trouver l'index rack disponible pour cette lettre (ou joker)
        var rack = MyRack;
        var upperLetter = char.ToUpperInvariant(letter);
        var rackIndex = -1;

        // Chercher d'abord la lettre exacte dans le rack
        for (var i = 0; i < rack.Count; i++)
        {
            if (!_usedRackIndices.Contains(i) && char.ToUpperInvariant(rack[i]) == upperLetter)
            {
                rackIndex = i;
                break;
            }
        }

        // Sinon chercher un joker
        if (rackIndex < 0)
        {
            for (var i = 0; i < rack.Count; i++)
            {
                if (!_usedRackIndices.Contains(i) && rack[i] == '*')
                {
                    rackIndex = i;
                    break;
                }
            }
        }

        if (rackIndex < 0) return false; // pas de lettre disponible

        // Sauter les cases déjà occupées par le plateau ou un placement provisoire
        while (KeyboardCursorRow >= 0 && KeyboardCursorRow < 15
               && KeyboardCursorCol >= 0 && KeyboardCursorCol < 15)
        {
            var existsOnBoard = Snapshot.Board.Any(t => t.Row == KeyboardCursorRow && t.Column == KeyboardCursorCol);
            var existsPending = PendingPlacements.Any(p => p.Row == KeyboardCursorRow && p.Col == KeyboardCursorCol);
            if (!existsOnBoard && !existsPending) break;

            AdvanceCursor();
        }

        if (KeyboardCursorRow < 0 || KeyboardCursorRow >= 15 || KeyboardCursorCol < 0 || KeyboardCursorCol >= 15)
            return false;

        // Déterminer si c'est un joker
        var isJoker = rack[rackIndex] == '*';
        var charToPlace = isJoker ? char.ToLowerInvariant(letter) : upperLetter;

        PendingPlacements.Add(new PendingPlacement(KeyboardCursorRow, KeyboardCursorCol, charToPlace, isJoker));
        _usedRackIndices.Add(rackIndex);
        _keyboardPlacedCount++;

        AdvanceCursor();
        return true;
    }

    /// <summary>Supprime la dernière lettre posée par le clavier et recule le curseur.</summary>
    public void HandleKeyboardBackspace()
    {
        if (!KeyboardModeActive || _keyboardPlacedCount <= 0) return;

        // Supprimer le dernier placement posé via le clavier
        // On cherche depuis la fin des placements
        for (var i = PendingPlacements.Count - 1; i >= 0; i--)
        {
            // Reculer le curseur d'abord
            RetreatCursor();

            // Vérifier si cette case correspond à un placement clavier (pas un drag préexistant)
            var p = PendingPlacements[i];
            if (p.Row == KeyboardCursorRow && p.Col == KeyboardCursorCol)
            {
                _usedRackIndices.RemoveAt(i);
                PendingPlacements.RemoveAt(i);
                _keyboardPlacedCount--;
                return;
            }
        }
    }

    /// <summary>Désactive le mode clavier sans rappeler les tuiles.</summary>
    public void StopKeyboardMode()
    {
        KeyboardModeActive = false;
        _keyboardPlacedCount = 0;
    }

    /// <summary>
    /// Déplace le curseur clavier d'un delta (row, col) avec bornage 0–14.
    /// La direction (H/V) est ajustée automatiquement selon l'axe du déplacement.
    /// </summary>
    public void MoveCursor(int dRow, int dCol)
    {
        KeyboardCursorRow = Math.Clamp(KeyboardCursorRow + dRow, 0, 14);
        KeyboardCursorCol = Math.Clamp(KeyboardCursorCol + dCol, 0, 14);

        if (dCol != 0) KeyboardIsHorizontal = true;
        else if (dRow != 0) KeyboardIsHorizontal = false;
    }

    /// <summary>
    /// Supprime la tuile provisoire (du tour courant) située à la position du curseur.
    /// Ne touche jamais les tuiles confirmées des tours précédents.
    /// </summary>
    public bool HandleKeyboardDelete()
    {
        if (!KeyboardModeActive) return false;
        return RecallTile(KeyboardCursorRow, KeyboardCursorCol);
    }

    /// <summary>
    /// Avance le curseur d'une case sans poser de tuile : crée un gap intentionnel
    /// entre les tuiles du tour courant (auto-correction avant validation).
    /// </summary>
    public void HandleKeyboardInsert()
    {
        if (!KeyboardModeActive) return;
        AdvanceCursor();
    }

    /// <summary>Bascule manuellement la direction du curseur (H ↔ V).</summary>
    public void ToggleKeyboardDirection()
    {
        KeyboardIsHorizontal = !KeyboardIsHorizontal;
    }

    private void AdvanceCursor()
    {
        if (KeyboardIsHorizontal) KeyboardCursorCol++;
        else KeyboardCursorRow++;
    }

    private void RetreatCursor()
    {
        if (KeyboardIsHorizontal) KeyboardCursorCol--;
        else KeyboardCursorRow--;
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
        // Ne pas effacer Error ici : les erreurs de mouvement persistent jusqu'au dismiss manuel
        try { Snapshot = await api.GetGameAsync(GameId); }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task<bool> PlayAsync(LamaApiClient api, string token)
    {
        IsLoading = true;
        SuggestPanelOpen = false;
        Error = null;
        ClearCheck();
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
        catch (UnauthorizedAccessException) { throw; }   // laisse remonter → redirection login
        catch (Exception ex) { Error = ex.Message; return false; }
        finally { IsLoading = false; }
    }

    public async Task SuggestAsync(LamaApiClient api, string token)
    {
        IsSuggestLoading = true;
        SuggestError = null;
        Suggestions.Clear();
        SuggestPanelOpen = true;
        try
        {
            var results = await api.SuggestMovesAsync(GameId, _myPlayerId ?? string.Empty, topPerCategory: 2, token: token);
            Suggestions.AddRange(results);
            // Marquer la partie comme non comptabilisée pour l'Elo (hors Tournament)
            SuggestionsUsed = true;
        }
        catch (Exception ex)
        {
            SuggestError = ex.Message;
        }
        finally { IsSuggestLoading = false; }
    }

    /// <summary>Vérifie et calcule le score du coup en cours sans le jouer.</summary>
    public async Task CheckAsync(LamaApiClient api, string token)
    {
        if (PendingPlacements.Count == 0) return;
        IsCheckLoading = true;
        CheckScore = null;
        CheckMessage = null;
        Error = null;
        try
        {
            var placements = PendingPlacements.Select(p => new PlacementDto(p.Row, p.Col, p.Letter)).ToList();
            var result = await api.CheckMoveAsync(GameId, _myPlayerId ?? string.Empty, placements, token);
            CheckScore = result.Score;
            CheckMessage = result.Message;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { IsCheckLoading = false; }
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
