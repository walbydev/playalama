# AGENTS.md — Guide pour agents IA

Ce fichier décrit l'état réel du projet LAMA pour les agents IA (Copilot, Cline, Cursor, etc.).
La source de vérité est le code dans `src/` et `tests/`.

---

## Vue d'ensemble

**LAMA** est un jeu de mots en CLI, inspiré du Scrabble, en **C# / .NET 10**.
Objectif: proposer un mode commande par commande et un mode interactif textuel,
avec un cœur métier reutilisable par d'autres interfaces.

---

## Stack technique

| Outil / Lib | Usage |
|---|---|
| .NET 10 / C# | Langage et runtime principal |
| Spectre.Console 0.57 | Rendu terminal (mode interactif + plateau) |
| Microsoft.Extensions.Hosting | Generic Host + DI |
| Microsoft.Extensions.Logging | Abstraction de logs |
| Serilog | Logs console + fichier |
| xUnit 2.9 | Tests unitaires |
| FluentAssertions | Assertions tests |
| coverlet | Couverture de code |

Gestion centralisée des versions NuGet via `Directory.Packages.props`.
Propriétés communes via `Directory.Build.props`.

---

## Architecture

Le projet suit une architecture type Clean / Ports & Adapters.

```
Lama.Contracts       <- Interfaces et entités de base
       ^
Lama.Domain          <- Moteur de jeu (regles)
       ^
Lama.Core            <- Use cases applicatifs
       ^
Lama.Infrastructure  <- Persistance JSON, session, auth, comptes
       ^
Lama.Console         <- CLI (Program, modes, parser, commandes)

Lama.Languages.fr    <- Provider langue FR (dico + scores)
```

### Projets et rôle

| Projet | Role actuel |
|---|---|
| `src/libs/Lama.Contracts` | Entites + interfaces (jeu, auth, session, ACL) |
| `src/libs/Lama.Domain` | Moteur de jeu implemente (`GameEngine`, validation, scoring, bag) |
| `src/libs/Lama.Core` | Use cases de jeu implementes (create/join/move/pass/swap/end) |
| `src/libs/Lama.Infrastructure` | Repository JSON, session locale, auth/token, comptes |
| `src/libs/Lama.Languages.fr` | Provider francais implemente |
| `src/Console/Lama.Console` | Point d'entree + commandes CLI + mode interactif (partiel) |

---

## Entites et interfaces clefs (`Lama.Contracts`)

- `Position`, `Tile`, `Move`, `BoardState`, `Player`, `GameState`
- `IGameEngine`
- `IGameRepository`
- `IGameLanguageProvider`
- `IAccessControlService`
- `ISessionService`
- `IAccountService`, `IAuthService`
- `Role` = `SuperAdmin`, `Admin`, `Host`, `Player`, `Spectator`
- `GameLevel` = `Casual`, `Standard`, `Competitive`, `Tournament`

---

## Permissions (etat reel)

Controle via `AccessControlService` + `AccessControlMiddleware`.
Refus d'accès: exit code `11`.

### Rappels importants

- `SuperAdmin`: accès total.
- `Admin`: ne peut pas jouer (`play.*`, `show.rack`) pour anti-triche.
- `Host`: joue + droits de gestion de sa partie.
- `Player`: joue sans droits admin.
- `Spectator`: lecture seule.
- Commandes publiques ACL: `system.setup`, `game.create`, `game.join`, `game.list`, `login`, `logout`.

---

## Commandes CLI — etat reel de l'implementation

Le parser supporte:
- format standard: `lama <groupe> <action> [arguments...] [options]`
- commandes mono-niveau: `lama login`, `lama logout`
- commandes tri-niveaux: `lama system account <create|list|revoke>`

### Commandes implementees et executables

- `game.create`
- `game.join`
- `game.list`
- `game.show`
- `game.pause`
- `game.save`
- `game.end`
- `play.move`
- `play.pass`
- `play.swap`
- `play.challenge`
- `play.check`
- `show.board`
- `show.rack`
- `show.scores`
- `show.history`
- `dict.check`
- `dict.search`
- `dict.anagram`
- `system.setup`
- `login`
- `logout`
- `system.account.create`
- `system.account.list`
- `system.account.revoke`

### Commandes presentes mais encore stubs

- `system.status`
- `system.restart`
- `player.create`
- `tournament.create`

---

## Etat d'avancement par composant

| Composant | Etat |
|---|---|
| `Lama.Contracts` | ✅ Matures |
| `Lama.Domain` | ✅ Implante (avec historique des coups et challenge complet) |
| `Lama.Core` | ✅ Implante (avec UseCases pour challenge, move, pass, swap) |
| `Lama.Infrastructure` | ✅ Implante (JSON/session/auth/accounts) |
| `Lama.Languages.fr` | ✅ Implante |
| `Lama.Console` mode commande | ✅ Principal operationnel - plusieurs commandes implementees |
| `Lama.Console` mode interactif | 🟡 Shell present, logique metier non branchee |
| Rendering dedie (`Rendering/*`) | 🔲 Classes vides |
| Tests E2E CLI | 🔲 A creer (parcours complets create/join/move/show/end) |
| Middlewares additionnels (`Accessibility/Logging/ErrorHandling`) | 🔲 Classes vides |

---

## Tests

### Couverture existante

- `tests/Lama.Domain.UnitTests`:
  - `GameEngine`, `MoveValidator`, `ScoreCalculator`, `TileBag`, `BonusMap`
- `tests/Lama.Core.UnitTests`:
  - `CreateGameUseCase`, `JoinGameUseCase`, `PlayMoveUseCase`, `PassTurn/Swap/End`
- `tests/Lama.Infrastructure.UnitTests`:
  - `JsonGameRepository`, `SessionService`, `AccountService`, `AuthService`, `PasswordHasher`
- `tests/Lama.Console.UnitTests`:
  - `AccessControlService`, `CommandContext`, `CommandContextParser`
- `tests/Lama.Languages.fr.UnitTests`:
  - `FrenchLanguageProvider`

### Etat actuel des tests console

- `Lama.Console.UnitTests`: ✅ vert (149/149)

### A renforcer

- Tests E2E CLI (parcours complet create -> join -> move -> show -> end)
- Tests des sorties `--output json` pour les commandes affichees

---

## Ecarts connus a traiter

1. Incoherence doc/code historique: plusieurs docs decrivent encore Domain/Core/Infra comme stubs.
2. Historique des coups absent du coeur, bloque `show.history` et scenarios challenge.
3. Mode interactif textuel encore largement placeholder.

---

## Conventions de code

- `Nullable` active.
- `ImplicitUsings` actif.
- Commentaires XML sur API publiques.
- Erreurs sur `stderr`, sortie exploitable sur `stdout`.
- Identifiants commandes en minuscule, format `groupe.action` ou explicites (`login`, `system.account.*`).

---

## Commandes utiles

```bash
# Build
 dotnet build

# Tests
 dotnet test

# Lancer la console
 dotnet run --project src/Console/Lama.Console
```

---

## Regle pour agents

Quand un doute apparait entre documentation et comportement, se fier au code:

1. `src/Console/Lama.Console/Program.cs`
2. `src/Console/Lama.Console/Services/CommandContextParser.cs`
3. `src/Console/Lama.Console/Services/CommandDispatcher.cs`
4. `src/Console/Lama.Console/Commands/**/*.cs`
5. `src/libs/Lama.Core/**/*.cs` + `src/libs/Lama.Domain/**/*.cs`
