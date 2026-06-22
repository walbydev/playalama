# AGENTS.md — Mémo IA principal

## Règle d’or
- Si la doc diverge du code, croire le code (`src/`, `tests/`).
- `Lama` = jeu de mots type Scrabble en `.NET 10`, avec CLI locale + mode online serveur.

## Architecture à retenir
- Chaîne logique : `Lama.Contracts` -> `Lama.Domain` -> `Lama.Core` -> `Lama.Infrastructure` -> `Lama.Console` / `Lama.Server`.
- Pas de logique métier dans la console ; les règles vivent dans `Lama.Domain` puis sont exposées par les use cases `Lama.Core`.
- `CreateGameUseCase` gère un cache mémoire + restauration JSON inter-process.
- `Lama.Server` est autoritaire en mémoire (`GameHubState`) avec fallback EF Core lecture dans `src/apps/Lama.Server/Program.cs`.

## Organisation documentaire
- `docs/architecture/` : infra, sécurité, DB, déploiement, règles structurantes.
- `docs/roadmap/` : jalons, progress, récapitulatifs, validations, plans.
- `docs/evolutions/` : propositions d'évolution futures ; `INDEX.md` liste tous les documents.
- `docs/utils/` : quick-start, checklists, index, aides d'exploitation.
- Racine du projet : seulement `README.md` et `AGENTS.md` doivent rester en `.md`.

## Flux critiques
- Local : CLI -> use cases -> `JsonGameRepository` (`~/.config/lama/games` ou `LAMA_SESSION_DIR/games`).
- Session CLI : `SessionService` lit/écrit `session.json`; `--game-id` et `--player` surchargent la session.
- Online : `OnlineGameGateway` route `game.create/join/show`, `play.*`, `game.end` vers l’API HTTP.

## Conventions importantes
- IDs commandes : `groupe.action` + exceptions `login/logout` + `system.account.<action>`.
- ACL : `AccessControlMiddleware` + `AccessControlService`; refus = exit code `11`.
- IO CLI : résultats sur `stdout`, erreurs sur `stderr`.
- Gameplay : lettre minuscule dans `play move` = joker forcé.

## Workflow minimal
- `dotnet tool restore`
- `dotnet build` puis `dotnet test`
- CLI locale : `dotnet run --project src/apps/Lama.Console -- game create Alice`
- Serveur : `dotnet run --project src/apps/Lama.Server --urls http://127.0.0.1:5055`
- Smoke online : `tools/scripts/e2e-online-smoke.sh`

## Points d’attention
- `LAMA_RUNTIME_MODE=online|local` et `LAMA_SERVER_URL` pilotent le routage CLI.
- `LAMA_SESSION_DIR` est clé pour l’isolation des tests.
- `FrenchLanguageProvider` charge les assets depuis `assets/languages/fr`.
- `src/apps/Lama.Server/Program.cs` est monolithique : changements minimaux, couverts par tests.

## Fichiers d’entrée prioritaires
- `src/apps/Lama.Console/Program.cs`
- `src/apps/Lama.Console/Services/CommandContextParser.cs`
- `src/apps/Lama.Console/Services/CommandDispatcher.cs`
- `src/libs/Lama.Core/UseCases/CreateGameUseCase.cs`
- `src/libs/Lama.Domain/Engine/GameEngine.cs`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `tools/scripts/e2e-online-smoke.sh`

