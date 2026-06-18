namespace Lama.Contracts;

/// <summary>
/// Profil persistant d'un joueur (identite + donnees optionnelles remplies par le joueur).
/// </summary>
/// <param name="PlayerId">Identifiant technique unique du joueur (GUID N).</param>
/// <param name="DisplayName">Nom affiche principal.</param>
/// <param name="Pseudo">Pseudo public optionnel.</param>
/// <param name="Country">Pays optionnel (texte libre court).</param>
/// <param name="Region">Region optionnelle (texte libre court).</param>
/// <param name="BirthYear">Annee de naissance optionnelle.</param>
/// <param name="CreatedAt">Date de creation du profil.</param>
/// <param name="UpdatedAt">Date de derniere mise a jour du profil.</param>
public record PlayerProfile(
    string PlayerId,
    string DisplayName,
    string? Pseudo = null,
    string? Country = null,
    string? Region = null,
    int? BirthYear = null,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset UpdatedAt = default)
{
    /// <summary>
    /// Nom prefere pour affichage public (pseudo si defini, sinon nom principal).
    /// </summary>
    public string PublicName => string.IsNullOrWhiteSpace(Pseudo) ? DisplayName : Pseudo!;
}

