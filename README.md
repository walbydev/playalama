# LAMA

LAMA is a word game inspired by Scrabble, developed in C# / .NET 10.
Players place words on a grid, scoring points based on letter values and bonus squares, competing until the letter bag is empty.

## License

This project is distributed under the **GNU Affero General Public License v3.0 or later (AGPL-3.0-or-later)**.

- The reference file is `LICENSE` at the root of the repository.
- AGPL ensures that forks and modified versions exposed over the network remain free.
- Donations to fund infrastructure are compatible with this license.

The game is available through several interfaces:

- A **console** (CLI) with two modes: **command line** (one-off actions, scripts, tests) and **textual interactive** (menus, prompts, enriched display);
- A **Blazor Server WebApp** (`Lama.WebApp`): public portal + online game interface;
- An **API server** (`Lama.Server`): centralized online play, rankings, SSE streaming.

---

## Game Rules

### The board

- Square board **15×15** by default (configurable from 15 to 26)
- Coordinates: letter = column (A–O), number = row (1–15) — e.g., `H8` for center square
- Bonus squares (classic Scrabble style): letter and word multipliers

### Tiles

- Each letter has a point value defined by the language dictionary
- **2 jokers** by default — they can represent any letter (0 pt value)
- The rack contains **7 letters** by default

### Game turn

At each turn, the active player must make one of these actions:

| Action | Command |
|--------|----------|
| Place a word on the board | `lama play move <square> <word> <direction>` |
| Pass turn | `lama play pass` |
| Exchange letters for the bag | `lama play swap <letters>` |
| Challenge the last played word | `lama play challenge` |

### Word placement

- The **first word** must pass through the starting square, by default the center `H8`
- Direction: `H` for horizontal or `V` for vertical
- Each placed word must connect to existing letters, except for the first word
- All formed words, main and crossings, must be in the dictionary
- Minimum length: **2 letters** by default

### Crossings (shared letters)

When you place a word that crosses an existing word:

1. **Specify the complete word**, including the crossing letter
2. **The letter must match** the one already on the board
3. The system automatically validates crossings

**Example**: If `LAMA` is already horizontal on H8, you can play `MAISON` vertically on J8:
```bash
lama play move J8 MAISON V
```
In this case, the `M` of `MAISON` (in J8) crosses with the `M` of `LAMA` — this is valid.

If you attempt `MAISON` on J8 with an incompatible `M`, the system rejects the placement with a clear message.

### End of game

The game ends when:

- the bag is empty **and** a player empties their rack;
- the maximum number of turns is reached, via `--max-turns`;
- the maximum score is reached;
- a player manually ends the game with `lama game end`.

---

## Architecture

The project follows the **Clean Architecture / Ports & Adapters** pattern.

The core principle is that game logic does not depend on any user interface.
All interfaces (console, WebApp, server) call the same application services.

### Components

```text
Apps
├── Lama.Console   — CLI (command-line + interactive mode)
├── Lama.Server    — Authoritative HTTP API (in-memory state, async PostgreSQL persistence post-game)
├── Lama.WebApp    — Blazor Server (portal + game interface, lightweight MVVM)
└── Lama.AIServer  — HTTP service for move suggestions (port 5203)

Libs
├── Lama.Contracts       — types and interfaces (0 dependencies)
├── Lama.Domain           — game rules (GameEngine, MoveValidator, ScoreCalculator)
├── Lama.Core             — use cases (CreateGame, PlayMove, EndGame, ...)
├── Lama.Infrastructure   — JSON persistence, PostgreSQL lexicon, auth, rating
└── Lama.Languages.fr|de|en — language packs (embedded JSON assets)
```

### Console — supported modes

```text
Lama.Console
├── Command line mode
│   ├── lama game create
│   ├── lama play move H8 LAMA H
│   └── lama show board
└── Textual interactive mode
    ├── menus
    ├── prompts
    ├── game loop
    └── textual rendering of board, rack and scores
```

The entry point `Program.cs` contains no game logic.
It configures the application, registers services, then delegates to the appropriate mode.

```text
Program.cs -> ApplicationModeResolver -> CommandLineMode | InteractiveMode
```

### Online server

- `Lama.Server` is **authoritative in memory** (`GameHubState`) with async PostgreSQL persistence after the game ends.
- Real-time streaming via **SSE** (Server-Sent Events), not SignalR.
- **JWT** authentication (`JwtTokenService` + `JwtMiddleware`).
- AI bots auto-seeded at server startup (`BotCatalog` + `BotAutoPlayService`).

### Decision on command parsing

The project does not rely on the `CommandLine` package as a structural dependency.

Parsing remains intentionally simple in command line mode:

```text
lama game create
lama game join
lama play move
lama show board
```

This decision allows us to:

- keep the architecture simple;
- avoid coupling the game to a CLI library;
- facilitate the textual interactive mode;
- keep game logic outside the console project.

If a strong need arises later for an advanced, scriptable, and highly typed CLI, a parser could be added only in `Lama.Console`, without impacting `Core`, `Domain`, or `Contracts`.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL lexicon available (words + definitions), via `LAMA_LEXICON_CONNECTION_STRING` or `ConnectionStrings:LamaServerDb`
- Language files in `assets/languages/{fr,de,en}/`:
  - `scores.json` — letter values in the format `{ "scores": { "A": 1, "Z": 10, ... } }`
  - `tile-distribution.json` — tile distribution and scaling rules

---

## Basic Commands

### Build the project

```bash
dotnet build
```

### Launch the textual interactive mode

The interactive mode is the primary game experience in a terminal.

```bash
lama
```

or explicitly:

```bash
lama interactive
```

Proposed aliases:

```bash
lama shell
lama ui
```

In this mode, the player is guided by textual menus, prompts and game screens.

Proposed actions include:

- create a game;
- join a game;
- show the board;
- show your rack;
- play a word;
- pass your turn;
- exchange letters;
- save;
- quit.

### Use the command line mode

The command line mode allows you to execute a single action and then terminate the process.

```bash
lama game create
lama game join Bob
lama show board
lama play move H8 MAISON H
lama play pass
lama play swap AEI
lama game save
lama game end
```

This mode is suitable for:

- fast uses;
- scripts;
- end-to-end tests;
- automations;
- diagnostic commands.

### Start a game in command line mode

```bash
# Classic 2-player game
lama game create Alice
lama game join Philippe
lama game join Sophie
# Game with options
lama game create --level tournament
```

### Play in command line mode

```bash
# Place a word horizontally on H8
lama play move H8 MAISON H
# Place a word with crossing
lama play move J8 MAISON V
# Place a word with forced joker (lowercase letter)
lama play move H8 mAISON H
# Simulate a move without playing it
lama play move A1 ZEN H --dry-run
# Pass your turn
lama play pass
# Exchange letters
lama play swap AEI
lama play swap --all
```

### Display

```bash
# Board with highlight of the last move
lama show board
# Rack
lama show rack
# Scores
lama show scores
# History of the last 5 moves
lama show history --last 5
```

### Player profiles

```bash
# Create a profile with optional metadata
lama player create Carla --pseudo Krl --country FR --region Brittany --birth-year 1995

# List profiles (filterable)
lama player list
lama player list --country FR --output json

# View / update a profile
lama player show
lama player update --pseudo LamaQueen --region Occitanie
```

### Ranking and rating

```bash
# Player rating (open/tournament/global)
lama rating show

# Leaderboard by ranking queue
lama rating leaderboard --queue global --top 20
lama rating leaderboard --queue open --top 20
lama rating leaderboard --queue tournament --top 20

# Period stats
lama rating stats --30d
```

### Dictionary

```bash
# Check a word
lama dict check QUARTZ
# Search by pattern
lama dict search "?OISETTE" --lang fr
# Find anagrams
lama dict anagram NOISETTE --min-length 4
```

### System administration

```bash
lama system setup
lama system status --output json
lama system restart
lama system account list
```

### End game

```bash
lama game end
```

---

## Global options

| Option | Alias | Description |
|--------|-------|-------------|
| `--help` | `-h` | Context help |
| `--version` | `-v` | Game version |
| `--verbose` | `-V` | Verbose mode |
| `--quiet` | `-q` | Quiet mode |
| `--no-color` | | Disable ANSI colors |
| `--high-contrast` | | High contrast mode |
| `--lang <code>` | `-l` | Interface language (`fr`, `en`, `de`) |
| `--output <format>` | `-o` | Output format: `text`, `json`, `csv` |

---

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | General error |
| `2` | Invalid argument |
| `3` | Game not found |
| `5` | Word not in dictionary |
| `6` | Placement impossible |
| `8` | Not your turn |
| `10` | Timeout exceeded |
| `11` | Insufficient permissions (ACL denied) |

Source: `src/apps/Lama.Console/Services/ExitCodes.cs`.

---

## Supported languages

| Code | Language | Status |
|------|----------|--------|
| `fr` | French | Implemented |
| `en` | English | Implemented |
| `de` | German | Implemented |

---

## Multiplayer (central server + local offline)

LAMA maintains two modes of operation:

- **Local (offline) mode**: game on local machine, without internet, ideal for dev/test. Isolated from world rankings.
- **Online (central server) mode**: centralized play via `Lama.Server`, necessary for world rankings. Real-time streaming via SSE, JWT authentication.

### Start central server

```bash
dotnet run --project src/apps/Lama.Server --urls http://127.0.0.1:5201
```

### Start WebApp

```bash
dotnet run --project src/apps/Lama.WebApp --urls http://127.0.0.1:5202
# with LAMA_SERVER_URL=http://127.0.0.1:5201
```

### Check server health

```bash
curl -s http://localhost:5201/health
```

### Online ports convention (compose)

- API Server: `5201`
- WebApp: `5202`
- AI Server (suggestions): `5203`

This convention is used in local, staging and production in the project's Docker compose stacks.

### Full development

```bash
make dev         # Server 5201 + WebApp 5202 + AIServer 5203 + PostgreSQL Docker
```

---

## Versioning and build info

LAMA uses a centralized versioning system via the `.build-info` (JSON) file which is synchronized to `BuildInfoConstants.cs` at each build.

### View build info

The WebApp displays build info (🚧 In development) in the **footer** of standard pages and in a **sticky bottom bar** (`GameBuildBar.razor`) on the game page, with:
- Version (v0.1.7)
- Build number (#36)
- Build timestamp (04/07/2026 08:15)

Both use the static `BuildInfoConstants` class to display build info without HTTP calls.

### Make versioning targets

```bash
# Generate a new build timestamp and sync to C#
make build-generate

# Increment the build number and sync to C#
make release BUILD=increment

# Set a specific version and sync to C#
make release VERSION=1.2.3
```

Each make target updates `.build-info` AND `src/apps/Lama.WebApp/Services/BuildInfoConstants.cs`.
