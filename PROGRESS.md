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

## [2026-06-17 21:04:35 UTC] - Parser CLI et ACL alignes + documentation harmonisee

### Contexte
- Un ecart etait identifie entre la conception CLI attendue et le comportement reel du parser.
- `login/logout` et `system.account.*` existaient cote commandes, mais n'etaient pas atteignables auparavant.

### Fait
- Parser etendu (`CommandContextParser`) pour supporter:
  - `lama login`
  - `lama logout`
  - `lama system account <create|list|revoke>`
- `CommandContext` supporte maintenant un `CommandId` explicite (fallback conserve sur `groupe.action`).
- `CommandDispatcher` affiche les erreurs sur `CommandId` reel.
- ACL ajustee pour commandes publiques auth (`login`, `logout`).
- Tests Console alignes et stabilises:
  - `Lama.Console.UnitTests` vert (149/149)
- Documentation mise a jour:
  - `docs/AGENTS.md`
  - `docs/defines-CLI.md`

### En cours
- Commandes stubs toujours presentes:
  - `game.list`, `game.show`, `game.pause`, `game.save`
  - `play.swap`, `play.challenge`, `play.check`
  - `show.history`
  - `player.create`, `tournament.create`
  - `system.status`, `system.restart`

### A faire
- Finaliser `play.swap` metier de bout en bout (au lieu du comportement transitoire).
- Introduire un historique de coups dans le coeur pour debloquer `show.history`/`challenge`.
- Ajouter des tests E2E CLI sur parcours complet.

### Risques / Ecarts
- Les docs sont maintenant alignes sur le code, mais la couverture fonctionnelle reste partielle (plusieurs commandes stubs).
- Le mode interactif est encore principalement un shell de navigation.

### Prochaines etapes
1. Implementer `game.list` et `game.show` sur repository JSON.
2. Finaliser `swap` cote `Domain/Core` puis brancher `PlaySwapCommand`.
3. Definir un premier flux jouable complet en mode interactif textuel.

### References
- `src/Console/Lama.Console/Services/CommandContextParser.cs`
- `src/Console/Lama.Console/Services/CommandContext.cs`
- `src/Console/Lama.Console/Services/AccessControlService.cs`
- `tests/Lama.Console.UnitTests/AccessControlServiceTests.cs`
- `docs/AGENTS.md`
- `docs/defines-CLI.md`

## [2026-06-17 23:11:09 UTC] - Swap complet + game.list/game.show operationnels

### Contexte
- La priorite immediate etait de traiter les deux chantiers proposes:
  1) finaliser `play.swap` de bout en bout,
  2) implémenter `game.list` et `game.show` sur la persistance existante.

### Fait
- `IGameEngine` et `GameEngine` supportent desormais un vrai echange de lettres (`SwapLetters`) avec validations metier:
  - lettres presentes dans le rack,
  - sac suffisamment fourni,
  - consommation du tour apres succes.
- `SwapLettersUseCase` branche sur la logique metier reelle (plus de fallback `PassTurn`), avec support `SwapAll`.
- `PlaySwapCommand` est implementee de bout en bout (session active, `--all`, retour rack/tour suivant, erreurs metier).
- `GameListCommand` implementee sur `IGameRepository` (sorties `text`, `json`, `csv`).
- `GameShowCommand` implementee sur `IGameRepository` (session courante ou `gameId` explicite, sorties `text`, `json`, `csv`).
- Documentation synchronisee:
  - `docs/AGENTS.md`
  - `docs/defines-CLI.md`

### En cours
- Stubs restant cote console:
  - `game.pause`, `game.save`
  - `play.challenge`, `play.check`
  - `show.history`
  - `player.create`, `tournament.create`
  - `system.status`, `system.restart`

### A faire
- Ajouter des tests unitaires dedies pour `GameListCommand` et `GameShowCommand` (formats et cas limites).
- Introduire l'historique des coups dans le coeur pour activer `show.history`/`challenge`.
- Renforcer les tests E2E CLI sur les formats `--output json/csv`.

### Risques / Ecarts
- Les commandes de listing/affichage reposent sur l'etat persiste; en cas de scenario non sauvegarde, la vue peut diverger temporairement.
- Le mode interactif reste principalement un shell de navigation.

### Prochaines etapes
1. Completer `game.save` puis `game.pause` pour fiabiliser les parcours longue duree.
2. Ajouter l'historique des coups dans le state core.
3. Cadrer une premiere batterie de tests E2E CLI (text/json/csv).

### References
- `src/libs/Lama.Contracts/IGameEngine.cs`
- `src/libs/Lama.Domain/Engine/GameEngine.cs`
- `src/libs/Lama.Core/UseCases/SwapLettersUseCase.cs`
- `src/Console/Lama.Console/Commands/Play/PlaySwapCommand.cs`
- `src/Console/Lama.Console/Commands/Game/GameListCommand.cs`
- `src/Console/Lama.Console/Commands/Game/GameShowCommand.cs`

## [2026-06-17 21:14:07 UTC] - game.save et game.pause implementes

### Contexte
- Suite du chantier CLI sur les commandes de gestion de partie encore en attente.
- Objectif: enchaîner sur `game.save` puis `game.pause`.

### Fait
- `GameSaveCommand` est maintenant branchee sur `CreateGameUseCase.SaveGame(...)`.
- `game.save` gere:
  - verification de session active,
  - sauvegarde explicite de la partie,
  - export optionnel JSON via `--file <chemin>`.
- `GamePauseCommand` est maintenant implementee comme snapshot persistant immediat de la partie courante.
- Les deux commandes mettent a jour `UpdatedAt` de la session locale.
- Documentation harmonisee:
  - `docs/AGENTS.md`
  - `docs/defines-CLI.md`

### En cours
- Stubs restants:
  - `play.challenge`, `play.check`
  - `show.history`
  - `player.create`, `tournament.create`
  - `system.status`, `system.restart`

### A faire
- Ajouter des tests unitaires dedies pour `GameSaveCommand` et `GamePauseCommand`.
- Decider d'un vrai etat "pause" metier (aujourd'hui: pause = sauvegarde immediate).

### Risques / Ecarts
- `game.pause` ne bloque pas formellement une reprise metier (pas de flag "paused" dans l'etat core).
- L'option `--file` exporte un snapshot JSON mais n'introduit pas encore un flux `game.load`.

### Prochaines etapes
1. Implementer `show.history` apres ajout d'un historique des coups dans le coeur.
2. Ouvrir `play.check`/`play.challenge` sur les APIs metier disponibles.

### References
- `src/Console/Lama.Console/Commands/Game/GameSaveCommand.cs`
- `src/Console/Lama.Console/Commands/Game/GamePauseCommand.cs`
- `docs/AGENTS.md`
- `docs/defines-CLI.md`

## [2026-06-17 21:22:40 UTC] - Couverture de tests console completee sur les commandes manquantes

### Contexte
- Une partie des commandes console nouvellement implantees n'avait encore aucun test direct.

### Fait
- Ajout d'une suite de tests console couvrant les commandes manquantes/recentes:
  - `game.create`
  - `game.join`
  - `game.list`
  - `game.show`
  - `game.save`
  - `game.pause`
  - `game.end`
  - `play.pass`
  - `play.swap`
  - `play.move`
- Ajout d'une configuration xUnit pour executer ces tests sans parallelisation d'assembly.
- Validation completee sur toute la solution.

### Resultat
- `dotnet test /home/philippe/RiderProjects/Games/Lama/Lama.slnx` : **496/496**

### Risques / Ecarts
- Les avertissements `NU1900` persistent a cause de la source NuGet interne indisponible, mais n'empechent pas les tests.

### Prochaines etapes
1. Si besoin, ajouter les tests d'erreur sur les commandes restantes (`play.check`, `play.challenge`, `show.history`).
2. Continuer la remontee de couverture sur le mode interactif.

### References
- `tests/Lama.Console.UnitTests/GameAndPlayCommandTests.cs`
- `tests/Lama.Console.UnitTests/AssemblyInfo.cs`

## [2026-06-18 00:55:00 UTC] - Documentation synchronisee + audit d'etat reel vs code

### Contexte
- La documentation ancienne affichait plusieurs commandes comme stubs alors qu'elles etaient deja implementees.
- Besoin de synchroniser l'etat reel du code avec docs/AGENTS.md et docs/defines-CLI.md.

### Fait
- **Audit de l'etat reel du code** : verifie que les commandes suivantes sont **operationnelles**:
  - `play.check` ✅ (verification de coup sans le jouer)
  - `play.challenge` ✅ (contestation du dernier mot joue)
  - `show.history` ✅ (affichage of historique des coups avec --last N et formats json/csv)
  - `game.list` ✅ (listing des parties persistees)
  - `game.show` ✅ (details d'une partie)
  - `game.save` ✅ (sauvegarde explicite)
  - `game.pause` ✅ (snapshot persistant immediat)

- **Synchronisation de docs/AGENTS.md** :
  - Retire `play.challenge`, `play.check`, `show.history` de la liste des stubs.
  - Ajoute ces trois commandes a la liste des commandes implementees.
  - Mise a jour de l'etat des composants : Domain, Core, Console marquent que support complet du challenge et historique.
  - Ajoute note sur "Tests E2E CLI" comme tache a faire.

- **Synchronisation de docs/defines-CLI.md** :
  - Marque les commandes `play.challenge`, `play.check` comme ✅ (etaient 🟡).
  - Marque `show.history` comme ✅ avec option --last N et formats (etait 🟡).

- **Tests existants** :
  - Suite Lama.Console.UnitTests : 173 tests ✅
  - Suite complete : 504 tests ✅

### En cours / A faire
- **Tests E2E CLI** : necessite architecture specifique pour les parcours complets.
- **Commandes vraiment stubs restantes** :
  - `player.create` 🟡
  - `tournament.create` 🟡
  - `system.status` 🟡  
  - `system.restart` 🟡
- **Gestion avancee des jokers** : validation et traitement complet.
- **Mode interactif textuel** : shell present, logique metier non branchee.

### Risques / Ecarts
- Tests E2E CLI tentent d'invoquer directement les commandes, mais le modele CommandContext pour les tests s'avere complexe.
- Recommandation : les vrais tests E2E devraient probablement passer par la CLI binaire plutot que l'invocation directe des APIs.

### Prochaines etapes
1. Ajouter tests E2E via processus CLI reel (bashe scripts ou processus).  
2. Evaluer l'effort pour implementer stubs restantes (player.create, tournament, system.status/restart).
3. Renforcer gestion des jokers dans le coeur (Domain).
4. Connecter davantage le mode interactif aux use cases reel.

### References
- `docs/AGENTS.md` (synchronise)
- `docs/defines-CLI.md` (synchronise)
- `tests/Lama.Console.UnitTests/` (504 tests passants)
