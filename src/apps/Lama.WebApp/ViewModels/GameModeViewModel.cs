using Lama.WebApp.Services;

namespace Lama.WebApp.ViewModels;

public sealed record GameModeViewModel(
    string Slug,
    string Icon,
    string Name,
    string ShortDesc,
    string Badge,
    string Color,
    string FullDesc,
    string[] Rules,
    string[] Options,
    bool IsImplemented = true);

public static class GameModes
{
    public static readonly IReadOnlyList<GameModeViewModel> All =
    [
        new(
            Slug:      "classique",
            Icon:      "🎯",
            Name:      "Classique",
            ShortDesc: "Le scrabble revisité. 15×15, 7 tuiles, dictionnaire FR/EN/DE.",
            Badge:     "Populaire",
            Color:     "#7c5cfc",
            FullDesc:  "Le mode Classique est le cœur de Playalama : un plateau 15×15, 7 tuiles en main, des cases bonus double/triple lettre et mot. Maîtrise le vocabulaire, optimise tes placements, grimpe dans les classements.",
            Rules:     ["Plateau 15×15 cases", "7 tuiles en main", "Cases DL, TL, DM, TM", "Dictionnaire FR/EN/DE", "Fin de partie quand le sac est vide et qu'un joueur pose toutes ses tuiles"],
            Options:   ["Nombre de joueurs (2–4)", "Langue du dictionnaire", "Durée du tour (30s–10min)", "Mode classé ou libre"],
            IsImplemented: true),

        new(
            Slug:      "blitz",
            Icon:      "⚡",
            Name:      "Blitz",
            ShortDesc: "À venir — 3 minutes chrono. Pose le plus de points avant le buzzer.",
            Badge:     "Rapide • À venir",
            Color:     "#f59e0b",
            FullDesc:  "En mode Blitz (à venir), chaque seconde compte. Tu disposes de 3 minutes pour scorer un maximum de points. Les mots longs valent plus, les bonus se multiplient, mais le chrono ne s'arrête pas.",
            Rules:     ["Chronomètre global de 3 minutes", "Plateau 15×15 standard", "Les passes sont pénalisées (−2 pts)", "Le joueur avec le plus de points à la fin gagne"],
            Options:   ["Durée (1–5 min)", "Nombre de joueurs (2–4)", "Pénalité de passe"],
            IsImplemented: false),

        new(
            Slug:      "solo-ia",
            Icon:      "🤖",
            Name:      "Solo vs IA",
            ShortDesc: "Affronte une IA adaptative avec 5 niveaux de difficulté.",
            Badge:     "Solo",
            Color:     "#22c55e",
            FullDesc:  "L'IA de Playalama analyse le plateau et joue en temps réel avec jusqu'à 5 niveaux de difficulté. Parfait pour progresser, apprendre de nouveaux mots et tester des stratégies sans pression.",
            Rules:     ["1 joueur contre l'IA", "5 niveaux : Débutant → Légendaire", "L'IA adapte son niveau à tes performances", "Statistiques détaillées en fin de partie"],
            Options:   ["Niveau IA (1–5)", "Langue du dictionnaire", "Durée du tour"],
            IsImplemented: true),

        new(
            Slug:      "2v2",
            Icon:      "👥",
            Name:      "2 vs 2",
            ShortDesc: "À venir — Coopère avec un partenaire contre une autre équipe en temps réel.",
            Badge:     "Équipe • À venir",
            Color:     "#f472b6",
            FullDesc:  "Le mode 2v2 (à venir) introduit la coopération : deux joueurs partagent un score d'équipe. Communique, coordonne tes placements avec ton partenaire pour prendre l'avantage sur l'équipe adverse.",
            Rules:     ["2 équipes de 2 joueurs", "Score partagé par équipe", "Tour alternatif par équipe", "Chat d'équipe intégré"],
            Options:   ["Mode classé ou amical", "Durée du tour", "Langue du dictionnaire"],
            IsImplemented: false),

        new(
            Slug:      "grand-plateau",
            Icon:      "🗺️",
            Name:      "Grand Plateau",
            ShortDesc: "À venir — Plateau 21×21 avec zones spéciales et évènements aléatoires.",
            Badge:     "Épique • À venir",
            Color:     "#fb923c",
            FullDesc:  "Le Grand Plateau (à venir) repousse les limites : 21×21 cases, zones spéciales (bonus mobiles, zones interdites), évènements aléatoires à chaque tour. Des parties longues, imprévisibles et épiques.",
            Rules:     ["Plateau 21×21", "Zones bonus mobiles", "Évènements aléatoires (toutes les 5 tuiles posées)", "2–4 joueurs", "Durée moyenne : 45–90 min"],
            Options:   ["Fréquence des évènements", "Zones activées/désactivées", "Nombre de joueurs (2–4)"],
            IsImplemented: false),

        new(
            Slug:      "chaos",
            Icon:      "💣",
            Name:      "Chaos",
            ShortDesc: "À venir — Tuiles malus, lettres piégées, bombes de plateau. Survie pure.",
            Badge:     "Expérimental • À venir",
            Color:     "#ef4444",
            FullDesc:  "Chaos mode (à venir) : les règles normales ne s'appliquent plus. Des tuiles malus inversent les scores, des lettres piégées font sauter des cases, et des bombes reconfigurent le plateau. Seuls les plus imprévisibles survivent.",
            Rules:     ["Tuiles malus (score négatif)", "Lettres piégées (explosent au placement)", "Bombes de plateau aléatoires", "Le score peut devenir négatif"],
            Options:   ["Fréquence des tuiles malus", "Type de bombes activées", "Mode survie (éliminé à 0 pt)"],
            IsImplemented: false)
    ];

    public static GameModeViewModel? FindBySlug(string slug) =>
        All.FirstOrDefault(m => string.Equals(m.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
