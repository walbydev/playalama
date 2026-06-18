# AGENTS.md

## Objectif pour agents IA
- Projet `Lama`: jeu de mots type Scrabble en `.NET 10`, avec CLI locale + mode online serveur.
- Priorite fiabilite: privilegier le comportement du code (`src/`, `tests/`) quand la doc diverge.

## Architecture reelle (a connaitre avant de coder)
- Couches: `Lama.Contracts` -> `Lama.Domain` -> `Lama.Core` -> `Lama.Infrastructure` -> adaptateurs `Lama.Console` et `Lama.Server`.
- Regle structurelle: pas de logique metier dans la console; ajouter regles dans `src/libs/Lama.Domain` puis exposer via use cases `src/libs/Lama.Core/UseCases`.
- `CreateGameUseCase` maintient un cache en memoire + restauration JSON inter-process (`src/libs/Lama.Core/UseCases/CreateGameUseCase.cs`).
- Le serveur online est autoritaire en memoire (`GameHubState`) avec fallback EF Core lecture (`src/Server/Lama.Server/Program.cs`).

## Flux critiques
- Mode local: commandes CLI -> use cases -> `JsonGameRepository` (`~/.config/lama/games` ou `LAMA_SESSION_DIR/games`).
- Session CLI: `SessionService` lit/ecrit `session.json`; `--game-id` et `--player` surchargent la session (`src/libs/Lama.Infrastructure/Session/SessionService.cs`, `src/Console/Lama.Console/Services/CommandContextParser.cs`).
- Mode online: `OnlineGameGateway` route `game.create/join/show`, `play.*`, `game.end` vers API HTTP (`src/Console/Lama.Console/Services/OnlineGameGateway.cs`).

## Conventions projet (specifiques)
- IDs commandes: `groupe.action` + exceptions `login/logout` + `system.account.<action>`.
- ACL centralisee via `AccessControlMiddleware` + `AccessControlService`; refus = exit code `11` (`src/Console/Lama.Console/Middleware/AccessControlMiddleware.cs`).
- Contrat IO CLI: resultats sur `stdout`, erreurs sur `stderr` (important pour `--output json|csv` et scripts).
- Option globale formatee via `CommandContext` (`--output`, `--lang`, `--no-color`, etc.).
- Convention gameplay: lettre minuscule dans `play move` force un joker (`PlayMoveCommand` + `GameEngine.ConsumeLettersFromRack`).

## Workflows utiles (rapides et fiables)
- Outils: `dotnet tool restore` (dotnet-ef est local via `dotnet-tools.json`).
- Build/tests: `dotnet build` puis `dotnet test` (ou projet cible: `dotnet test tests/Lama.Console.UnitTests`).
- CLI locale: `dotnet run --project src/Console/Lama.Console -- game create Alice`.
- Serveur online: `dotnet run --project src/Server/Lama.Server --urls http://127.0.0.1:5055`.
- Smoke online complet: `tools/scripts/e2e-online-smoke.sh` (lance host+guest, move, show.*, end).
- PostgreSQL dev: `docker compose -f docker-compose.postgresdev.yml up -d` puis `dotnet tool run dotnet-ef database update --project src/Server/Lama.Server/Lama.Server.csproj --startup-project src/Server/Lama.Server/Lama.Server.csproj --context LamaDbContext`.

## Points d'attention integration
- `LAMA_RUNTIME_MODE=online|local` et `LAMA_SERVER_URL` pilotent le routage CLI (`RuntimeModeService`).
- `LAMA_SESSION_DIR` est cle pour isolation tests et simulation multi-joueurs sur une meme machine.
- `FrenchLanguageProvider` charge les assets depuis `assets/languages/fr`; verifier le contenu est copie en sortie publish/run.
- `src/Server/Lama.Server/Program.cs` est monolithique (endpoints + DTO + state): faire des changements minimaux et couverts par tests.

## Fichiers d'entree prioritaires
- `src/Console/Lama.Console/Program.cs`
- `src/Console/Lama.Console/Services/CommandContextParser.cs`
- `src/Console/Lama.Console/Services/CommandDispatcher.cs`
- `src/libs/Lama.Core/UseCases/CreateGameUseCase.cs`
- `src/libs/Lama.Domain/Engine/GameEngine.cs`
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `tools/scripts/e2e-online-smoke.sh`

