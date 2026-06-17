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
| `src/libs/Lama.Core` | Use cases de jeu implementes (create/join/move/pass/end + swap partiel) |
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
- `system.setup`, `game.create`, `game.join`, `game.list` sont traites comme commandes publiques cote ACL.

---

## Commandes CLI — etat reel de l'implementation

Le parser CLI actuel est a 2 niveaux:
`lama <groupe> <action> [arguments...] [options]`

### Commandes implementees et executables

- `game.create`
- `game.join`
- `game.end`
- `play.move`
- `play.pass`
- `show.board`
- `show.rack`
- `show.scores`
- `dict.check`
- `dict.search`
- `dict.anagram`
- `system.setup`
- `system.status` (stub retourne non implemente)
- `system.restart` (stub retourne non implemente)
- `player.create` (stub retourne non implemente)
- `tournament.create` (stub retourne non implemente)
- `game.list` (stub retourne non implemente)
- `game.show` (stub retourne non implemente)
- `game.pause` (stub retourne non implemente)
- `game.save` (stub retourne non implemente)
- `play.swap` (stub cote commande; use case existant mais incomplet)
- `play.challenge` (stub retourne non implemente)
- `play.check` (stub retourne non implemente)
- `show.history` (stub retourne non implemente)

### Commandes enregistrees mais non atteignables via le parser actuel

Le parser ne construit que `groupe.action`, donc ces `CommandId` ne matchent pas:

- `login`
- `logout`
- `system.account.create`
- `system.account.list`
- `system.account.revoke`

---

## Etat d'avancement par composant

| Composant | Etat |
|---|---|
| `Lama.Contracts` | ✅ Matures |
| `Lama.Domain` | ✅ Implante (plus un stub) |
| `Lama.Core` | ✅ Implante (avec limites sur swap/challenge/historique) |
| `Lama.Infrastructure` | ✅ Implante (JSON/session/auth/accounts) |
| `Lama.Languages.fr` | ✅ Implante |
| `Lama.Console` mode commande | 🟡 Partiel: noyau OK, plusieurs commandes stubs |
| `Lama.Console` mode interactif | 🟡 Shell present, logique metier non branchee |
| Rendering dedie (`Rendering/*`) | 🔲 Classes vides |
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

### A renforcer

- Tests E2E CLI (parcours complet create -> join -> move -> show -> end)
- Tests de non-regression sur parsing multi-niveaux (system.account.*, login/logout)
- Tests des sorties `--output json` pour les commandes affichees

---

## Ecarts connus a traiter

1. Incoherence doc/code historique: plusieurs docs decrivent encore Domain/Core/Infra comme stubs.
2. Incoherence parser/commandes sur `login`, `logout`, `system.account.*`.
3. `SwapLettersUseCase` encore transitoire (passe le tour sans vrai echange metier complet).
4. Historique des coups absent du coeur, bloque `show.history` et scenarios challenge.

---

## Conventions de code

- `Nullable` active.
- `ImplicitUsings` actif.
- Commentaires XML sur API publiques.
- Erreurs sur `stderr`, sortie exploitable sur `stdout`.
- Identifiants commandes en minuscule, format `groupe.action`.

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
