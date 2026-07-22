# LAMA

LAMA est un jeu de mots inspiré du Scrabble, développé en C# / .NET 10.
Les joueurs posent des mots sur un plateau en grille, accumulent des points selon la valeur des lettres et les cases bonus, et s'affrontent jusqu'à épuisement du sac de lettres.

## Licence

Ce projet est distribué sous **GNU Affero General Public License v3.0 or later (AGPL-3.0-or-later)**.

- Le fichier de référence est `LICENSE` à la racine du dépôt.
- L'AGPL garantit que les forks et versions modifiées exposées en réseau restent libres.
- Les dons pour financer l'infrastructure sont compatibles avec cette licence.

Le jeu est disponible via plusieurs interfaces :

- une **console** (CLI) avec deux modes : **commande par commande** (actions ponctuelles, scripts, tests) et **interactif textuel** (menus, prompts, affichage enrichi) ;
- une **WebApp Blazor Server** (`Lama.WebApp`) : portail public + interface de jeu en ligne ;
- un **serveur API** (`Lama.Server`) : parties centralisées online, classement, streaming SSE.

---

## Règles du jeu

### Le plateau

- Grille carrée **15×15** par défaut (configurable de 15 à 26)
- Coordonnées : lettre = colonne (A–O), chiffre = ligne (1–15) — ex. `H8` pour la case centrale
- Cases bonus (style Scrabble classique) : multiplicateurs de lettre et de mot

### Les tuiles

- Chaque lettre possède une valeur en points définie par le dictionnaire de langue
- **2 jokers** par défaut — ils peuvent représenter n'importe quelle lettre (valeur 0 pt)
- Le rack contient **7 lettres** par défaut

### Tour de jeu

À chaque tour, le joueur actif doit faire l'une de ces actions :

| Action | Commande |
|--------|----------|
| Poser un mot sur le plateau | `lama play move <case> <mot> <direction>` |
| Passer son tour | `lama play pass` |
| Échanger des lettres contre le sac | `lama play swap <lettres>` |
| Contester le dernier mot joué | `lama play challenge` |

### Placement d'un mot

- Le **premier mot** doit passer par la case de départ, par défaut le centre `H8`
- Direction : `H` pour horizontal ou `V` pour vertical
- Chaque mot posé doit être raccordé aux lettres déjà présentes, sauf le premier
- Tous les mots formés, principal et croisements, doivent être dans le dictionnaire
- Longueur minimale : **2 lettres** par défaut

### Croisements (partage de lettres)

Quand vous posez un mot qui croise un mot existant :

1. **Spécifiez le mot complet**, incluant la lettre du croisement
2. **La lettre doit correspondre** à celle qui existe déjà sur le plateau
3. Le système valide automatiquement les croisements

**Exemple** : Si `LAMA` est déjà horizontal en H8, vous pouvez poser `MAISON`verticalement en J8 :
```bash
lama play move J8 MAISON V
```
Dans ce cas, le `M` de `MAISON` (en J8) croise avec le `M` de `LAMA` — c'est valide.

Si vous tentez `MAISON` en J8 avec un `M` incompatible, le système rejette le placement avec un message clair.

### Fin de partie

La partie se termine quand :

- le sac est vide **et** un joueur épuise son rack ;
- le nombre maximum de tours est atteint, via `--max-turns` ;
- le score maximum est atteint ;
- un joueur met fin manuellement à la partie avec `lama game end`.

---

## Architecture

Le projet suit une architecture inspirée de la **Clean Architecture / Ports & Adapters**.

Le principe central est que la logique du jeu ne dépend d'aucune interface utilisateur.
La console, la WebApp et le serveur appellent tous les mêmes services applicatifs.

### Composants

```text
Apps
├── Lama.Console   — CLI (mode commande + mode interactif)
├── Lama.Server    — API HTTP autoritaire (état en mémoire, persistance PostgreSQL)
├── Lama.WebApp    — Blazor Server (portail + interface de jeu, MVVM léger)
└── Lama.AIServer  — service HTTP de suggestions de coups (port 5203)

Libs
├── Lama.Contracts       — types et interfaces (0 dépendance)
├── Lama.Domain           — règles du jeu (GameEngine, MoveValidator, ScoreCalculator)
├── Lama.Core             — use cases (CreateGame, PlayMove, EndGame, ...)
├── Lama.Infrastructure   — persistance JSON, lexique PostgreSQL, auth, rating
└── Lama.Languages.fr|de|en — packs de langue (assets JSON embarqués)
```

### Console — modes supportés

```text
Lama.Console
├── Mode commande par commande
│   ├── lama game create
│   ├── lama play move H8 LAMA H
│   └── lama show board
└── Mode interactif textuel
    ├── menus
    ├── prompts
    ├── boucle de jeu
    └── rendu textuel du plateau, du rack et des scores
```

Le point d'entrée `Program.cs` ne contient pas la logique du jeu.
Il configure l'application, enregistre les services, puis délègue l'exécution au mode approprié.

```text
Program.cs -> ApplicationModeResolver -> CommandLineMode | InteractiveMode
```

### Serveur online

- `Lama.Server` est **autoritaire en mémoire** (`GameHubState`) avec persistance async PostgreSQL (EF Core) après fin de partie.
- Streaming temps réel via **SSE** (Server-Sent Events), pas de SignalR.
- Authentification **JWT** (`JwtTokenService` + `JwtMiddleware`).
- Bots IA auto-seedés au démarrage (`BotCatalog` + `BotAutoPlayService`).

### Décision sur le parsing des commandes

Le projet ne s'appuie pas sur le paquet `CommandLine` comme dépendance structurante.

Le parsing reste volontairement simple dans le mode commande par commande :

```text
lama game create
lama game join
lama play move
lama show board
```

Cette décision permet de :

- garder une architecture simple ;
- éviter de coupler le jeu à une librairie de parsing CLI ;
- faciliter le mode interactif textuel ;
- maintenir la logique métier en dehors du projet console.

Si un besoin fort apparaît plus tard pour une CLI avancée, scriptable et très typée, un parser pourra être ajouté uniquement dans `Lama.Console`, sans impacter `Core`, `Domain` ou `Contracts`.

---

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Lexicon PostgreSQL disponible (mots + définitions), via `LAMA_LEXICON_CONNECTION_STRING` ou `ConnectionStrings:LamaServerDb`
- Fichiers de langue dans `assets/languages/{fr,de,en}/` :
  - `scores.json` — valeurs des lettres au format `{ "scores": { "A": 1, "Z": 10, ... } }`
  - `tile-distribution.json` — distribution et règles de scaling des tuiles

---

## Commandes de base

### Construire le projet

```bash
dotnet build
```

### Lancer le mode interactif textuel

Le mode interactif est l'expérience principale pour jouer dans un terminal.

```bash
lama
```

ou explicitement :

```bash
lama interactive
```

Alias prévus :

```bash
lama shell
lama ui
```

Dans ce mode, le joueur est guidé par des menus textuels, des prompts et des écrans de jeu.

Exemples d'actions proposées :

- créer une partie ;
- rejoindre une partie ;
- afficher le plateau ;
- afficher son rack ;
- jouer un mot ;
- passer son tour ;
- échanger des lettres ;
- sauvegarder ;
- quitter.

### Utiliser le mode commande par commande

Le mode commande par commande permet d'exécuter une action unique puis de terminer le processus.

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


Ce mode est adapté :

- aux usages rapides ;
- aux scripts ;
- aux tests end-to-end ;
- aux automatisations ;
- aux commandes de diagnostic.

### Démarrer une partie en mode commande

```bash
# Partie classique à 2 joueurs
lama game create Alice
lama game join Philippe
lama game join Sophie
# Partie avec options
lama game create --level tournament
```

### Jouer en mode commande

```bash
# Poser un mot en H8 horizontalement
lama play move H8 MAISON H
# Poser un mot avec croisement
lama play move J8 MAISON V
# Poser un mot avec joker force (notation minuscule)
lama play move H8 mAISON H
# Simuler un coup sans le jouer
lama play move A1 ZEN H --dry-run
# Passer son tour
lama play pass
# Échanger des lettres
lama play swap AEI
lama play swap --all
```

### Affichage

```bash
# Plateau avec mise en évidence du dernier coup
lama show board
# Rack
lama show rack
# Scores
lama show scores
# Historique des 5 derniers coups
lama show history --last 5
```

### Profils joueurs

```bash
# Créer un profil avec métadonnées optionnelles
lama player create Carla --pseudo Krl --country FR --region Bretagne --birth-year 1995

# Lister les profils (filtrable)
lama player list
lama player list --country FR --output json

# Consulter / mettre à jour un profil
lama player show
lama player update --pseudo LamaQueen --region Occitanie
```

### Classement et rating

```bash
# Rating joueur (open/tournoi/global)
lama rating show

# Leaderboard par file de classement
lama rating leaderboard --queue global --top 20
lama rating leaderboard --queue open --top 20
lama rating leaderboard --queue tournament --top 20

# Stats période
lama rating stats --30d
```

### Dictionnaire

```bash
# Vérifier un mot
lama dict check QUARTZ
# Rechercher par motif
lama dict search "?OISETTE" --lang fr
# Trouver des anagrammes
lama dict anagram NOISETTE --min-length 4
```

### Administration système

```bash
lama system setup
lama system status --output json
lama system restart
lama system account list
```

### Fin de partie

```bash
lama game end
```

---

## Options globales

| Option | Alias | Description |
|--------|-------|-------------|
| `--help` | `-h` | Aide contextuelle |
| `--version` | `-v` | Version du jeu |
| `--verbose` | `-V` | Mode verbeux |
| `--quiet` | `-q` | Mode silencieux |
| `--no-color` | | Désactive les couleurs ANSI |
| `--high-contrast` | | Mode contraste élevé |
| `--lang <code>` | `-l` | Langue de l'interface (`fr`, `en`, `de`) |
| `--output <format>` | `-o` | Format de sortie : `text`, `json`, `csv` |

---

## Codes de retour

| Code | Signification |
|------|---------------|
| `0` | Succès |
| `1` | Erreur générale |
| `2` | Argument invalide |
| `3` | Partie introuvable |
| `5` | Mot hors dictionnaire |
| `6` | Placement impossible |
| `8` | Pas votre tour |
| `10` | Timeout dépassé |
| `11` | Droits insuffisants (ACL refusée) |

Source : `src/apps/Lama.Console/Services/ExitCodes.cs`.

---

## Langues supportées

| Code | Langue | Statut |
|------|--------|--------|
| `fr` | Français | Implémenté |
| `en` | Anglais | Implémenté |
| `de` | Allemand | Implémenté |


---

## Multijoueur (serveur central + local offline)

LAMA conserve deux modes de fonctionnement :

- **Mode local (offline)** : jeu sur la machine locale, sans internet, idéal pour dev/test. Isolé des classements mondiaux.
- **Mode online (serveur central)** : parties centralisées via `Lama.Server`, nécessaire pour le classement mondial. Streaming temps réel via SSE, authentification JWT.

### Démarrer le serveur central

```bash
dotnet run --project src/apps/Lama.Server --urls http://127.0.0.1:5201
```

### Démarrer la WebApp

```bash
dotnet run --project src/apps/Lama.WebApp --urls http://127.0.0.1:5202
# avec LAMA_SERVER_URL=http://127.0.0.1:5201
```

### Vérifier la santé du serveur

```bash
curl -s http://localhost:5201/health
```

### Convention de ports online (compose)

- API Server : `5201`
- WebApp : `5202`
- AI Server (suggestions) : `5203`

Cette convention est utilisée en local, staging et production dans les stacks Docker compose du projet.

### Dev complet

```bash
make dev-debug   # Server 5201 + WebApp 5202 en parallèle
```

---

## Versioning et build info

LAMA utilise un système de versioning centralisé via le fichier `.build-info` (JSON) qui est synchronisé vers `BuildInfoConstants.cs` à chaque build.

### Consulter les infos de build

La WebApp affiche les infos de build (🚧 En développement) dans le **footer** des pages standard et dans une **barre sticky en bas** (`GameBuildBar.razor`) sur la page de jeu, avec :
- Version (v0.1.7)
- Build number (#36)
- Timestamp du build (04/07/2026 08:15)

Les deux utilisent la classe statique `BuildInfoConstants` pour afficher les infos sans appels HTTP.

### Make targets de versioning

```bash
# Générer un nouveau build timestamp et synchroniser vers C#
make build-generate

# Incrémenter le numéro de build et synchroniser vers C#
make build-increment

# Fixer une version spécifique et synchroniser vers C#
make version-set VERSION=1.2.3
```

Chaque make target met à jour `.build-info` ET `src/apps/Lama.WebApp/Services/BuildInfoConstants.cs`.
