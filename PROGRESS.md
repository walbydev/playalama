# PROGRESS

Journal unique de progression du projet LAMA.

## Objectif du document

- Centraliser l'etat du projet a un instant donne.
- Garder un historique des decisions et des avances par etape.
- Suivre clairement : `fait`, `en cours`, `a faire`, `risques`, `prochaines etapes`.

## Regles de mise a jour

- Ajouter une nouvelle entree a chaque jalon (ou fin de session significative).
- Conserver les entrees precedentes sans ecrasement.
- Horodatage en UTC au format `YYYY-MM-DD HH:mm:ss UTC`.
- Prioriser l'ecart entre cible documentaire et etat reel du code.

## Template d'entree

```md
## [YYYY-MM-DD HH:mm:ss UTC] - Titre du point

### Contexte
- ...

### Fait
- ...

### En cours
- ...

### A faire
- ...

### Risques / Ecarts
- ...

### Prochaines etapes
1. ...
2. ...

### References
- `README.md`
- `docs/AGENTS.md`
- `docs/console-interface-architecture.md`
```

---

## [2026-06-17 20:50:57 UTC] - Etat global initial (phase console)

### Contexte
- Projet LAMA : jeu de mots type Scrabble en C# / .NET 10.
- Architecture cible : Clean Architecture / Ports & Adapters (`Contracts` -> `Domain` -> `Core` -> `Infrastructure` -> `Console`).
- Deux modes console vises : `commande par commande` + `interactif textuel`.
- Source de verite fonctionnelle et architecturale : `README.md`, `docs/AGENTS.md`, `docs/console-interface-architecture.md`.

### Resume de la situation actuelle
- Le socle est deja plus avance que la photo de `docs/AGENTS.md` (qui mentionne encore plusieurs stubs).
- Le mode commande est partiellement operationnel avec un vrai pipeline : parse contexte, controle d'acces, dispatch commande, use cases, persistance JSON.
- Le mode interactif textuel existe (menu Spectre.Console) mais reste majoritairement un shell de navigation.
- La couverture de tests unitaires est deja substantielle sur `Domain`, `Core`, `Infrastructure`, `Console` (parser/contexte/access control).

### Fait
- **Architecture et dependances**
  - Separation en projets respectee : `Lama.Contracts`, `Lama.Domain`, `Lama.Core`, `Lama.Infrastructure`, `Lama.Console`, `Lama.Languages.fr`.
  - Config commune et packages centralises via `Directory.Build.props` et `Directory.Packages.props`.

- **Contracts**
  - Entites et interfaces presentes : etat du jeu, moteur, repository, auth, comptes, session, controle d'acces.
  - Role model detaille (`SuperAdmin`, `Admin`, `Host`, `Player`, `Spectator`) + `GameLevel`.

- **Domain**
  - Moteur de jeu implemente (`GameEngine`) : initialisation, validation de coup, jeu, passage de tour, fin de partie.
  - Validation des coups (`MoveValidator`) et calcul de score (`ScoreCalculator`) disponibles.
  - Gestion du sac (`TileBag`) et carte des bonus (`BonusMap`) implementees.

- **Core (use cases)**
  - Use cases existants : creation partie, rejoindre, jouer, passer, swap (partiel), fin de partie.
  - Mecanisme cache memoire + reconstruction depuis persistance dans `CreateGameUseCase`.

- **Infrastructure**
  - Persistance JSON des parties (`JsonGameRepository`).
  - Session locale persistante (`SessionService`).
  - Comptes et auth locales (`AccountService`, `AuthService`, `PasswordHasher` PBKDF2 + tokens HMAC).

- **Console - pipeline principal**
  - `Program.cs` configure Host/DI/logging/modes/commandes.
  - `ApplicationModeResolver` bascule entre `CommandLineMode` et `InteractiveMode`.
  - `CommandContextParser` + `CommandDispatcher` + `AccessControlMiddleware` fonctionnels.
  - Codes de retour formalises (`ExitCodes`) et messages erreurs sur `stderr`.

- **Console - commandes deja operationnelles (ou majoritairement)**
  - `game.create`, `game.join`, `game.end`
  - `play.move`, `play.pass`
  - `show.board`, `show.rack`, `show.scores`
  - `dict.check`, `dict.search`, `dict.anagram`
  - `system.setup`, `login`, `logout`, `system.account.create/list/revoke`

- **Tests**
  - Tests unitaires nombreux sur `Lama.Domain.UnitTests`, `Lama.Core.UnitTests`, `Lama.Infrastructure.UnitTests`, `Lama.Console.UnitTests`, `Lama.Languages.fr.UnitTests`.

### En cours
- **Mode interactif textuel**
  - Menu present dans `InteractiveMode`, mais handlers encore en placeholders.

- **Commandes console partiellement implementees / stubs**
  - `game.list`, `game.show`, `game.pause`, `game.save`
  - `play.swap`, `play.challenge`, `play.check`
  - `show.history`
  - `player.create`
  - `tournament.create`
  - `system.status`, `system.restart`

- **Rendering / middleware additionnels**
  - Classes presentes mais vides : `BoardRenderer`, `RackRenderer`, `ScoreRenderer`, `ThemeManager`, `AccessibilityMiddleware`, `LoggingMiddleware`, `ErrorHandlingMiddleware`.

### A faire (phase 1-2 console)
- **Stabiliser les regles metier de base**
  - Completer `swap` cote moteur/use case (aujourd'hui comportement transitoire via `PassTurn`).
  - Traiter proprement les jokers, validations avancees et historique des coups.

- **Completer les commandes prioritaires manquantes**
  - Priorite haute : `game.list`, `game.show`, `game.save`, `show.history`, `play.swap`.
  - Priorite moyenne : `play.check`, `play.challenge`, `game.pause`.

- **Alignement docs <-> code**
  - Mettre a jour `docs/AGENTS.md` (etat reel des composants, plus coherent avec le code actuel).
  - Harmoniser la spec CLI (`docs/defines-CLI.md`) avec ce qui est reellement supporte aujourd'hui.

- **Qualite / robustesse**
  - Ajouter des tests E2E CLI (parcours complet create/join/move/show/end).
  - Renforcer la serialisation JSON de sortie (`--output json`) pour eviter les concatenations manuelles fragiles.

### Risques / ecarts
- **Risque fonctionnel** : certains TODO affichent "Lama.Core absent" alors que `Core` existe deja ; cela peut induire de fausses priorites.
- **Risque produit** : la documentation vend une CLI tres large, mais une partie notable reste encore stubbee.
- **Risque UX** : le mode interactif est visible mais pas encore jouable de bout en bout.
- **Risque technique** : historique de coups absent dans le state, ce qui bloque `show.history` et des features de challenge.

### Estimation macro des phases suivantes (mode graphique)
- **Positionnement**
  - Le decouplage actuel est favorable : `Core` et `Domain` peuvent etre reutilises par une UI graphique.

- **Besoins principaux (sans detail d'implementation)**
  - Un adaptateur UI (presentation + navigation + binding d'etat).
  - Un contrat de view-model stable au-dessus des use cases.
  - Gestion asynchrone des interactions utilisateur (annulation, erreurs, feedbacks).
  - Strategie de theming/accessibilite equivalente au mode console.

- **Charge relative estimee**
  - **Pre-requis console (a finaliser avant UI graphique)** : elevee.
  - **Fondation application pour UI graphique (API interne + DTO presentation)** : moyenne a elevee.
  - **Premier MVP graphique (plateau + rack + scores + jouer/passer)** : elevee.
  - **Parite complete avec la CLI (admin, tournoi, dictionnaire avance, historique)** : tres elevee.

- **Recommandation**
  - Finaliser d'abord la phase console "jouable + persistante + testee" avant d'ouvrir la phase graphique, afin de limiter les allers-retours metier.

### Prochaines etapes recommandees
1. Finaliser `play.swap` de bout en bout (Contracts -> Domain -> Core -> Console + tests).
2. Implementer `game.list` / `game.show` sur la persistance existante.
3. Introduire un historique de coups dans le coeur pour debloquer `show.history` et `challenge`.
4. Mettre a jour `docs/AGENTS.md` et `README.md` selon l'etat reel.
5. Definir le backlog MVP du mode interactif textuel (parcours complet de jeu).

### References
- `README.md`
- `docs/AGENTS.md`
- `docs/console-interface-architecture.md`
- `docs/defines-CLI.md`

