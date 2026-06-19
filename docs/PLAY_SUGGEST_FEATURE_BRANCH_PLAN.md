# Plan de fonctionnalite: `play.suggest` (assistant de prochain coup)

## 1) Objectif

Ajouter une nouvelle fonctionnalite `play.suggest` pour proposer les meilleurs prochains coups a partir d'un etat de partie:

- top N mots les plus longs;
- top N coups les plus payants en points;
- evaluation orientee "bonus/malus futurs" (heuristique de risque/opportunite), utile pour preparer un futur joueur machine.

Cette fonctionnalite doit rester coherente avec l'architecture existante:

`Lama.Contracts -> Lama.Domain -> Lama.Core -> Lama.Infrastructure -> Lama.Console / Lama.Server`

---

## 2) Constat sur l'existant (regard neuf)

Le socle actuel est favorable:

- validation de coup disponible via `MoveValidator` (`src/libs/Lama.Domain/Validation/MoveValidator.cs`);
- calcul de score disponible via `ScoreCalculator` (`src/libs/Lama.Domain/Scoring/ScoreCalculator.cs`);
- carte bonus officielle via `BonusMap` (`src/libs/Lama.Domain/Board/BonusMap.cs`);
- dictionnaire charge via `FrenchLanguageProvider` (`src/libs/Lama.Languages.fr/FrenchLanguageProvider.cs`);
- execution locale et online deja structuree (`CreateGameUseCase`, `PlayMoveUseCase`, `GamesCommandEndpoints`, `OnlineGameGateway`).

Points a noter:

- le projet gere aujourd'hui les bonus (DL/TL/DW/TW), pas de notion explicite de "malus" au sens regle de score;
- `play.check` existe deja et valide/simule un coup sans mutation (excellent point d'entree conceptuel);
- ACL centralisee deja en place (`AccessControlService`, `AccessControlMiddleware`), donc ajout simple de politique d'acces.

---

## 3) Portee fonctionnelle proposee (MVP)

### Commande CLI

Nouvelle commande:

- `lama play suggest [--top <n>] [--sort score|length|balanced] [--output text|json|csv]`

Comportement MVP:

- propose des coups jouables pour le joueur courant;
- retourne par defaut top 2;
- `sort=score`: maximise les points immediats;
- `sort=length`: maximise la longueur du mot puis score;
- `sort=balanced`: score immediat + heuristique bonus/malus futurs.

### Sortie minimale d'une suggestion

- mot propose;
- position (`H8`, etc.);
- direction (`H|V`);
- score estime;
- longueur;
- details optionnels (`bonusConsommes`, `riskIndex`, `rackLeave`).

---

## 4) Integration architecture (propre dans l'existant)

### 4.1 Domaine (`Lama.Domain`)

Ajouter un moteur dedie, sans logique CLI:

- fichier propose: `src/libs/Lama.Domain/Engine/MoveSuggestionEngine.cs`

Responsabilites:

- generer des candidats depuis rack + plateau;
- valider chaque candidat via `MoveValidator`;
- scorer via `ScoreCalculator`;
- calculer des metriques complementaires (longueur, opportunite bonus, risque ouverture, leave rack);
- retourner un classement deterministe.

Modeles proposes (Domain/Contracts selon design final):

- `SuggestedMove`;
- `MoveSuggestionRequest`;
- `MoveSuggestionMode` (`Score`, `Length`, `Balanced`).

### 4.2 Core (`Lama.Core`)

Ajouter un use case:

- `src/libs/Lama.Core/UseCases/SuggestMovesUseCase.cs`

Responsabilites:

- recuperer session via `CreateGameUseCase`;
- verifier joueur courant (meme pattern que `PlayMoveUseCase`);
- appeler `MoveSuggestionEngine`;
- ne pas muter la partie;
- renvoyer une reponse exploitable localement et online.

### 4.3 Console (`Lama.Console`)

Ajouter commande:

- `src/Console/Lama.Console/Commands/Play/PlaySuggestCommand.cs`

Mises a jour associees:

- enregistrement DI dans `src/Console/Lama.Console/Program.cs`;
- ACL dans `src/Console/Lama.Console/Services/AccessControlService.cs`;
- aide dans `src/Console/Lama.Console/Services/HelpCatalog.cs`;
- verifier coherence avec `tests/Lama.Console.UnitTests/HelpCatalogConsistencyTests.cs`.

### 4.4 Server (`Lama.Server`) - online

Etendre le switch de commandes dans:

- `src/Server/Lama.Server/Endpoints/Games/GamesCommandEndpoints.cs`

Ajouter `case "play.suggest"`:

- parse payload (`top`, `sort`);
- execute suggestion sans mutation;
- renvoie liste des suggestions.

Client HTTP:

- reutiliser `OnlineGameGateway.PlayCommandAsync(...)` (pas de nouvel endpoint obligatoire au MVP).

---

## 5) Heuristique bonus/malus futurs (version pragmatique)

Comme il n'existe pas de "malus" natif dans les regles implementees, on introduit une metrique d'evaluation pour l'IA:

- **bonus futurs (opportunite)**: valeur des cases premium encore disponibles et accessibles par extension;
- **malus futurs (risque)**: ouverture de lignes offrant TW/TL a l'adversaire au tour suivant;
- **leave rack**: qualite des lettres conservees (equilibre voyelles/consonnes, lettres tres lourdes, joker conserve).

Score "balanced" propose:

`balanced = scoreImmediate + alpha * leaveQuality - beta * opponentOpeningRisk + gamma * premiumControl`

Le MVP peut commencer avec des coefficients fixes (`alpha/beta/gamma`) puis etre affine via tests.

---

## 6) Plan de livraison par PR (recommande)

### PR1 - Fondations domaine/core

- `MoveSuggestionEngine` + modeles;
- `SuggestMovesUseCase`;
- tests unitaires Domain/Core.

### PR2 - CLI locale

- `PlaySuggestCommand`;
- DI + ACL + HelpCatalog;
- tests console unitaires.

### PR3 - Online

- `play.suggest` cote server;
- adaptation output cote CLI online;
- tests e2e online.

### PR4 - Qualite IA (optionnel)

- heuristique `balanced` plus riche;
- optimisation perf (caching ancres, prefixes dictionnaire, pruning);
- benchmark simple.

---

## 7) Strategie Git (branche dediee)

Oui, il est recommande de creer une branche dediee maintenant, meme si `master` n'est pas finalisee.

Benefices:

- isoler un chantier algorithmique sensible;
- faciliter revue incrementalement (PR par etape);
- eviter de melanger avec stabilisation de `master`;
- permettre rebase regulier pour rester a jour.

Convention proposee:

- branche: `feature/play-suggest-ai`
- PRs: `feature/play-suggest-ai` -> `master` (ou branche integration selon workflow equipe)

Commandes utiles:

```bash
git checkout -b feature/play-suggest-ai
git push -u origin feature/play-suggest-ai
```

Rebase regulier:

```bash
git fetch origin
git rebase origin/master
```

---

## 8) Tests et criteres d'acceptation

### Tests a ajouter

- Domain:
  - suggestions premier coup (obligation H8);
  - respect jokers (minuscule => joker force);
  - tri `score` et tri `length` deterministes;
  - non-mutation de l'etat.
- Core:
  - refus si ce n'est pas le tour du joueur;
  - top N borne.
- Console:
  - `play.suggest` text/json/csv;
  - erreurs sur stderr, data sur stdout.
- Online:
  - `play.suggest` via `PlayCommandAsync`;
  - coherence des resultats local/online sur meme etat.

### Definition of Done (DoD)

- commande `play.suggest` disponible en local et online;
- ACL + aide + tests de coherence catalogue passes;
- aucune mutation involontaire de partie;
- performances raisonnables sur plateau standard (latence interactive acceptable);
- documentation utilisateur minimale dans README ou docs CLI.

---

## 9) Risques et mitigations

- explosion combinatoire des coups:
  - mitigation: generation par ancres + prefix pruning + top-K incremental.
- divergence local/online:
  - mitigation: reposer sur meme logique domain/core, tests miroirs.
- dette sur `Program.cs` serveur monolithique:
  - mitigation: changements minimaux et testes (conforme contraintes projet).

---

## 10) Suite vers joueur machine

`play.suggest` est la brique ideale pour un bot:

- phase 1: bot "reactif" joue le meilleur `score`;
- phase 2: bot "balanced" (vision a 1 coup adverse);
- phase 3: recherche limitee (beam/minimax simplifie) en reemployant `MoveSuggestionEngine`.

Ainsi, l'investissement dans cette fonctionnalite sert directement le futur joueur machine sans dupliquer la logique metier.

