# SCOPE

Cadre produit du projet LAMA: ce document decrit le perimetre actuel et les extensions envisagees.

## Perimetre actuel (LAMA classique)

- Plateau type Scrabble 15x15.
- Validation dictionnaire.
- Bonus de grille classiques.
- Modes CLI commande par commande et interactif.

## Extensions futures - Mode "Crazy Lama"

Objectif: proposer un mode alternatif fun/chaotique tout en gardant un mode classique stable.

### Principes directeurs

- Le mode classique reste la reference competitive.
- Les mecaniques "Crazy" sont activables par options de partie.
- Les regles doivent etre configurables, testables, et visibles dans `game.show`.
- Le mode doit rester jouable en local sans backend externe.

### Axe A - Cases dynamiques (bonus/malus aleatoires)

- Cases bonus/malus attribuees aleatoirement en debut de partie.
- Variantes de visibilite:
  - visible a tous,
  - cache a tous (revelation au moment de la pose),
  - brouillard partiel (visibilite locale autour du dernier coup).
- Types envisages:
  - `+N` points fixes,
  - `x2` ou `x3` mot/lettre,
  - `-N` points,
  - annulation de bonus classique.
- Contraintes d'equilibrage:
  - distribution symetrique autour du centre,
  - seed random persistante pour reproductibilite.

### Axe B - Cartes action

- Pioche de cartes (debut de tour, fin de tour, ou evenement).
- Cartes de boost:
  - doubler/tripler le prochain mot,
  - bonus sur les mots croises,
  - bonus si mot long (>= 6 lettres).
- Cartes de protection:
  - immunite contre une attaque,
  - annulation de malus,
  - conservation du score minimal du tour.
- Cartes d'attaque:
  - reduction de score adverse,
  - blocage temporaire (ex: pas de dictionnaire au prochain tour),
  - taxe de rack (defausse/repioche forcee).
- Regles de timing a definir:
  - carte jouable avant ou apres `play.move`,
  - limite de cartes en main,
  - anti-stack (pas plus d'une carte offensive par cible/tour).

### Axe C - Economie de points et couts d'actions

- Faire payer certaines aides/actions:
  - `play.check` payant (cout fixe ou progressif),
  - `dict.check/search/anagram` payants,
  - achat d'une lettre cible contre des points.
- Option de design:
  - couts en points purs,
  - ou couts mixtes points + perte de tour partielle.
- Objectif: creer un arbitrage risque/recompense, sans tuer le rythme.

### Axe D - Regles punitives/competitives

- Penalite sur verification abusive (check spam).
- Penalite sur challenge rate.
- Recompense sur challenge reussi.
- Mode "agressif" facultatif avec attaques directes entre joueurs.

## Configuration cible (idee API/CLI)

Exemples de flags envisageables a terme:

- `--mode crazy`
- `--crazy-cells visible|hidden|fog`
- `--crazy-cards on|off`
- `--crazy-attacks on|off`
- `--check-cost <points>`
- `--dict-cost <points>`
- `--buy-letter-cost <points>`

## Roadmap proposee

1. **MVP Crazy v1**
   - Cases dynamiques visibles + seed random persistante.
   - `play.check` payant (cout fixe).
   - Sans cartes ni attaques.

2. **MVP Crazy v2**
   - Cartes de boost + protection (pas d'attaque directe).
   - Main limitee + pioche simple.

3. **Crazy PvP avance**
   - Cartes offensives,
   - modes hidden/fog,
   - equilibrage statistique + telemetry locale.

## Backlog detaille (mode produit, sans implementation)

### Epic CL-1 - Cases dynamiques aleatoires

- **CL-1.1 (P0)** - Bonus/malus aleatoires visibles a tous
  - Story: en tant que joueur, je veux des cases speciales aleatoires pour renouveler les parties.
  - Critere d'acceptation:
    - une partie "Crazy" peut etre creee avec une seed explicite,
    - la repartition est stable pour tous les joueurs d'une meme partie,
    - les cases speciales apparaissent clairement dans les vues du plateau.

- **CL-1.2 (P1)** - Modes de visibilite `hidden` et `fog`
  - Story: en tant que joueur, je veux un niveau d'incertitude pour augmenter la tension strategique.
  - Critere d'acceptation:
    - mode `hidden`: effet revele a la pose,
    - mode `fog`: visibilite partielle autour du dernier coup,
    - mode classique inchange si option desactivee.

### Epic CL-2 - Economie de points (couts d'aides)

- **CL-2.1 (P0)** - `play.check` payant
  - Story: en tant que joueur, je peux verifier un coup, mais cela a un cout en points.
  - Critere d'acceptation:
    - cout configurable par partie (`check-cost`),
    - le cout est applique uniquement si l'action est autorisee,
    - un message explicite indique le cout preleve.

- **CL-2.2 (P1)** - Usage dictionnaire payant (`dict.check/search/anagram`)
  - Story: en tant que joueur, j'accepte de payer pour obtenir une aide dictionnaire.
  - Critere d'acceptation:
    - cout configurable (`dict-cost`),
    - gestion claire du cas "points insuffisants",
    - journalisation visible dans l'historique de partie.

- **CL-2.3 (P1)** - Achat d'une lettre contre points
  - Story: en tant que joueur, je peux acheter une lettre cible pour debloquer mon rack.
  - Critere d'acceptation:
    - cout configurable (`buy-letter-cost`),
    - la lettre est retiree du sac si disponible,
    - refus propre si la lettre n'est plus disponible.

### Epic CL-3 - Cartes action

- **CL-3.1 (P1)** - Cartes de boost (x2/x3 prochain mot)
  - Story: en tant que joueur, je veux une carte pour booster mon prochain coup.
  - Critere d'acceptation:
    - activation avant `play.move`,
    - effet consomme en un seul coup,
    - pas d'empilement de boosts multiples sur le meme coup.

- **CL-3.2 (P2)** - Cartes de protection
  - Story: en tant que joueur, je veux pouvoir annuler une attaque adverse.
  - Critere d'acceptation:
    - protection jouable en reaction,
    - la carte est consommee a l'usage,
    - priorite des resolutions definie et documentee.

- **CL-3.3 (P2)** - Cartes d'attaque
  - Story: en tant que joueur, je veux pouvoir impacter temporairement un adversaire.
  - Critere d'acceptation:
    - effets limites et bornes (pas de soft-lock),
    - garde-fou anti-abus par tour,
    - feedback explicite pour la cible et l'attaquant.

### Epic CL-4 - Regles punitives et anti-abus

- **CL-4.1 (P1)** - Penalite sur verifications abusives
  - Story: en tant que joueur, je veux eviter le spam d'aides sans contrepartie.
  - Critere d'acceptation:
    - compteur de verifications par tour/partie,
    - penalite progressive configurable,
    - desactivation possible selon mode.

- **CL-4.2 (P1)** - Penalite/recompense challenge
  - Story: en tant que joueur, je veux que le challenge ait un vrai enjeu.
  - Critere d'acceptation:
    - challenge reussi => bonus defini,
    - challenge rate => malus defini,
    - regles compatibles avec le mode classique si feature off.

## Crazy MVP v1 propose (strict backlog)

Scope cible pour un premier prototype minimal et jouable:

- `CL-1.1` Cases dynamiques visibles (seed persistante).
- `CL-2.1` `play.check` payant.
- `CL-4.2` challenge avec enjeu points minimal.

Hors scope v1:

- cartes action,
- modes `hidden`/`fog`,
- achat de lettre,
- attaques directes entre joueurs.

## Questions de produit a trancher avant kickoff v1

- Valeur par defaut des couts (`check`, challenge rate/reussi)?
- Le mode Crazy est-il classe ou non-classe?
- Quelle visibilite dans `show.history` pour les couts/bonus Crazy?
- Faut-il forcer une seed affichable pour faciliter les tests E2E?

## Statut du sujet backlog "Crazy Lama"

Le backlog Crazy est considere comme **cadre et capture termines** a date.

- Aucun ajout de nouvelle mecanique Crazy sans decision explicite ulterieure.
- Aucune implementation Crazy prioritaire tant que le mode classique n'est pas juge completement jouable et robuste.
- Le present document sert de reference unique pour les evolutions futures, sans etendre le scope court terme.

## Recentrage prioritaire (mode classique)

Objectif produit immediat: livrer un jeu classique stable, testable, et jouable de bout en bout.

- Prioriser les manques qui bloquent une partie complete en CLI/interactif.
- Renforcer les scenarios E2E de parcours reel (incluant croisements et scores).
- Stabiliser UX et feedback des commandes de tour (`play.*`, `show.*`).

## Questions ouvertes

- Le mode Crazy doit-il autoriser le classement/elo ou rester non classe?
- Combien de variance aleatoire est acceptable en mode "competitif fun"?
- Faut-il separer les decks de cartes par niveau (`Casual`, `Standard`, `Tournament`)?
- Les couts (check/dict/achat lettre) doivent-ils etre fixes ou scalables par tour?

## Definition of Done (pour valider une feature Crazy)

- Regle documentee dans `README.md` et `docs/defines-CLI.md`.
- Commande CLI exposee avec aide (`--help`) et exemples.
- Persistance JSON stable et retrocompatible.
- Tests unitaires + tests E2E CLI de scenario nominal et edge cases.
- Option desactivable pour revenir au mode classique sans impact.

