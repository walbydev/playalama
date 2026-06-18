namespace Lama.Domain.Rating;

using Lama.Contracts;

/// <summary>
/// Détermine le niveau et le nom du joueur basé sur son Elo.
/// </summary>
public sealed class LevelDeterminer
{
    /// <summary>
    /// Seuils Elo pour chaque niveau Lama.
    /// </summary>
    private static readonly Dictionary<LevelEnum, (double minElo, double maxElo, string name, string emoji)> 
        LevelThresholds = new()
        {
            {
                LevelEnum.JeuneLama,
                (1100, 1299, "🌱 Jeune Lama", "🌱")
            },
            {
                LevelEnum.LamaAcrobate,
                (1300, 1499, "🎪 Lama Acrobate", "🎪")
            },
            {
                LevelEnum.LamaMaitre,
                (1500, 1699, "🎋 Lama Maître", "🎋")
            },
            {
                LevelEnum.LamaSeigneur,
                (1700, 1899, "👑 Lama Seigneur", "👑")
            },
            {
                LevelEnum.LamaMythique,
                (1900, 2099, "✨ Lama Mythique", "✨")
            },
            {
                LevelEnum.LamaEternel,
                (2100, double.MaxValue, "🔥 Lama Éternel", "🔥")
            }
        };

    /// <summary>
    /// Détermine le niveau basé sur l'Elo.
    /// </summary>
    public (LevelEnum level, string name, string emoji) DetermineLevel(double elo)
    {
        if (elo < 1100)
            return (LevelEnum.NotRanked, "✨ Non classé", "✨");

        foreach (var (level, thresholds) in LevelThresholds)
        {
            if (elo >= thresholds.minElo && elo < thresholds.maxElo)
            {
                return (level, thresholds.name, thresholds.emoji);
            }
        }

        // Cas de sécurité (ne devrait pas se produire)
        return (LevelEnum.LamaEternel, "🔥 Lama Éternel", "🔥");
    }

    /// <summary>
    /// Retourne la description complète d'un niveau.
    /// </summary>
    public string GetLevelDescription(LevelEnum level)
    {
        return level switch
        {
            LevelEnum.NotRanked => "Pas encore classé",
            LevelEnum.JeuneLama => "Novice plein d'énergie, explore les stratégies",
            LevelEnum.LamaAcrobate => "S'adapte et maîtrise l'équilibre du jeu",
            LevelEnum.LamaMaitre => "Maîtrise les fondamentaux du jeu",
            LevelEnum.LamaSeigneur => "Dominateur du plateau",
            LevelEnum.LamaMythique => "Légende locale du jeu",
            LevelEnum.LamaEternel => "Hors des charts, légende vivante",
            _ => "Niveau inconnu"
        };
    }

    /// <summary>
    /// Retourne les seuils Elo pour un niveau.
    /// </summary>
    public (double min, double max)? GetLevelThresholds(LevelEnum level)
    {
        if (LevelThresholds.TryGetValue(level, out var thresholds))
        {
            return (thresholds.minElo, thresholds.maxElo);
        }

        return null;
    }

    /// <summary>
    /// Calcule la progression vers le prochain niveau (pourcentage 0-100).
    /// </summary>
    public double GetProgressToNextLevel(double elo)
    {
        var (level, _, _) = DetermineLevel(elo);

        if (level == LevelEnum.LamaEternel)
            return 100; // Déjà au maximum

        if (level == LevelEnum.NotRanked)
            return Math.Min(100, (elo / 1100) * 100);

        if (!LevelThresholds.TryGetValue(level, out var current))
            return 0;

        var nextLevel = level + 1;
        if (!LevelThresholds.TryGetValue(nextLevel, out var next))
            return 100;

        var used = elo - current.minElo;
        var range = next.minElo - current.minElo;

        return Math.Min(100, (used / range) * 100);
    }
}

