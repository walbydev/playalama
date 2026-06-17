using Lama.Contracts;

namespace Lama.Core.Models;

// ═══════════════════════════════════════════════════════════════════════════
// Requêtes (entrées des cas d'usage)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Requête pour créer une nouvelle partie.
/// </summary>
/// <param name="HostPlayerName">Nom de l'hôte (créateur de la partie).</param>
/// <param name="Language">Code de langue (ex: "fr"). Défaut : "fr".</param>
/// <param name="BoardSize">Taille du plateau (défaut : 15).</param>
/// <param name="RackSize">Taille du rack par joueur (défaut : 7).</param>
/// <param name="MinWordLength">Longueur minimale d'un mot (défaut : 2).</param>
public record CreateGameRequest(
    string    HostPlayerName,
    string    Language      = "fr",
    GameLevel GameLevel     = GameLevel.Standard,
    int       BoardSize     = 15,
    int       RackSize      = 7,
    int       MinWordLength = 2);

/// <summary>
/// Requête pour qu'un joueur rejoigne une partie existante.
/// </summary>
/// <param name="GameId">Identifiant de la partie.</param>
/// <param name="PlayerName">Nom du joueur qui rejoint.</param>
public record JoinGameRequest(
    string GameId,
    string PlayerName);

/// <summary>
/// Requête pour jouer un coup.
/// </summary>
/// <param name="GameId">Identifiant de la partie.</param>
/// <param name="PlayerId">Identifiant du joueur.</param>
/// <param name="Letters">Lettres posées sur le plateau (Position → lettre).</param>
public record PlayMoveRequest(
    string GameId,
    string PlayerId,
    Dictionary<Position, char> Letters);

/// <summary>
/// Requête pour passer le tour.
/// </summary>
/// <param name="GameId">Identifiant de la partie.</param>
/// <param name="PlayerId">Identifiant du joueur.</param>
public record PassTurnRequest(
    string GameId,
    string PlayerId);

/// <summary>
/// Requête pour échanger des lettres avec le sac.
/// </summary>
/// <param name="GameId">Identifiant de la partie.</param>
/// <param name="PlayerId">Identifiant du joueur.</param>
/// <param name="Letters">Lettres à échanger.</param>
/// <param name="SwapAll">True pour échanger tout le rack du joueur courant.</param>
public record SwapLettersRequest(
    string GameId,
    string PlayerId,
    IReadOnlyList<char>? Letters = null,
    bool SwapAll = false);

/// <summary>
/// Requête pour terminer la partie.
/// </summary>
/// <param name="GameId">Identifiant de la partie.</param>
public record EndGameRequest(string GameId);

// ═══════════════════════════════════════════════════════════════════════════
// Réponses (sorties des cas d'usage)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Réponse à la création d'une partie.
/// </summary>
/// <param name="GameId">Identifiant unique de la partie créée.</param>
/// <param name="HostPlayerId">Identifiant du joueur hôte.</param>
/// <param name="InitialState">État initial du plateau.</param>
public record CreateGameResponse(
    string    GameId,
    string    HostPlayerId,
    GameState InitialState);

/// <summary>
/// Réponse quand un joueur rejoint une partie.
/// </summary>
/// <param name="PlayerId">Identifiant attribué au joueur.</param>
/// <param name="Rack">Rack initial du joueur.</param>
/// <param name="GameState">État courant de la partie.</param>
public record JoinGameResponse(
    string     PlayerId,
    List<char> Rack,
    GameState  GameState);

/// <summary>
/// Réponse après un coup joué.
/// </summary>
/// <param name="Score">Points marqués avec ce coup.</param>
/// <param name="NewRack">Nouveau rack du joueur (après recharge).</param>
/// <param name="GameState">Nouvel état de la partie.</param>
public record PlayMoveResponse(
    int        Score,
    List<char> NewRack,
    GameState  GameState);

/// <summary>
/// Réponse après échange de lettres.
/// </summary>
/// <param name="NewRack">Nouveau rack après échange.</param>
/// <param name="GameState">Nouvel état de la partie.</param>
public record SwapLettersResponse(
    List<char> NewRack,
    GameState  GameState);

/// <summary>
/// Réponse à la fin de partie.
/// </summary>
/// <param name="FinalState">État final de la partie.</param>
/// <param name="Winner">Nom du gagnant (ou null si égalité).</param>
/// <param name="Scores">Scores finaux triés par rang.</param>
public record EndGameResponse(
    GameState              FinalState,
    string?                Winner,
    IReadOnlyList<(string PlayerName, int Score)> Scores);
