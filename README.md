# LAMA

LAMA est un jeu de mots inspiré du Scrabble, développé en C# / .NET 10.
Les joueurs posent des mots sur un plateau en grille, accumulent des points selon la valeur des lettres et les cases bonus, et s'affrontent jusqu'à épuisement du sac de lettres.

Le jeu est d'abord disponible en console, avec deux modes d'utilisation :

- un mode **commande par commande**, adapté aux actions ponctuelles, aux scripts et aux tests ;
- un mode **interactif textuel**, jouable directement dans un terminal avec menus, prompts et affichage enrichi.

### Interfaces supportées

L'application console prend en charge deux modes :

À terme, le cœur du jeu pourra être réutilisé par d'autres interfaces, notamment une WebApp ou une interface graphique.

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
La console actuelle, le futur mode interactif textuel et une éventuelle future WebApp doivent tous appeler les mêmes services applicatifs.

### Interfaces supportées

L'application console prend en charge deux modes :

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
Program.cs -> ApplicationModeResolver -> CommandLineMode -> InteractiveMode
```

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
- préparer une future interface graphique ou WebApp ;
- maintenir la logique métier en dehors du projet console.

Si un besoin fort apparaît plus tard pour une CLI avancée, scriptable et très typée, un parser pourra être ajouté uniquement dans `Lama.Console`, sans impacter `Core`, `Domain` ou `Contracts`.

Voir aussi : [`docs/console-interface-architecture.md`](docs/console-interface-architecture.md).

---

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Fichiers de langue dans `assets/languages/fr/assets/` :
  - `dictionary.txt` — un mot par ligne
  - `scores.json` — valeurs des lettres au format `{ "scores": { "A": 1, "Z": 10, ... } }`

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


---

## Multijoueur (serveur central + local offline)

LAMA conserve deux modes de fonctionnement:

- **Mode local (offline)**: jeu sur la machine locale, sans internet, ideal pour dev/test.
- **Mode online (serveur central)**: parties centralisees via `Lama.Server`, necessaire pour classement mondial.

Le mode local reste **isole** des classements mondiaux.

Voir le plan de migration detaille : [`docs/multiplayer-migration-plan.md`](docs/multiplayer-migration-plan.md).

### Demarrer le serveur central alpha

```bash
dotnet run --project src/Server/Lama.Server
```

### Verifier la sante du serveur

```bash
curl -s http://localhost:5000/health
```


