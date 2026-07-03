# AGENTS.md — Mémo IA principal

## Règle d’or
- Si la doc diverge du code, croire le code (`src/`, `tests/`).
- `Lama` = jeu de mots type Scrabble en `.NET 10`, avec CLI locale + mode online serveur.

## Architecture à retenir
- Dépendances dirigées vers le centre : `Lama.Contracts` (0 dépendance) <- `Lama.Domain` <- `Lama.Core` ; `Lama.Infrastructure` et `Lama.Languages.*` dépendent de `Contracts` + `Domain` (siblings de `Core`, pas descendants). Apps (`Lama.Console`, `Lama.Server`, `Lama.WebApp`, `Lama.AIServer`) référencent les libs selon leurs besoins.
- Pas de logique métier dans la console ; les règles vivent dans `Lama.Domain` puis sont exposées par les use cases `Lama.Core`.
- `CreateGameUseCase` gère un cache mémoire + restauration JSON inter-process.
- `Lama.Server` est autoritaire en mémoire (`GameHubState`) avec persistance async PostgreSQL (EF Core) après fin de partie. Streaming temps réel via **SSE** (Server-Sent Events), pas de SignalR. Endpoints organisés en sous-dossiers (`Games/`, `Auth/`, `Players/`, `Lexicon/`, `Bots/`).
- `Lama.WebApp` est l'unique app Blazor Server : portail public + interface de jeu en ligne. Pas de logique métier — elle appelle uniquement `Lama.Server` via HTTP (`LamaApiClient`).
- `Lama.AIServer` (port 5203) est un service HTTP séparé pour les suggestions de coups (CPU intensif) ; appelé par `Lama.Server` via `HttpAISuggestionClient`.
- Packs de langue : `Lama.Languages.fr|de|en` (assets JSON embarqués via `EmbeddedResource`). Chargés par `AssetLanguageProvider` via `LanguageProviderRegistry`.

## Organisation documentaire
- `docs/architecture/` : infra, sécurité, DB, déploiement, règles structurantes.
- `docs/roadmap/` : jalons, progress, récapitulatifs, validations, plans.
- `docs/evolutions/` : propositions d'évolution futures ; `INDEX.md` liste tous les documents.
- `docs/utils/` : quick-start, checklists, index, aides d'exploitation.
- Racine du projet : seulement `README.md` et `AGENTS.md` doivent rester en `.md`.

## Flux critiques
- Local : CLI -> use cases -> `JsonGameRepository` (`~/.config/lama/games` ou `LAMA_SESSION_DIR/games`).
- Session CLI : `SessionService` lit/écrit `session.json`; `--game-id` et `--player` surchargent la session.
- Online : `OnlineGameGateway` route `game.create/join/show`, `play.*`, `game.start/abandon/end`, `game.events` (SSE) vers l’API HTTP.

## Conventions importantes
- IDs commandes : `groupe.action` + exceptions `login/logout` + `system.account.<action>`.
- ACL : `AccessControlMiddleware` + `AccessControlService`; refus = exit code `11`.
- IO CLI : résultats sur `stdout`, erreurs sur `stderr`.
- Gameplay : lettre minuscule dans `play move` = joker forcé.

## Workflow minimal
- `dotnet tool restore`
- `dotnet build` puis `dotnet test`
- CLI locale : `dotnet run --project src/apps/Lama.Console -- game create Alice`
- Serveur : `dotnet run --project src/apps/Lama.Server --urls http://127.0.0.1:5201`
- WebApp : `dotnet run --project src/apps/Lama.WebApp --urls http://127.0.0.1:5202` (avec `LAMA_SERVER_URL=http://127.0.0.1:5201`)
- Dev complet : `make dev-debug` (Server 5201 + WebApp 5202 en parallèle)
- Smoke online : `tools/scripts/e2e/e2e-online-smoke.sh`

## Points d’attention
- `LAMA_RUNTIME_MODE=online|local` et `LAMA_SERVER_URL` pilotent le routage CLI.
- `LAMA_SESSION_DIR` est clé pour l’isolation des tests.
- `AssetLanguageProvider` charge les assets depuis `assets/languages/{lang}` (fr, de, en) ; `LanguageProviderRegistry` orchestre le multi-langue.
- `src/apps/Lama.Server/Program.cs` est volontairement concentré : changements minimaux, couverts par tests. L'état serveur vit dans `Runtime/GameHubState.cs` ; les endpoints sont répartis dans `Endpoints/{Games,Auth,Players,Lexicon,Bots}/`.
- Authentification online : JWT via `JwtTokenService` + `JwtMiddleware` ; secret signé par `LAMA_JWT_SECRET`.
- Convention de ports online/compose : Server `5201`, WebApp `5202`, AIServer `5203` (dev/staging/prod).
- **Versioning et build**: `.build-info` (JSON) sync via script vers `BuildInfoConstants.cs` ; make targets: `build-increment`, `version-set VERSION=x.y.z`, `build-generate`. `DevBanner.razor` utilise la classe statique sans HTTP.

## Modèles de jeu et classement
- Modes : `Casual` (aides activées), `Standard`, `Competitive` (challenge obligatoire), `Tournament` (règles figées).
- Rating Elo (`EloCalculator`, K=40 / K=20 au-dessus de 2400) avec files `OpenRanked`, `Tournament`, `CasualUnranked`.
- Niveaux joueurs (`LevelDeterminer`) : 7 paliers, de `NotRanked` (<1100) à `LamaEternel` (2100+).
- Bots IA : `BotCatalog` (catalogue d'IA) + `BotAutoPlayService` (boucle d'auto-jeu) ; auto-seeded au démarrage serveur.

## Fichiers d’entrée prioritaires
- `src/apps/Lama.Console/Program.cs`
- `src/apps/Lama.Console/Services/CommandContextParser.cs`
- `src/apps/Lama.Console/Services/CommandDispatcher.cs`
- `src/libs/Lama.Core/UseCases/CreateGameUseCase.cs`
- `src/libs/Lama.Domain/Engine/GameEngine.cs`
- `src/libs/Lama.Domain/Validation/MoveValidator.cs`
- `src/apps/Lama.Server/Runtime/GameHubState.cs`
- `src/apps/Lama.Server/Endpoints/Games/`
- `src/apps/Lama.WebApp/Program.cs`
- `src/apps/Lama.WebApp/Services/LamaApiClient.cs`
- `src/apps/Lama.WebApp/ViewModels/` (MVVM léger — état et logique de présentation)
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `tests/Lama.WebApp.UnitTests/LamaApiClientTests.cs`
- `tools/scripts/e2e/e2e-online-smoke.sh`

## Lama.WebApp — MVVM léger
- Les ViewModels (`src/apps/Lama.WebApp/ViewModels/`) encapsulent l’état et la logique de présentation.
- Les composants Razor ne font qu’appeler `VM.XxxAsync()` et binder sur `VM.Yyy`.
- Les ViewModels dépendent de `LamaApiClient` — jamais de `IJSRuntime`.
- `ThemeService` gère dark/light et persiste en localStorage.
- `AuthService` gère le JWT en localStorage via les fonctions JS `playalamaAuth.*`.
- Page de jeu : messages utilisateur centralisés dans la zone dédiée de la colonne droite (`game-messages-panel`).
