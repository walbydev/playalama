namespace Lama.Contracts;

/// <summary>
/// Représente l'état complet d'une partie persistée.
/// Contient tout ce qui est nécessaire pour reconstruire un <c>GameEngine</c>
/// après un redémarrage du processus.
/// </summary>
/// <param name="GameId">Identifiant unique de la partie.</param>
/// <param name="Language">Code de langue (ex: "fr").</param>
/// <param name="GameLevel">Niveau de la partie.</param>
/// <param name="IsFirstMove">Indique si aucun coup n'a encore été joué.</param>
/// <param name="IsGameOver">Indique si la partie est terminée.</param>
/// <param name="CurrentPlayerIndex">Index du joueur dont c'est le tour.</param>
/// <param name="TurnNumber">Numéro du tour courant.</param>
/// <param name="Players">Liste des joueurs avec leur score et leur rack.</param>
/// <param name="Board">Grille du plateau : liste de tuiles positionnées.</param>
/// <param name="RemainingTiles">Lettres restantes dans le sac.</param>
/// <param name="History">Historique des coups joués.</param>
/// <param name="LastMoveSnapshot">Snapshot permettant d'annuler le dernier coup en cas de challenge réussi.</param>
/// <param name="CreatedAt">Date de création de la partie (UTC).</param>
/// <param name="UpdatedAt">Date de dernière modification (UTC).</param>
public record PersistedGame(
    string                    GameId,
    string                    Language,
    GameLevel                 GameLevel,
    bool                      IsFirstMove,
    bool                      IsGameOver,
    int                       CurrentPlayerIndex,
    int                       TurnNumber,
    List<PersistedPlayer>     Players,
    List<PersistedTile>       Board,
    List<char>                RemainingTiles,
    DateTimeOffset            CreatedAt,
    DateTimeOffset            UpdatedAt,
    List<GameMove>            History = null!,
    GameStateSnapshot?        LastMoveSnapshot = null);

/// <summary>
/// Représente un joueur persisté (nom, score, rack).
/// </summary>
/// <param name="PlayerId">Identifiant du joueur dans la session.</param>
/// <param name="Name">Nom d'affichage du joueur.</param>
/// <param name="Score">Score courant.</param>
/// <param name="Rack">Lettres dans le rack du joueur.</param>
public record PersistedPlayer(
    string     PlayerId,
    string     Name,
    int        Score,
    List<char> Rack);

/// <summary>
/// Représente une tuile placée sur le plateau (position + lettre).
/// Évite le problème de sérialisation des tableaux 2D de <see cref="BoardState"/>.
/// </summary>
/// <param name="Row">Ligne (0..14).</param>
/// <param name="Col">Colonne (0..14).</param>
/// <param name="Letter">La lettre posée.</param>
/// <param name="IsWildcard">True si c'est un joker.</param>
public record PersistedTile(int Row, int Col, char Letter, bool IsWildcard = false);

/// <summary>
/// Référentiel de persistance des parties.
/// Abstraction permettant de changer le backend de stockage (JSON → SQLite)
/// sans modifier les cas d'usage.
///
/// Chemin typique pour l'implémentation JSON :
/// <list type="bullet">
///   <item>Linux   : <c>~/.config/lama/games/{gameId}.json</c></item>
///   <item>Windows : <c>%APPDATA%\lama\games\{gameId}.json</c></item>
///   <item>macOS   : <c>~/Library/Application Support/lama/games/{gameId}.json</c></item>
/// </list>
/// </summary>
public interface IGameRepository
{
    /// <summary>
    /// Sauvegarde l'état complet d'une partie.
    /// Crée le fichier s'il n'existe pas, l'écrase s'il existe.
    /// </summary>
    void Save(PersistedGame game);

    /// <summary>
    /// Charge l'état d'une partie par son identifiant.
    /// Retourne <c>null</c> si la partie n'existe pas ou si le fichier est corrompu.
    /// </summary>
    PersistedGame? Load(string gameId);

    /// <summary>
    /// Supprime la partie persistée (fin de partie ou abandon).
    /// Silencieux si la partie n'existe pas.
    /// </summary>
    void Delete(string gameId);

    /// <summary>
    /// Retourne la liste des identifiants de toutes les parties persistées.
    /// </summary>
    IReadOnlyList<string> ListGameIds();

    /// <summary>
    /// Indique si une partie avec cet identifiant est persistée.
    /// </summary>
    bool Exists(string gameId);
}
