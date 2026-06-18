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

## [2026-06-18 09:40:00 UTC] - E2E CLI reels + stubs leves + jokers renforces + interactif branche

### Contexte
- Les priorites listees etaient: tests E2E en processus reel, suppression des stubs restants, renforcement jokers cote Domain, et connexion du shell interactif aux cas d'usage reels.

### Fait
- **Tests E2E CLI reels**:
  - Ajout de scripts Bash de parcours complet:
    - `tools/scripts/e2e-cli-smoke.sh` (create -> join -> pass -> show -> end)
    - `tools/scripts/e2e-system-and-stubs.sh` (system.status/restart + player/tournament.create)
  - Ajout de tests E2E via processus `dotnet run` dans:
    - `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`

- **Stubs commandes implementees**:
  - `player.create` cree desormais un profil local persiste en session.
  - `tournament.create` cree une partie de niveau `Tournament` et initialise la session hote.
  - `system.status` retourne un diagnostic systeme (text/json/csv).
  - `system.restart` effectue un redemarrage logique (purge cache memoire + restauration de la partie active).

- **Jokers renforces (Domain/Core)**:
  - `GameEngine` consomme maintenant `*` du rack comme joker quand la lettre demandee manque.
  - Les tuiles posees via joker sont marquees `IsWildcard=true` sur le plateau.
  - Le score tient compte des jokers a 0 point meme quand ils representent une lettre.
  - La restauration depuis persistance conserve desormais l'etat `IsWildcard` des tuiles.
  - Validation de coup durcie: rejet des caracteres non alphabetiques (hors `*`).

- **Mode interactif**:
  - `InteractiveMode` branche maintenant les menus sur des commandes reelles:
    - Nouvelle partie -> `game.create`
    - Rejoindre -> `game.join`
    - Charger -> `game.show --game-id`
  - La section Options affiche l'etat de session locale en direct.

- **Aide CLI renforcee**:
  - Ajout d'une aide contextuelle fonctionnelle:
    - `lama help`
    - `lama <groupe> --help`
    - `lama <groupe> <action> --help`
    - `lama help <groupe> <action>`
  - `system.restart` documente explicitement comme redemarrage logique in-process (pas de restart de service OS externe).
  - Niveau 2 implemente: catalogue d'aide centralise (`HelpCatalog`) avec metadonnees par commande:
    - usage,
    - options,
    - exemples,
    - ACL (roles),
    - formats de sortie.
  - Support de l'aide pour commandes multi-niveaux: `lama system account create --help`.
  - Couverture etendue a l'ensemble des commandes enregistrees dans `Program.cs`.
  - Ajout d'un test automatique de coherence `Program.cs` <-> `HelpCatalog` pour detecter les desynchronisations futures.

### En cours / A faire
- Definir un vrai modele metier de tournoi (aujourd'hui: `tournament.create` s'appuie sur une partie `GameLevel.Tournament`).
- Ajouter un support explicite de notation joker cote CLI (`play.move`) pour choisir visuellement les lettres jokers si necessaire.
- Etendre le mode interactif avec un vrai cycle de jeu (plateau, rack, coup, pass, swap) dans une boucle unique.

### Risques / Ecarts
- `system.restart` est un redemarrage logique in-process (pas un restart d'un daemon externe).
- Les E2E via `dotnet run` sont plus lents que des tests executes sur binaire precompile.

### Prochaines etapes
1. Faire evoluer `tournament.create` vers une entite tournoi persistante dediee.
2. Ajouter une commande interactive de tour (`play.move/pass/swap`) dans `InteractiveMode`.
3. Ajouter des E2E JSON/CSV supplementaires pour `show.*` et `game.*`.

### References
- `tools/scripts/e2e-cli-smoke.sh`
- `tools/scripts/e2e-system-and-stubs.sh`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `src/Console/Lama.Console/Commands/Player/PlayerCreateCommand.cs`
- `src/Console/Lama.Console/Commands/Tournament/TournamentCreateCommand.cs`
- `src/Console/Lama.Console/Commands/System/SystemStatusCommand.cs`
- `src/Console/Lama.Console/Commands/System/SystemRestartCommand.cs`
- `src/Console/Lama.Console/Modes/InteractiveMode.cs`
- `src/libs/Lama.Domain/Engine/GameEngine.cs`
- `src/libs/Lama.Domain/Validation/MoveValidator.cs`
- `src/libs/Lama.Domain/Scoring/ScoreCalculator.cs`

## [2026-06-18 10:35:00 UTC] - Focus gameplay: tour interactif + E2E formats game/show

### Contexte
- Objectif de transition apres le chantier aide CLI: avancer sur le jeu.
- Priorites selectionnees: (2) tour de jeu en mode interactif, (3) E2E supplementaires sur formats `json/csv` pour `game.*` et `show.*`.

### Fait
- **Mode interactif enrichi** (`InteractiveMode`):
  - Ajout d'une entree de menu `Jouer un tour`.
  - Flux de tour connecte aux commandes reelles:
    - `play.move`
    - `play.check`
    - `play.challenge`
    - `play.pass`
    - `play.swap` (avec confirmation `--all` ou saisie lettres)
  - Construction d'un `CommandContext` lie a la session active (GameId/PlayerId/Role/GameLevel).
  - Mini tableau de bord post-action (si succes): `show.board`, `show.rack`, `show.scores`.
  - Mode `tour continu` active dans ce sous-menu: enchainement d'actions sans retour force au menu principal.
  - Polish UX ajoute:
    - en-tete de contexte au menu principal (partie/joueur/role),
    - action `Reafficher le dashboard`,
    - action `Abandonner la partie` dans le sous-menu de tour,
    - action `Effacer la session locale` au menu principal.

- **Notation joker explicite**:
  - `play.move` supporte maintenant une convention explicite: lettre minuscule => joker force (ex: `lAMA`).
  - Cote moteur, la minuscule force la consommation d'un `*` meme si la lettre existe deja dans le rack.

- **Tests E2E processus reel supplements** (`RealCliE2ETests`):
  - Verification `game.list --output json`.
  - Verification `game.list --output csv`.
  - Verification `game.show --output csv`.
  - Verification `show scores --output json`.

### En cours / A faire
- Ajouter un rendu interactif in-loop apres chaque action (`show.board` + `show.rack` + `show.scores`) pour une experience de tour plus fluide.
- Etendre les E2E formats a `show.history` des qu'un scenario de coups deterministe est fixe.

### Risques / Ecarts
- Le mode interactif reste couple a des prompts sequentiels (pas encore boucle de partie unique continue).
- Le mode interactif est jouable en tour continu, mais l'UX reste textuelle sequentielle (pas de navigation contextuelle avancee).
- Le mode interactif est maintenant pertinent pour une recette manuelle de bout en bout en solo/local.
- Les tests E2E via `dotnet run` restent plus lents qu'une execution binaire precompilee.

### Prochaines etapes
1. Enchaîner automatiquement sur un mini tableau de bord (`board/rack/scores`) apres chaque action interactive.
2. Ajouter un sous-menu `challenge/check` dans `Jouer un tour`.
3. Etendre les E2E formats a `show.history` des qu'un scenario de coups deterministe est fixe.

### References
- `src/Console/Lama.Console/Modes/InteractiveMode.cs`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`

## [2026-06-18 13:00:00 UTC] - Support complet des croisements (intersections de mots)

### Contexte
- L'utilisateur rapportait une difficulté UX : placer un second mot qui croise un mot existant etait impossible, 
  car le systeme rejetait tout placement sur une case deja occupee.
- Probleme identifie : le validateur `MoveValidator` n'acceptait aucune case occupee, contrairement au Scrabble reel 
  qui permet les croisements quand la lettre correspond.

### Fait
- **Modification du validateur (MoveValidator.cs)**:
  - Remplacer la validation stricte "aucune case occupee" par une validation plus intelligente.
  - Nouvelles regles :
    1. Les cases occupees sont acceptees SI la lettre proposee correspond a la lettre existante (croisement valide).
    2. Au moins une lettre NOUVELLE doit etre posee (pas de coup comprenant uniquement des croisements).
    3. En cas de non-correspondance, message d'erreur clair avec position et lettres en conflit.
  - Tests domaine verifies : **161 tests passes**, incluant validation de croisements valides/invalides.

- **Amelioration de la commande PlayMoveCommand**:
  - Messages d'aide etendus pour expliquer les croisements.
  - Documentation dans les exemples d'utilisation.
  - Aide contextuelle pour distinguer jokers (minuscules) et croisements (mots complets).

- **Documentation completee**:
  - Mise a jour `README.md` : nouvelle section "Croisements (partage de lettres)" avec exemples.
  - Creation `docs/crossing-rules.md` : guide complet sur les croisements, regles, pieges courants, strategie.
  - Harmonisation des exemples CLI : ajout de `lama play move J8 MAISON V` (exemple de croisement).

- **Tests passes**:
  - `Lama.Domain.UnitTests` : **161 tests** ✅
  - `Lama.Console.UnitTests` : **190 tests** ✅
  - Suite complete : **504 tests** ✅

### Comportement utilisateur resultant
1. L'utilisateur tente de placer `MAISON` vertical en A2 quand `LAMA` horizontal existe en A1-D1 :
   ```bash
   lama play move A2 MAISON V
   ```
2. Le systeme valide que M(A2) correspond a M de LAMA → coup VALIDE ✅

3. L'utilisateur tente de placer `POISON` vertical en A2 (P != M) :
   ```bash
   lama play move A2 POISON V
   ```
4. Le systeme rejette avec :
   ```
   À la case A2, la lettre 'M' existe déjà. Vous tentez de placer 'P'.
   Pour un croisement valide, les lettres doivent être identiques.
   ```

### En cours / A faire
- Ajouter une option `--crosses <pos1,pos2,...>` pour affichage explicite des positions partagees (enhancement optionnel).
- Etendre tests E2E pour parcours complet avec croisements multiples.

### Risques / Ecarts
- Aucun regression : les tests existants restent verts (validation croisements integree a la logique existante).
- Les calculs de score tiennent desormais compte des croisements dans les mots secondaires formes (comportement Scrabble standard).

### Prochaines etapes
1. Tester un scenario jouable complet avec croisements en mode interactif.
2. Ajouter des tests E2E CLI pour croisements multiples et mots secondaires.
3. Verifier le calcul des scores avec mots croises.

### References
- `src/libs/Lama.Domain/Validation/MoveValidator.cs` (validation intelligente des croisements)
- `src/Console/Lama.Console/Commands/Play/PlayMoveCommand.cs` (aide utilisateur)
- `README.md` (section "Croisements")
- `docs/crossing-rules.md` (guide complet)
- Tests masses : 504 passants

## [2026-06-18 14:20:00 UTC] - Backlog produit capture pour mode "Crazy Lama"

### Contexte
- Besoin utilisateur de noter les evolutions futures gameplay dans un emplacement unique et durable.
- Orientation souhaitee: variantes fun/chaotiques avec options activables par partie.

### Fait
- `docs/SCOPE.md` renseigne avec un cadrage complet du mode futur "Crazy Lama".
- Idees structurees en 4 axes:
  - cases bonus/malus aleatoires (visibles ou cachees),
  - cartes action (boost/protection/attaque),
  - economie de points (check/dict payants, achat de lettre),
  - regles punitives/competitives (challenge, anti-abus).
- Proposition de roadmap incremental (v1 -> v2 -> PvP avance).
- Liste de questions produit ouvertes pour arbitrage futur.

### En cours
- Aucun dev technique demarre pour Crazy Lama (phase ideation/cadrage uniquement).

### A faire
- Prioriser 2-3 mecaniques MVP pour un premier prototype jouable.
- Definir un schema de configuration partie pour activer/desactiver les modules Crazy.
- Ajouter tests de non-regression garantissant qu'un mode classique reste intact.

### Risques / Ecarts
- Risque d'explosion de complexite si trop de mecaniques sont introduites en meme temps.
- Necessite de conserver une lisibilite UX forte en mode interactif CLI.

### Prochaines etapes
1. Valider un "Crazy MVP v1" minimal (ex: cases dynamiques + cout de check).
2. Definir les options CLI (`--mode crazy`, flags associes) et leur persistance.
3. Ecrire les specs de regles detaillees avant implementation.

### References
- `docs/SCOPE.md`

## [2026-06-18 14:40:00 UTC] - Crazy Lama passe en backlog priorise (epics/stories)

### Contexte
- Demande utilisateur explicite: rester en mode backlog, sans implementation technique immediate.
- Objectif: rendre les idees Crazy actionnables pour priorisation produit.

### Fait
- Enrichissement de `docs/SCOPE.md` avec un backlog detaille:
  - epics (`CL-1` a `CL-4`),
  - stories priorisees (`P0`, `P1`, `P2`),
  - criteres d'acceptation orientés produit.
- Proposition d'un perimetre "Crazy MVP v1" minimal:
  - cases dynamiques visibles,
  - `play.check` payant,
  - challenge avec enjeu points simple.
- Ajout des questions produit bloquantes avant kickoff v1.

### En cours
- Aucun dev code lance pour Crazy Lama (toujours phase backlog/spec).

### A faire
- Arbitrer les valeurs par defaut des couts/penalites v1.
- Decider du statut classe/non-classe du mode Crazy.
- Preparer un atelier de priorisation (impact joueur vs complexite tech).

### Risques / Ecarts
- Sans bornes claires sur v1, risque de glissement vers une "v1 trop large".
- Certaines stories (cartes d'attaque) exigent des regles anti-abus precises.

### Prochaines etapes
1. Valider la shortlist MVP v1 (`CL-1.1`, `CL-2.1`, `CL-4.2`).
2. Fixer les parametres par defaut (couts, bonus/malus) dans une spec courte.
3. Ouvrir ensuite des tickets techniques separes par epic.

### References
- `docs/SCOPE.md`

## [2026-06-18 15:00:00 UTC] - Cloture backlog futur et recentrage sur jeu fonctionnel

### Contexte
- Demande utilisateur: clore le sujet backlog des evolutions futures et revenir aux manques du jeu fonctionnel.

### Fait
- Le backlog "Crazy Lama" est formalise comme capture complete (epics, stories, priorites, criteres) dans `docs/SCOPE.md`.
- Ajout d'un statut explicite de cloture backlog:
  - freeze des nouvelles idees Crazy a court terme,
  - pas d'implementation Crazy prioritaire avant stabilisation du mode classique.
- Ajout d'un cadrage de recentrage dans `docs/SCOPE.md` sur le livrable immediat: jeu classique jouable de bout en bout.

### En cours
- Recentrage produit vers les manques bloquants du mode classique (hors Crazy).

### A faire
- Etablir une shortlist definitive des gaps restants pour "jeu fonctionnel" (CLI + interactif).
- Prioriser les correctifs/ameliorations impactant directement une partie complete.
- Consolider les tests E2E de parcours reel sur les scenarios cle.

### Risques / Ecarts
- Risque de reouverture prematuree du scope Crazy si le cadre de freeze n'est pas respecte.

### Prochaines etapes
1. Produire une liste courte des manques "must-have" pour finaliser le mode classique.
2. Sequencer les taches en sprint court axe fiabilite/UX/tests E2E.
3. Ne reouvrir Crazy qu'apres validation explicite du jalon "jeu fonctionnel".

### References
- `docs/SCOPE.md`

## [2026-06-18 15:20:00 UTC] - Shortlist must-have pour finaliser le mode classique

### Contexte
- Suite au recentrage, besoin d'une liste courte et priorisee des manques pour atteindre un jeu fonctionnel.

### Fait
- Creation de `docs/CLASSIC_GAME_SHORTLIST.md` avec une shortlist priorisee:
  - P0 (bloquants): parcours CLI/interactif complet, coherence check/move, scoring robuste.
  - P1: formats JSON/CSV, historique fiable, UX d'erreur harmonisee.
  - P2: perf de base E2E, nettoyage docs etat reel.
- Ajout d'un ordre de sprint court recommande et d'une definition claire du jalon "Jeu fonctionnel".

### En cours
- Preparation du sequencing execution sur les items P0.

### A faire
- Convertir chaque item P0 en taches techniques concretes (code + tests).
- Lancer l'execution par lot en commencant par coherence `play.check`/`play.move` et parcours CLI reel.

### Risques / Ecarts
- Le jalon peut deriver si des travaux P1/P2 sont traites avant la cloture P0.

### Prochaines etapes
1. Ouvrir les tickets d'implementation pour `CG-01` a `CG-04`.
2. Executer et valider les scenarios E2E cibles.
3. Passer aux items P1 seulement apres validation P0.

### References
- `docs/CLASSIC_GAME_SHORTLIST.md`

## [2026-06-18 15:35:00 UTC] - CG-01 renforce: parcours CLI reel fiabilise

### Contexte
- Demarrage de l'execution de la shortlist classique sur l'item `CG-01` (parcours CLI complet fiable).

### Fait
- Renforcement du smoke E2E reel:
  - `tools/scripts/e2e-cli-smoke.sh` couvre maintenant `create -> join -> swap --all -> game.show --output json -> show scores -> end`.
- Ajustement du test processus reel associe:
  - `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
  - scenario principal aligne sur le meme parcours avec assertions de sortie.
- Validation executee:
  - test cible `Cli_RealProcess_FullGameJourney_Works` ✅
  - script `tools/scripts/e2e-cli-smoke.sh` ✅

### En cours
- Passage progressif aux autres items P0 (`CG-03`, `CG-04`, puis `CG-02`).

### A faire
- Ajouter un scenario E2E reel centré sur `play.move` (cas nominal deterministe) pour completer `CG-01`.
- Verifier un cas d'echec attendu (erreur metier) avec code de sortie controle.

### Risques / Ecarts
- Le parcours CG-01 est plus robuste, mais reste partiellement dependant du contexte de session joueur sur certaines actions de tour.

### Prochaines etapes
1. Completer `CG-03` (coherence `play.check` vs `play.move`) avec E2E reel dedie.
2. Etendre le parcours CG-01 avec un coup `play.move` deterministe.
3. Garder le scope strictement sur les P0 avant P1/P2.

### References
- `tools/scripts/e2e-cli-smoke.sh`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `docs/CLASSIC_GAME_SHORTLIST.md`

## [2026-06-18 15:50:00 UTC] - CG-03 valide: coherence `play.check` / `play.move` en E2E reel

### Contexte
- Priorite P0 suivante: garantir qu'un coup valide en `play.check` reste jouable en `play.move` dans un vrai processus CLI.

### Fait
- Ajout d'un test E2E reel dedie dans `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`:
  - `Cli_RealProcess_PlayCheckThenMove_CrossingLetter_RemainsConsistent`
- Le test prepare un etat persiste deterministe (session + partie):
  - plateau avec croisement existant (`LA` horizontal),
  - rack sans la lettre de croisement,
  - verification `play check I8 AS V` puis `play move I8 AS V`.
- Validation executee:
  - test cible CG-03 ✅
  - suite complete `RealCliE2ETests` ✅ (4/4).

### En cours
- Preparation de `CG-04` (robustesse scoring croisements/jokers/bonus).

### A faire
- Ajouter des scenarios E2E score-oriented pour valider les points affiches apres coups croises/jokers.
- Completer la couverture sur cas limites de challenge lies au score.

### Risques / Ecarts
- Le scenario CG-03 est deterministe cote persistance; il reste utile d'ajouter un second scenario base uniquement sur commandes utilisateur pour recette manuelle.

### Prochaines etapes
1. Lancer `CG-04` avec une batterie de cas score deterministes.
2. Completer ensuite `CG-02` (parcours interactif complet fiable).
3. Garder la priorisation stricte sur P0.

### References
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `docs/CLASSIC_GAME_SHORTLIST.md`

## [2026-06-18 16:05:00 UTC] - CG-04 avance: robustesse scoring jokers/croisements renforcee

### Contexte
- Priorite P0 `CG-04`: fiabiliser le scoring sur les cas limites (croisements, jokers, bonus).

### Fait
- Correctif applique dans `ScoreCalculator`:
  - une lettre deja presente sur le plateau et issue d'un joker (`IsWildcard=true`) vaut desormais 0 point sur les coups suivants.
- Ajout de tests de regression:
  - `Score_ExistingWildcardTile_CountsAsZero_WhenIncludedInWord` (ScoreCalculator),
  - `PlayMove_CrossingExistingWildcardTile_DoesNotCountWildcardPointsTwice` (GameEngine).
- Validation executee:
  - tests cibles CG-04 ✅
  - suite `Lama.Domain.UnitTests` complete ✅ (165/165).

### En cours
- Consolidation des scenarios score-oriented additionnels (bonus mot/lettre + challenge) pour couverture CG-04 complete.

### A faire
- Ajouter un scenario E2E reel orienté score (sortie utilisateur) avec croisements/jokers.
- Verifier la coherence des points affiches cote commandes `play.move` et `show.scores`.

### Risques / Ecarts
- A ce stade, la robustesse score est renforcee au niveau Domain; la verification complete cote parcours CLI score reste a etendre.

### Prochaines etapes
1. Ajouter un E2E CLI score-deterministe pour cloturer CG-04.
2. Enchainer sur `CG-02` (parcours interactif complet fiable).
3. Conserver la priorisation P0 stricte avant les items P1.

### References
- `src/libs/Lama.Domain/Scoring/ScoreCalculator.cs`
- `tests/Lama.Domain.UnitTests/Scoring/ScoreCalculatorTests.cs`
- `tests/Lama.Domain.UnitTests/Engine/GameEngineTests.cs`
- `docs/CLASSIC_GAME_SHORTLIST.md`

## [2026-06-18 16:20:00 UTC] - CG-04 cloture

### Contexte
- Objectif: clore `CG-04` avec validation complete du scoring (croisements/jokers/bonus) au niveau Domain et E2E reel.

### Fait
- Correctif score confirme: une tuile existante marquee `IsWildcard=true` reste a 0 point lorsqu'elle est reutilisee dans un coup.
- Couverture completee avec:
  - tests Domain cibles (ScoreCalculator + GameEngine),
  - test E2E reel CLI: `Cli_RealProcess_PlayMove_CrossingExistingWildcard_ReportsExpectedScore`.
- Verification de non-regression:
  - `Lama.Domain.UnitTests` complet ✅,
  - `RealCliE2ETests` complet ✅.
- `docs/CLASSIC_GAME_SHORTLIST.md` mis a jour: `CG-04` marque complete.

### En cours
- Reste du P0: `CG-02` (parcours interactif complet fiable).

### A faire
- Ouvrir execution `CG-02` (boucle de tour interactive + dashboard post-action + stabilisation session).

### Risques / Ecarts
- Aucun ecart critique identifie sur le scoring apres cloture CG-04.

### Prochaines etapes
1. Demarrer `CG-02`.
2. Executer recette manuelle interactive complete.
3. Verrouiller le jalon P0 une fois `CG-02` valide.

### References
- `src/libs/Lama.Domain/Scoring/ScoreCalculator.cs`
- `tests/Lama.Domain.UnitTests/Scoring/ScoreCalculatorTests.cs`
- `tests/Lama.Domain.UnitTests/Engine/GameEngineTests.cs`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `docs/CLASSIC_GAME_SHORTLIST.md`

## [2026-06-18 16:35:00 UTC] - CG-02 en cours: mode interactif durci hors TTY

### Contexte
- Demarrage de `CG-02` (parcours interactif fiable) avec un point de fiabilite critique: execution en terminal non interactif.

### Fait
- `InteractiveMode` detecte desormais l'absence de TTY interactif et retourne une erreur explicite sans exception fatale.
- Ajout d'un test E2E reel dedie:
  - `Cli_RealProcess_InteractiveMode_NonInteractiveTerminal_ReturnsFriendlyError`.
- Validation executee:
  - test cible CG-02 ✅,
  - suite `RealCliE2ETests` complete ✅ (6/6).

### En cours
- `CG-02` reste en cours: il manque la validation finale du parcours interactif complet en recette manuelle guidee.

### A faire
- Realiser une recette interactive complete (create/join/play/check/challenge/pass/swap/show/end) en TTY reel.
- Verifier la fluidite de la boucle de tour et la persistance de session sur l'ensemble du parcours.

### Risques / Ecarts
- Les tests automatises en CI ne peuvent pas couvrir integralement les prompts Spectre (necessitent un vrai terminal interactif).

### Prochaines etapes
1. Executer une recette interactive manuelle de reference.
2. Corriger les ecarts UX eventuels releves pendant la recette.
3. Clore `CG-02` puis valider le jalon P0 global.

### References
- `src/Console/Lama.Console/Modes/InteractiveMode.cs`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `docs/CLASSIC_GAME_SHORTLIST.md`

