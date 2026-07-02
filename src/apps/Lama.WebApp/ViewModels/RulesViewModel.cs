namespace Lama.WebApp.ViewModels;

public sealed class RulesViewModel
{
    public IReadOnlyList<RulesSection> Sections { get; } =
    [
        new("common", "📖 Règles communes", "Les fondamentaux qui s'appliquent à tous les modes de jeu.",
        [
            new("Le plateau",           "Le plateau est composé de cases normales et de cases bonus : ×2 lettre (DL), ×3 lettre (TL), ×2 mot (DM), ×3 mot (TM). La case centrale est une DM."),
            new("Les tuiles",           "Chaque tuile porte une lettre et une valeur en points. Les lettres rares (Z, K, X…) valent plus. La tuile vierge (joker) vaut 0 point mais peut représenter n'importe quelle lettre."),
            new("Constituer un mot",    "Un mot doit s'appuyer sur au moins une tuile déjà posée sur le plateau (sauf le premier tour). Il doit être présent dans le dictionnaire de la langue choisie."),
            new("Dictionnaire",         "Tous les mots acceptés sont issus du Wiktionnaire (via Kaikki). Ce corpus inclut les noms communs, mais aussi les noms de lieux, de régions, de langues, d'abréviations, les formes conjuguées, etc. Un mot introuvable dans un dictionnaire papier traditionnel peut donc être valide ici, et inversement."),
            new("Joker",                "En CLI, une lettre minuscule dans la commande play move force l'utilisation du joker. En Web, un bouton permet de désigner le joker."),
            new("Échange de tuiles",    "Tu peux échanger jusqu'à 7 tuiles contre des tuiles du sac, mais ça compte comme un tour (0 point)."),
            new("Fin de partie",        "La partie se termine quand le sac est vide et qu'un joueur pose toutes ses tuiles, ou quand tous les joueurs passent deux fois de suite. Les tuiles restantes en main sont soustraites du score."),
        ]),

        new("classique", "🎯 Mode Classique", "Le scrabble revisité avec des variantes modernes.",
        [
            new("Format",               "15×15, 7 tuiles en main, sac de 102 tuiles (FR), 2–4 joueurs."),
            new("Premier tour",         "Le premier mot doit passer par la case centrale (H8)."),
            new("Bingo",                "Poser ses 7 tuiles en un seul tour rapporte 50 points bonus."),
            new("Dictionnaire",         "Les mots doivent appartenir au dictionnaire de la langue sélectionnée (FR, EN, DE)."),
        ]),

        new("blitz", "⚡ Mode Blitz (À venir)", "La vitesse prime sur la stratégie.",
        [
            new("Chrono",               "3 minutes pour la partie entière. Chaque joueur joue dès que c'est son tour, le chrono ne s'arrête pas."),
            new("Passe pénalisé",       "Passer son tour coûte 2 points."),
            new("Fin de partie",        "Quand le chrono atteint zéro, on comptabilise les scores. Les tuiles en main ne sont pas soustraites."),
            new("Status",               "⚠️ Ce mode sera bientôt disponible."),
        ]),

        new("solo-ia", "🤖 Mode Solo vs IA", "Perfectionne ton jeu face à l'IA.",
        [
            new("Niveaux IA",           "5 niveaux : Débutant (joue des mots courts), Intermédiaire, Avancé, Expert, Légendaire (utilise des stratégies d'anticipation)."),
            new("Adaptation",           "L'IA Légendaire s'adapte à ton style de jeu sur la durée."),
            new("Statistiques",         "Fin de partie : analyse de tes mots manqués, suggestions d'amélioration."),
        ]),

        new("2v2", "👥 Mode 2 vs 2 (À venir)", "La coopération comme arme principale.",
        [
            new("Équipes",              "2 équipes de 2 joueurs. Le score est la somme des scores des deux partenaires."),
            new("Ordre de jeu",         "L'ordre alterne entre équipes : Équipe A (J1) → Équipe B (J1) → Équipe A (J2) → Équipe B (J2)."),
            new("Communication",        "Un chat d'équipe privé est disponible pendant la partie (non visible par l'adversaire)."),
            new("Status",               "⚠️ Ce mode sera bientôt disponible."),
        ]),

        new("grand-plateau", "🗺️ Grand Plateau (À venir)", "Des parties épiques sur un espace élargi.",
        [
            new("Taille",               "21×21 cases. Plus de place pour les stratégies longues distance."),
            new("Zones spéciales",      "Des zones bonus mobiles apparaissent et disparaissent au fil des tours."),
            new("Évènements aléatoires","Toutes les 5 tuiles posées, un évènement peut survenir : doublement d'une zone, annulation d'un mot, bonus surprise…"),
            new("Status",               "⚠️ Ce mode sera bientôt disponible."),
        ]),

        new("chaos", "💣 Mode Chaos (À venir)", "Quand le plateau devient un champ de mines.",
        [
            new("Tuiles malus",         "Certaines tuiles dans le sac ont une valeur négative. Tu ne peux voir leur type qu'après les avoir posées."),
            new("Lettres piégées",      "Certaines lettres sur le plateau explosent si un mot les inclut, supprimant les tuiles adjacentes."),
            new("Bombes de plateau",    "À intervalles aléatoires, une zone du plateau est détruite et ses tuiles retournent dans le sac."),
            new("Score négatif",        "Un score peut descendre en dessous de zéro — y compris le vainqueur final."),
            new("Status",               "⚠️ Ce mode sera bientôt disponible."),
        ]),
    ];

    public string? ActiveSectionId { get; private set; }

    public void SetActiveSection(string? id) => ActiveSectionId = id;
}

public sealed record RulesSection(
    string Id,
    string Title,
    string Intro,
    IReadOnlyList<RulesRule> Rules);

public sealed record RulesRule(string Title, string Text);
