namespace Lama.Contracts;

/// <summary>
/// Service de gestion des profils joueurs persistants.
/// </summary>
public interface IPlayerProfileService
{
    /// <summary>
    /// Cree ou met a jour un profil joueur.
    /// </summary>
    Task<PlayerProfile> SaveAsync(PlayerProfile profile);

    /// <summary>
    /// Recupere un profil par PlayerId.
    /// </summary>
    Task<PlayerProfile?> GetByIdAsync(string playerId);

    /// <summary>
    /// Retourne tous les profils connus.
    /// </summary>
    Task<IReadOnlyList<PlayerProfile>> ListAsync();
}

