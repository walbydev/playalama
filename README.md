# LAMA

LAMA est un jeu de mots en ligne de commande inspiré du Scrabble, développé en C# 10 / .NET 10.
Les joueurs posent des mots sur un plateau en grille, accumulent des points selon la valeur des lettres et les cases bonus, et s'affrontent jusqu'à épuisement du sac de lettres.

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
| Poser un mot sur le plateau | `lama play <case> <mot> <direction>` |
| Passer son tour | `lama play pass` |
| Échanger des lettres contre le sac | `lama play swap <lettres>` |
| Contester le dernier mot joué | `lama play challenge` |

### Placement d'un mot

- Le **premier mot** doit passer par la case de départ (défaut : centre `H8`)
- Direction : `H` (horizontal) ou `V` (vertical)
- Chaque mot posé doit être raccordé aux lettres déjà présentes (sauf le premier)
- Tous les mots formés (principal et croisements) doivent être dans le dictionnaire
- Longueur minimale : **2 lettres** par défaut

### Fin de partie

La partie se termine quand :
- Le sac est vide **et** un joueur épuise son rack
- Le nombre maximum de tours est atteint (`--max-turns`)
- Le score maximum est atteint (`--max-turns`)
- Un joueur met fin manuellement à la partie (`lama game end`)

---

## Architecture

Le projet suit une architecture **Clean Architecture / Ports & Adapters** :

```
Lama.Contracts       — Interfaces et entités du domaine (Position, Tile, Move, GameState…)
Lama.Domain          — Logique de jeu (implémentation de IGameEngine)
Lama.Core            — Couche application / cas d'usage
Lama.Infrastructure  — Adaptateurs (persistance, I/O, réseau)
Lama.Languages.fr    — Dictionnaire et valeurs de lettres en français
Lama.Console         — Interface CLI (Spectre.Console + Generic Host)
```

---

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Fichiers de langue dans `assets/languages/fr/assets/` :
  - `dictionary.txt` — un mot par ligne (généré avec `aspell`, voir `docs/tools.md`)
  - `scores.json` — valeurs des lettres au format `{ "scores": { "A": 1, "Z": 10, ... } }`

---

## Commandes de base

### Construire le projet

```bash
dotnet build
```

### Lancer les tests

```bash
dotnet test
```

### Démarrer une partie

```bash
# Partie classique à 2 joueurs
lama game create > game_id.txt
lama game join philippe < game_id.txt
lama game join sophie < game_id.txt

# Plateau 19×19, 3 joueurs, timer 90 secondes
lama game create --size 19 --players 3 --time-limit 90 > game_id.txt
```

### Jouer

```bash
# Poser un mot en H8 horizontalement
lama play H8 MAISON H

# Poser un mot avec un joker (position 3 = lettre I)
lama play H8 MAISON H --joker 3=I

# Simuler un coup sans le jouer
lama play A1 ZEN H --dry-run

# Passer son tour
lama play pass

# Échanger des lettres
lama play swap AEI
lama play swap --all
```

### Affichage

```bash
# Plateau avec mise en évidence du dernier coup
lama show board --last-move

# Rack avec valeurs de lettres
lama show rack --with-values

# Scores
lama show scores

# Historique des 5 derniers coups
lama show history --last 5
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

### Fin de partie

```bash
lama game end < game_id.txt --with-scores
lama game end < game_id.txt --with-scores --export resultat.json
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

Liste complète dans [`docs/defines-CLI.md`](docs/defines-CLI.md).

---

## Langues supportées

| Code | Langue | Statut |
|------|--------|--------|
| `fr` | Français | Implémenté |
| `en` | Anglais | Prévu |
| `de` | Allemand | Prévu |
| `es` | Espagnol | Prévu |
| `it` | Italien | Prévu |
