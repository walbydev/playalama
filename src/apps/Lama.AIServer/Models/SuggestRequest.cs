namespace Lama.AIServer.Models;

/// <summary>
/// Requête de suggestion envoyée par Lama.Server.
/// Contient uniquement les informations nécessaires au moteur :
/// rack du joueur et tuiles déjà posées sur le plateau.
/// </summary>
public record SuggestRequest(
    IReadOnlyList<char> Rack,
    IReadOnlyList<PlacedTileDto> PlacedTiles,
    bool IsFirstMove,
    int TopPerCategory = 2,
    int TimeoutSeconds = 15);

/// <summary>
/// Représentation sérialisable d'une tuile posée sur le plateau.
/// </summary>
public record PlacedTileDto(int Row, int Col, char Letter, bool IsWildcard = false);
