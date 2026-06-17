# Architecture de l'interface console

Ce document décrit l'architecture retenue pour l'interface console de LAMA.

L'objectif est de supporter deux modes de jeu en console tout en préparant l'évolution future du projet vers d'autres interfaces, notamment une WebApp ou une interface graphique.

---

## Objectifs

L'interface console doit permettre deux usages complémentaires :

1. un mode **commande par commande** ;
2. un mode **interactif textuel**.

Ces deux modes doivent partager le même cœur applicatif.

La logique du jeu ne doit pas être implémentée dans le projet console.
Elle doit rester dans les couches réutilisables du projet (`Lama.Contracts`, `Lama.Domain`, `Lama.Core`).

---

## Modes supportés

### Mode commande par commande

Le mode commande par commande exécute une seule action puis termine le processus.

Exemples :

```bash
lama game create
lama game join philippe
lama play move H8 MAISON H
lama play pass
lama show board
lama game save
```

Ce mode est adapté :

- aux actions ponctuelles ;
- aux scripts ;
- aux tests automatisés ;
- aux diagnostics ;
- aux scénarios d'intégration continue.

Dans ce mode, l'utilisateur fournit une commande explicite.
L'application parse les arguments, construit un `CommandContext`, vérifie les droits via `IAccessControlService`, exécute l'action, affiche le résultat, puis se termine.

---

### Mode interactif textuel

Le mode interactif textuel est l'expérience principale de jeu en console.

Il est lancé avec :

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

Dans ce mode, l'utilisateur n'a pas besoin de saisir des commandes complètes.
Il navigue dans des menus, répond à des prompts et joue dans une boucle interactive.

Exemples d'écrans possibles :

```
Menu principal
  - Créer une partie
  - Rejoindre une partie
  - Charger une partie
  - Options
  - Quitter
```

```
Tour du joueur
  - Afficher le plateau
  - Afficher mon rack
  - Jouer un mot
  - Passer
  - Échanger des lettres
  - Contester
  - Sauvegarder
```

Le mode interactif repose sur une UI textuelle non graphique.
Il peut utiliser `Spectre.Console` pour améliorer l'expérience terminal, mais il ne doit pas contenir les règles métier du jeu.

---

## Décision : ne pas utiliser CommandLine comme dépendance structurante

Le projet ne s'appuie pas sur le paquet `CommandLine` pour structurer l'application.

Cette décision est volontaire.

Le besoin principal du projet n'est pas de fournir une CLI fortement typée et exhaustive, mais de proposer un jeu avec :

- une expérience interactive agréable ;
- un mode commande simple ;
- une architecture réutilisable par d'autres interfaces.

Un parseur CLI avancé pourrait être utile pour une application principalement orientée scripting.
LAMA est d'abord un jeu, et le mode interactif textuel est une interface de premier plan.

Si un besoin fort apparaît plus tard pour une CLI avancée, scriptable et très typée, un parser pourra être ajouté uniquement dans `Lama.Console`, sans impacter `Core`, `Domain` ou `Contracts`.

---

## Pourquoi éviter CommandLine au centre de l'architecture ?

Utiliser une bibliothèque comme `CommandLine` dès le cœur de l'architecture pourrait introduire un couplage inutile entre :

- la façon dont l'utilisateur saisit une commande ;
- la structure interne des cas d'usage ;
- les futures interfaces de l'application.

Or, le projet doit pouvoir évoluer vers :

- Console commande par commande
- Console interactive textuelle
- WebApp
- Interface graphique
- Tests automatisés

Toutes ces interfaces doivent appeler les mêmes cas d'usage sans dépendre d'une bibliothèque CLI.

La dépendance à un parser de ligne de commande, si elle est ajoutée plus tard, doit rester confinée au projet `Lama.Console`.

---

## Principe architectural

La console est un adaptateur d'entrée/sortie.

Elle ne doit pas contenir :

- les règles de placement des mots ;
- le calcul des scores ;
- la validation du dictionnaire ;
- la gestion du sac de lettres ;
- les règles de fin de partie ;
- la logique de challenge ;
- la logique de sauvegarde métier.

Elle doit uniquement :

1. lire une entrée utilisateur ;
2. transformer cette entrée en demande applicative ;
3. appeler un service applicatif ;
4. recevoir un résultat ;
5. afficher ce résultat.

---

## Architecture globale

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

Aucune dépendance ne doit remonter vers les couches supérieures.

---

## Vue d'ensemble du projet console

```
Program.cs
  → configure l'application
  → enregistre les services
  → résout le mode d'exécution via ApplicationModeResolver
  → lance le mode choisi (CommandLineMode ou InteractiveMode)

ApplicationModeResolver
  → choisit InteractiveMode ou CommandLineMode selon les arguments

CommandLineMode
  → parse les arguments simples
  → construit un CommandContext
  → appelle CommandDispatcher
  → vérifie les droits via IAccessControlService
  → affiche le résultat
  → retourne un code de retour

InteractiveMode
  → lance une boucle interactive
  → affiche des menus via Spectre.Console
  → collecte les choix utilisateur
  → appelle les services applicatifs

Core / Domain / Contracts
  → contiennent le cœur du jeu
  → ne dépendent pas de la console

Infrastructure
  → fournit les adaptateurs techniques (persistance, I/O)
```

---

## Rôle de Program.cs

`Program.cs` doit rester minimal.

Ses responsabilités sont :

- créer le host (`Microsoft.Extensions.Hosting`) ;
- configurer le logging (Serilog) ;
- enregistrer les services applicatifs et d'infrastructure ;
- enregistrer les modes console ;
- enregistrer les commandes ;
- résoudre le mode d'exécution via `IApplicationModeResolver` ;
- lancer le mode choisi ;
- gérer les erreurs globales et retourner le code de sortie approprié.

Il ne doit pas contenir de logique de jeu.

Flux attendu :

```
Program.cs
  → Build le Generic Host
  → Résout IApplicationModeResolver
  → Appelle mode.RunAsync()
  → Retourne le exit code
```

---

## Organisation recommandée du projet console

```
Lama.Console/
├── Program.cs
├── Modes/
│   ├── IConsoleMode.cs
│   ├── IApplicationModeResolver.cs
│   ├── ApplicationModeResolver.cs
│   ├── CommandLineMode.cs
│   └── InteractiveMode.cs
├── Commands/
│   ├── Game/
│   │   ├── GameCommand.cs
│   │   ├── GameCreateCommand.cs
│   │   ├── GameJoinCommand.cs
│   │   ├── GameListCommand.cs
│   │   ├── GameShowCommand.cs
│   │   ├── GamePauseCommand.cs
│   │   ├── GameSaveCommand.cs
│   │   └── GameEndCommand.cs
│   ├── Play/
│   │   ├── PlayCommand.cs
│   │   ├── PlayMoveCommand.cs
│   │   ├── PlayPassCommand.cs
│   │   ├── PlaySwapCommand.cs
│   │   ├── PlayChallengeCommand.cs
│   │   └── PlayCheckCommand.cs
│   ├── Show/
│   │   ├── ShowCommand.cs
│   │   ├── ShowBoardCommand.cs
│   │   ├── ShowRackCommand.cs
│   │   ├── ShowScoresCommand.cs
│   │   └── ShowHistoryCommand.cs
│   ├── Dict/
│   │   ├── DictCommand.cs
│   │   ├── DictCheckCommand.cs
│   │   └── DictSearchCommand.cs
│   ├── Player/
│   │   ├── PlayerCommand.cs
│   │   └── PlayerCreateCommand.cs
│   ├── Tournament/
│   │   ├── TournamentCommand.cs
│   │   └── TournamentCreateCommand.cs
│   └── System/
│       ├── SystemCommand.cs
│       ├── SystemStatusCommand.cs
│       └── SystemRestartCommand.cs
├── Interactive/
│   ├── InteractiveGameShell.cs
│   ├── MainMenuScreen.cs
│   ├── GameMenuScreen.cs
│   ├── PlayerTurnScreen.cs
│   ├── BoardScreen.cs
│   ├── RackScreen.cs
│   └── ScoresScreen.cs
├── Rendering/
│   ├── BoardRenderer.cs
│   ├── RackRenderer.cs
│   ├── ScoreRenderer.cs
│   └── ThemeManager.cs
└── Services/
    ├── CommandContext.cs
    ├── ICommandDispatcher.cs
    ├── CommandDispatcher.cs
    └── AccessControlService.cs  ← délègue à IAccessControlService (Contracts)
```

---

## Commandes CLI

Les commandes sont organisées en groupes. Format : `lama <groupe> <action> [options]`

| Groupe | Actions |
|--------|---------|
| `game` | `create`, `join`, `list`, `show`, `pause`, `save`, `end` |
| `play` | `move <case> <mot> <direction>`, `pass`, `swap <lettres>`, `challenge`, `check` |
| `show` | `board`, `rack`, `scores`, `history` |
| `dict` | `check <mot>`, `search <motif>`, `anagram <lettres>` |
| `player` | `create` |
| `tournament` | `create` |
| `system` | `status`, `restart` |

### Options globales

| Option | Alias | Description |
|--------|-------|-------------|
| `--help` | `-h` | Aide contextuelle |
| `--version` | `-v` | Version du jeu |
| `--verbose` | `-V` | Mode verbeux |
| `--quiet` | `-q` | Mode silencieux |
| `--no-color` | | Désactive les couleurs ANSI |
| `--high-contrast` | | Mode contraste élevé |
| `--lang <code>` | `-l` | Langue de l'interface (`fr`, `en`, `de`, `es`, `it`) |
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
| `11` | Droits insuffisants (access denied) |

Les erreurs sont écrites sur `stderr`. La sortie standard (`stdout`) est réservée aux résultats formatés (notamment pour `--output json`).

---

## Système de permissions

L'accès aux commandes est contrôlé par `IAccessControlService` (défini dans `Lama.Contracts`).

### Rôles

| Rôle | Description |
|------|-------------|
| `Admin` | Accès complet : système, parties, joueurs, tournois |
| `Player` | Joueur actif : jouer, voir son rack, voir le plateau |
| `Spectator` | Lecture seule, aucune action de jeu |

### Niveaux de partie

| Niveau | Aides | Challenge | Notes |
|--------|-------|-----------|-------|
| `Casual` | activées | optionnel | Idéal débutants |
| `Standard` | désactivées | autorisé | Équilibre |
| `Competitive` | désactivées | obligatoire | Joueurs confirmés |
| `Tournament` | désactivées | règles figées | Organisateur décide |

En cas de refus d'accès, la commande se termine avec le code de retour `11`.

---

## Rendu et affichage

Le projet utilise `Spectre.Console 0.57` pour le rendu CLI.

Responsabilités du rendu :

- `BoardRenderer` — affiche le plateau 15×15 avec les cases bonus et les lettres posées ;
- `RackRenderer` — affiche le rack du joueur avec les valeurs de lettres ;
- `ScoreRenderer` — affiche le tableau des scores ;
- `ThemeManager` — gère les thèmes (couleurs, contraste élevé, mode `--no-color`).

Le rendu doit être découplé de la logique de jeu.
Les classes de rendu reçoivent des modèles de données (issus des cas d'usage) et les affichent.

---

## Références

- [`README.md`](../README.md) — présentation générale du projet et commandes de base
- [`docs/defines-CLI.md`](defines-CLI.md) — liste complète des codes de retour et définitions CLI
- `Lama.Contracts` — interfaces `IGameEngine`, `IAccessControlService`, `IGameLanguageProvider`
