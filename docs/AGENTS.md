# AGENTS.md — Guide pour agents IA

Ce fichier décrit le projet LAMA à destination des agents IA (Copilot, Cline, Cursor, etc.).
Il sert de référence rapide pour comprendre l'architecture, les conventions et l'état d'avancement du projet.

---

## Vue d'ensemble

**LAMA** est un jeu de mots en ligne de commande (CLI), inspiré du Scrabble, développé en **C# / .NET 10**.
Les joueurs posent des mots sur un plateau en grille, accumulent des points selon la valeur des lettres et les cases bonus, et s'affrontent jusqu'à épuisement du sac de lettres.

---

## Stack technique

| Outil / Lib | Usage |
|---|---|
| .NET 10 / C# | Langage et runtime principal |
| Spectre.Console 0.57 | Rendu CLI coloré (plateau, rack, scores) |
| Microsoft.Extensions.Hosting | Generic Host + injection de dépendances |
| Microsoft.Extensions.Logging | Abstraction de logs |
| Serilog | Implémentation des logs (console + fichier) |
| xUnit 2.9 | Tests unitaires |
| coverlet | Couverture de code |

Gestion centralisée des versions NuGet via `Directory.Packages.props` (Central Package Management).
Propriétés communes (TargetFramework, Nullable, ImplicitUsings) dans `Directory.Build.props`.

---

## Architecture

Le projet suit une **Clean Architecture / Ports & Adapters** stricte.
Aucune dépendance ne doit remonter vers les couches supérieures.

```
Lama.Contracts       ← Interfaces et entités du domaine (pas de dépendances externes)
       ↑
Lama.Domain          ← Logique métier pure (implémente IGameEngine)
       ↑
Lama.Core            ← Cas d'usage / couche application
       ↑
Lama.Infrastructure  ← Adaptateurs : persistance, I/O, réseau
       ↑
Lama.Console         ← Interface CLI (Spectre.Console + Generic Host)

Lama.Languages.fr    ← Plugin de langue (implémente IGameLanguageProvider)
```

### Projets et namespaces

| Projet | Namespace racine | Rôle |
|---|---|---|
| `src/libs/Lama.Contracts` | `Lama.Contracts` | Interfaces et entités partagées |
| `src/libs/Lama.Domain` | `Lama.Domain` | Moteur de jeu (logique métier) |
| `src/libs/Lama.Core` | `Lama.Core` | Cas d'usage |
| `src/libs/Lama.Infrastructure` | `Lama.Infrastructure` | Persistance, I/O |
| `src/libs/Lama.Languages.fr` | `Lama.Languages.fr` | Dictionnaire et scoring français |
| `src/Console/Lama.Console` | `Lama.Console` | CLI (point d'entrée) |
| `tests/Lama.Console.UnitTests` | `Lama.Console.UnitTests` | Tests de la couche Console |
| `tests/Lama.Languages.fr.UnitTests` | `Lama.Languages.fr.UnitTests` | Tests du provider français |

---

## Entités du domaine (`Lama.Contracts`)

```csharp
record Position(int Row, int Column)   // case du plateau, IsValid si 0..14
record Tile(char Letter, bool IsWildcard = false)  // tuile posée
record Move(Dictionary<Position, char> Letters, int Score = 0)  // coup joué
class  BoardState { Tile?[,] Grid }    // état immutable du plateau 15×15
record Player(string Name, int Score, List<char> Rack)
record GameState { BoardState Board; List<Player> Players; int CurrentPlayerIndex; int TurnNumber; bool IsGameOver }
```

### Interfaces clés

| Interface | Rôle |
|---|---|
| `IGameEngine` | Moteur de jeu : `InitializeGame`, `PlayMove`, `ValidateMove`, `PassTurn`, `EndGame`, `GetGameState`, `GetCurrentPlayer`, `CreatePlayerRack` |
| `IGameLanguageProvider` | Plugin de langue : `GetDictionary()`, `GetLetterScores()`, `GetLanguageName()`, `GetLocale()` |
| `IAccessControlService` | Contrôle d'accès : `CheckAccess(command, role, gameLevel?)`, `GetAllowedCommands(role, gameLevel?)` |

---

## Système de permissions

### Rôles (`Role`)

| Rôle | Description |
|---|---|
| `Admin` | Accès complet : système, parties, joueurs, tournois |
| `Player` | Joueur actif : jouer, voir son rack, voir le plateau |
| `Spectator` | Lecture seule, aucune action de jeu |

### Niveaux de partie (`GameLevel`)

| Niveau | Aides | Challenge | Notes |
|---|---|---|---|
| `Casual` | ✅ activées | optionnel | Idéal débutants |
| `Standard` | ✗ désactivées | autorisé | Équilibre |
| `Competitive` | ✗ désactivées | obligatoire | Joueurs confirmés |
| `Tournament` | ✗ désactivées | règles figées | Organisateur décide |

### Matrice de permissions (résumé)

| Commande | Admin | Player+Casual | Player+Standard/Compet./Tourn. | Spectator |
|---|---|---|---|---|
| `system.*` | ✅ | ✗ | ✗ | ✗ |
| `game.create`, `game.end.force` | ✅ | ✗ | ✗ | ✗ |
| `game.join`, `game.list`, `game.show` | ✅ | ✅ | ✅ | ✅ |
| `game.pause`, `game.save` | ✅ | ✅ | ✅ | ✗ |
| `play.move`, `play.pass`, `play.swap`, `play.challenge` | ✅ | ✅ | ✅ | ✗ |
| `play.check`, `play.simulate`, `show.hints` | ✅ | ✅ | ✗ | ✗ |
| `show.board`, `show.scores`, `show.history` | ✅ | ✅ | ✅ | ✅ |
| `show.rack` | ✅ | ✅ | ✅ | ✗ |
| `dict.check`, `dict.search`, `dict.anagram` | ✅ | ✅ | ✗ | ✗ |
| `dict.install`, `dict.remove` | ✅ | ✗ | ✗ | ✗ |
| `player.*`, `tournament.*` | ✅ | ✅ | ✅ | ✅ (lecture) |

Implémenté dans `AccessControlService` + `AccessControlMiddleware` (exit code 11 si refus).

---

## Commandes CLI

Les commandes sont organisées en groupes. Format : `lama <groupe> <action> [options]`

| Groupe | Actions |
|---|---|
| `game` | `create`, `join`, `list`, `show`, `pause`, `save`, `end` |
| `play` | `move <case> <mot> <direction>`, `pass`, `swap <lettres>`, `challenge`, `check` |
| `show` | `board`, `rack`, `scores`, `history` |
| `dict` | `check <mot>`, `search <motif>` |
| `player` | `create`, (autres à venir) |
| `tournament` | `create`, (autres à venir) |
| `system` | `status`, `restart` |

Options globales : `--help`, `--version`, `--verbose`, `--quiet`, `--no-color`, `--high-contrast`, `--lang <code>`, `--output <text|json|csv>`

### Codes de retour

| Code | Signification |
|---|---|
| 0 | Succès |
| 1 | Erreur générale |
| 2 | Argument invalide |
| 3 | Partie introuvable |
| 5 | Mot hors dictionnaire |
| 6 | Placement impossible |
| 8 | Pas votre tour |
| 10 | Timeout dépassé |
| 11 | Droits insuffisants (access denied) |

---

## Support multilingue

| Code | Langue | Statut |
|---|---|---|
| `fr` | Français | ✅ Implémenté (`Lama.Languages.fr`) |
| `en` | Anglais | 🔲 Prévu |
| `de` | Allemand | 🔲 Prévu |
| `es` | Espagnol | 🔲 Prévu |
| `it` | Italien | 🔲 Prévu |

Le `FrenchLanguageProvider` charge :
- `assets/languages/fr/assets/dictionary.txt` — un mot par ligne (généré avec `aspell`)
- `assets/languages/fr/assets/scores.json` — `{ "scores": { "A": 1, "Z": 10, ... } }`

---

## État d'avancement

| Composant | État |
|---|---|
| `Lama.Contracts` — entités et interfaces | ✅ Complet |
| `Lama.Languages.fr` — dictionnaire FR | ✅ Complet |
| `AccessControlService` + middleware | ✅ Complet + testé |
| `Lama.Domain` — moteur de jeu | 🔲 Stub vide |
| `Lama.Core` — cas d'usage | 🔲 Stub vide |
| `Lama.Infrastructure` — persistance | 🔲 Stub vide |
| `Lama.Console` — commandes CLI | 🔲 Stubs vides (Program.cs = "Hello World") |
| Tests unitaires AccessControl | ✅ Complets |
| Tests unitaires FrenchLanguageProvider | ✅ Présents |

---

## Conventions de code

- **Nullable activé** partout (`<Nullable>enable</Nullable>`)
- **ImplicitUsings** activé (pas besoin de `using System;` etc.)
- Records C# pour les entités immuables (`Position`, `Tile`, `Move`, `Player`, `GameState`)
- `sealed` sur les implémentations de services (`AccessControlService`, `AccessControlMiddleware`)
- Commentaires XML (`///`) obligatoires sur toutes les interfaces et classes publiques
- Écriture sur `stderr` pour les messages d'erreur (ne pas polluer `stdout` utilisé pour `--output json`)
- Identifiants de commandes en minuscules avec point : `"groupe.action"` (ex: `"play.move"`, `"show.hints"`)

---

## Commandes utiles

```bash
# Build
dotnet build

# Tests
dotnet test

# Lancer la console (quand implémentée)
dotnet run --project src/Console/Lama.Console
```
