# AGENTS.md — Main AI Memo

## Golden Rule
- If the documentation diverges from the code, trust the code (`src/`, `tests/`).
- `Lama` is a word game inspired by Scrabble in `.NET 10`, with a local CLI and online server mode.

## Architecture to Remember
- Dependencies directed towards the center: `Lama.Contracts` (0 dependencies) <- `Lama.Domain` <- `Lama.Core`; `Lama.Infrastructure` depends on `Contracts` + `Domain`; `Lama.Languages.*` depend on `Contracts` only (siblings of `Core`, not descendants). Apps (`Lama.Console`, `Lama.Server`, `Lama.WebApp`, `Lama.AIServer`) reference the libs as needed.
- No business logic in the console; rules live in `Lama.Domain` and are then exposed by the use cases in `Lama.Core`.
- `CreateGameUseCase` manages an in-memory cache + JSON restoration across processes.
- `Lama.Server` is authoritative in memory (`GameHubState`) with async PostgreSQL persistence via EF Core after the game ends. Real-time streaming via **SSE** (Server-Sent Events), not SignalR. Endpoints are organized in subfolders (`Games/`, `Auth/`, `Players/`, `Lexicon/`) plus flat files (e.g. `BotsEndpoints.cs`).
- `Lama.WebApp` is the only Blazor Server app: a public portal + online game interface. No business logic — it calls `Lama.Server` via HTTP (`LamaApiClient`).
- `Lama.AIServer` (port 5203) is a separate HTTP service for move suggestions (CPU-intensive), called by `Lama.Server` via `HttpAISuggestionClient`.
- Language packs: `Lama.Languages.fr|de|en` (JSON assets embedded via `EmbeddedResource`). Loaded by `AssetLanguageProvider` through `LanguageProviderRegistry`.

## Documentation Organization
- `docs/architecture/` : infrastructure, security, DB, deployment, structural rules.
- `docs/roadmap/` : milestones, progress, recaps, validations, plans.
- `docs/evolutions/` : future evolution proposals; `INDEX.md` lists all documents.
- `docs/utils/` : quick-start, checklists, index, operational aids.
- `docs/bugs/` : bug investigation notes and findings.
- Project Root: ideally only `README.md` and `AGENTS.md` should remain in `.md` (though `README.fr.md` was added in v0.1.6 for French user accessibility).

## Critical Flows
- Local: CLI -> use cases -> `JsonGameRepository` (`~/.config/lama/games` or `LAMA_SESSION_DIR/games`).
- CLI Session: `SessionService` reads/writes `session.json`; `--game-id` and `--player` override the session.
- Online: `OnlineGameGateway` routes `game.create/join/show`, `play.*`, `game.start/abandon/end`, `game.events` (SSE) to the HTTP API.

## Important Conventions
- Command IDs: `group.action` + exceptions `login/logout` + `system.account.<action>`.
- ACL: `AccessControlMiddleware` + `AccessControlService`; refusal = exit code `11`.
- CLI I/O: results on `stdout`, errors on `stderr`.
- Gameplay: lowercase letter in `play move` = forced joker.

## Minimal Workflow
- `dotnet tool restore`
- `dotnet build` then `dotnet test`
- Local CLI: `dotnet run --project src/apps/Lama.Console -- game create Alice`
- Server: `dotnet run --project src/apps/Lama.Server --urls http://127.0.0.1:5201`
- WebApp: `dotnet run --project src/apps/Lama.WebApp --urls http://127.0.0.1:5202` (with `LAMA_SERVER_URL=http://127.0.0.1:5201`)
- Full development: `make dev` (Server 5201 + WebApp 5202 + AIServer 5203 in parallel, with PostgreSQL Docker)
- Online smoke test: `tools/scripts/e2e/e2e-online-smoke.sh`

## Points of Attention
- `LAMA_RUNTIME_MODE=online|local` and `LAMA_SERVER_URL` control CLI routing.
- `LAMA_SESSION_DIR` is key for test isolation.
- `AssetLanguageProvider` loads assets from `assets/languages/{lang}` (fr, de, en); `LanguageProviderRegistry` orchestrates multi-language support.
- `src/apps/Lama.Server/Program.cs` is intentionally focused: minimal changes, covered by tests. Server state lives in `Runtime/GameHubState.cs`; endpoints are distributed in `Endpoints/{Games,Auth,Players,Lexicon}/` plus flat files (e.g. `BotsEndpoints.cs`).
- Online authentication: JWT via `JwtTokenService` + `JwtMiddleware`; secret signed by `LAMA_JWT_SECRET`.
- Online/compose port convention: Server `5201`, WebApp `5202`, AIServer `5203` (dev/staging/prod).
- **Versioning and build**: `.build-info` (JSON) sync via script to `BuildInfoConstants.cs`; make targets: `release BUILD=increment`, `release VERSION=x.y.z`, `build-generate`. Build info is shown in the footer (`Footer.razor`) and a sticky bottom bar on the game page (`GameBuildBar.razor`), both using the static class without HTTP.

## Game Models and Ranking
- Modes: `Casual` (aids enabled), `Standard`, `Competitive` (mandatory challenge), `Tournament` (frozen rules), `Blitz` (per-player countdown 5/10/25 min; suggestions allowed but disable Elo).
- Elo rating (`EloCalculator`, K=40 / K=20 above 2400) with ranking queues `OpenRanked`, `Tournament`, `CasualUnranked`, `GlobalPrestige` (combo: 70% tournament + 30% open).
- Player levels (`LevelDeterminer`): 7 tiers, from `NotRanked` (<1100) to `LamaEternel` (2100+).
- AI bots: `BotCatalog` (AI catalog) + `BotAutoPlayService` (auto-play loop); auto-seeded at server startup.

## Priority Input Files
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
- `src/apps/Lama.WebApp/ViewModels/` (lightweight MVVM — state and presentation logic)
- `tests/Lama.Console.UnitTests/RealCliE2ETests.cs`
- `tests/Lama.WebApp.UnitTests/LamaApiClientTests.cs`
- `tools/scripts/e2e/e2e-online-smoke.sh`

## Lama.WebApp — Lightweight MVVM
- ViewModels (`src/apps/Lama.WebApp/ViewModels/`) encapsulate state and presentation logic.
- Razor components only call `VM.XxxAsync()` and bind on `VM.Yyy`.
- ViewModels depend on `LamaApiClient` — never on `IJSRuntime`.
- `ThemeService` manages dark/light and persists in localStorage.
- `AuthService` manages JWT in localStorage via the JS functions `playalamaAuth.*`.
- Game page: user messages centralized in the dedicated column area (`game-messages-panel`).
