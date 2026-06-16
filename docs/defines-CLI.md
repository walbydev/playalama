# LAMA - Référence complète des commandes CLI

## Synopsis général

```
lama <commande> [sous-commande] [arguments] [options]
lama --help
lama --version
lama <commande> --help
```

---

## Options globales

| Option | Alias | Description |
|--------|-------|-------------|
| `--help` | `-h` | Affiche l'aide contextuelle |
| `--version` | `-v` | Affiche la version du jeu |
| `--verbose` | `-V` | Mode verbeux (détails techniques) |
| `--quiet` | `-q` | Mode silencieux (sortie minimale) |
| `--no-color` | | Désactive les couleurs ANSI (utile pour malvoyants avec lecteur d'écran) |
| `--high-contrast` | | Active le mode contraste élevé |
| `--lang <code>` | `-l` | Langue de l'interface (`fr`, `en`, `de`, `es`, `it`) |
| `--config <fichier>` | `-c` | Chemin vers un fichier de configuration alternatif |
| `--output <format>` | `-o` | Format de sortie : `text` (défaut), `json`, `csv` |

---

## 1. Gestion du système

### `lama system`

```
lama system <action> [options]
```

| Sous-commande | Description |
|---------------|-------------|
| `lama system status` | Affiche l'état général du système |
| `lama system restart` | Redémarre le système de jeu |
| `lama system restart --force` | Redémarre sans demander confirmation |
| `lama system restart --save` | Sauvegarde toutes les parties avant de redémarrer |
| `lama system shutdown` | Arrête proprement le système |
| `lama system shutdown --force` | Arrêt immédiat sans confirmation |
| `lama system config show` | Affiche la configuration actuelle |
| `lama system config set <clé> <valeur>` | Modifie un paramètre de configuration |
| `lama system config reset` | Remet la configuration par défaut |
| `lama system logs` | Affiche les derniers logs système |
| `lama system logs --tail <n>` | Affiche les `n` dernières lignes de logs |
| `lama system logs --follow` | Suit les logs en temps réel |
| `lama system logs --level <niveau>` | Filtre par niveau : `info`, `warn`, `error` |
| `lama system diagnostics` | Lance un diagnostic complet du système |
| `lama system update` | Vérifie et applique les mises à jour disponibles |

> **Alias de compatibilité :** `lama restart system` → équivalent à `lama system restart`

---

## 2. Gestion des profils joueurs

### `lama player`

```
lama player <action> [arguments] [options]
```

| Sous-commande | Description |
|---------------|-------------|
| `lama player create <pseudo>` | Crée un nouveau profil joueur |
| `lama player create <pseudo> --lang <code>` | Crée un profil avec la langue préférée |
| `lama player create <pseudo> --accessibility` | Crée un profil avec les options d'accessibilité activées |
| `lama player delete <pseudo>` | Supprime un profil joueur |
| `lama player delete <pseudo> --confirm` | Suppression sans demande de confirmation |
| `lama player show <pseudo>` | Affiche les informations d'un joueur |
| `lama player list` | Liste tous les joueurs enregistrés |
| `lama player list --sort-by <champ>` | Trie par `name`, `score`, `games`, `winrate` |
| `lama player list --top <n>` | Affiche les `n` meilleurs joueurs |
| `lama player stats <pseudo>` | Affiche les statistiques détaillées d'un joueur |
| `lama player stats <pseudo> --history` | Affiche l'historique des parties |
| `lama player stats <pseudo> --history --last <n>` | Les `n` dernières parties |
| `lama player rename <ancien> <nouveau>` | Renomme un profil joueur |
| `lama player reset-stats <pseudo>` | Remet les statistiques à zéro |
| `lama player export <pseudo> <fichier>` | Exporte le profil en JSON |
| `lama player import <fichier>` | Importe un profil depuis un fichier JSON |

---

## 3. Gestion des parties

### `lama game`

```
lama game <action> [arguments] [options]
```

---

### 3.1 Créer une partie

```
lama game create [options]
lama game create [options] > <fichier_id>
```

| Option | Alias | Valeurs | Défaut | Description |
|--------|-------|---------|--------|-------------|
| `--size <n>` | `-s` | `15`, `17`, `19`, `21`, `23`, `25`, `26` | `15` | Taille du plateau (carré n×n) |
| `--size <l>x<h>` | `-s` | ex: `15x17` | `15x15` | Plateau rectangulaire |
| `--start <case>` | | ex: `H8`, `center` | `center` | Case de départ du premier mot |
| `--players <n>` | `-p` | `2` à `6` | `2` | Nombre de joueurs attendus |
| `--lang <code>` | `-l` | `fr`, `en`, `de`, `es`, `it` | `fr` | Langue du dictionnaire |
| `--time-limit <sec>` | `-t` | entier > 0, `0`=illimité | `0` | Temps limite par tour en secondes |
| `--rack-size <n>` | `-r` | `5` à `10` | `7` | Nombre de lettres en main |
| `--bag-preset <nom>` | | `classic`, `extended`, `custom` | `classic` | Distribution des lettres |
| `--bag-file <fichier>` | | chemin | | Distribution personnalisée depuis un fichier |
| `--bonus-preset <nom>` | | `classic`, `none`, `random`, `custom` | `classic` | Disposition des cases bonus |
| `--bonus-file <fichier>` | | chemin | | Cases bonus personnalisées depuis un fichier |
| `--name <nom>` | `-n` | texte | auto-généré | Nom de la partie |
| `--private` | | | false | Partie privée (non listée) |
| `--seed <n>` | | entier | aléatoire | Graine aléatoire (reproductibilité) |
| `--allow-challenges` | | | false | Autorise les contestations de mots |
| `--challenge-penalty <n>` | | entier ≥ 0 | `0` | Pénalité en points si contestation échoue |
| `--min-word-length <n>` | | `2` à `5` | `2` | Longueur minimale des mots |
| `--jokers <n>` | | `0` à `4` | `2` | Nombre de jokers dans le sac |
| `--max-score <n>` | | entier > 0, `0`=illimité | `0` | Score maximum pour terminer la partie |
| `--max-turns <n>` | | entier > 0, `0`=illimité | `0` | Nombre maximum de tours |

**Exemples :**
```bash
# Partie classique standard
lama game create > game_id.txt

# Plateau 19x19, 3 joueurs, dictionnaire anglais, 90 secondes par tour
lama game create --size 19 --players 3 --lang en --time-limit 90 > game_id.txt

# Plateau 21x21 avec départ en case B2, parties privées, nom personnalisé
lama game create --size 21 --start B2 --private --name "Tournoi Club" > game_id.txt

# Partie reproductible avec seed fixe et bonus aléatoires
lama game create --bonus-preset random --seed 42 > game_id.txt
```

> **Alias de compatibilité :** `lama create new game` → équivalent à `lama game create`

---

### 3.2 Rejoindre une partie

```
lama game join <pseudo> [options]
lama game join <pseudo> [options] < <fichier_id>
lama game join <pseudo> --id <game_id> [options]
```

| Option | Alias | Description |
|--------|-------|-------------|
| `--id <game_id>` | `-i` | Identifiant de la partie (alternatif au pipe) |
| `--spectator` | | Rejoindre en tant que spectateur |
| `--accessibility` | `-a` | Active le mode accessibilité pour ce joueur |
| `--no-color` | | Désactive les couleurs pour ce joueur |
| `--high-contrast` | | Active le contraste élevé pour ce joueur |

**Exemples :**
```bash
lama game join philippe < game_id.txt
lama game join sophie --id a3f7c2
lama game join roger --spectator < game_id.txt
lama game join marie --high-contrast < game_id.txt
```

> **Alias de compatibilité :** `lama join as <pseudo> game` → équivalent à `lama game join <pseudo>`

---

### 3.3 Lister les parties

```
lama game list [options]
```

| Option | Alias | Description |
|--------|-------|-------------|
| `--with-scores` | `-s` | Affiche les scores courants |
| `--with-players` | `-p` | Affiche les joueurs connectés (inclus par défaut) |
| `--sort-by <champ>` | | Trie par `name`, `score`, `date`, `players`, `turns` |
| `--sort-desc` | | Tri décroissant |
| `--sort-asc` | | Tri croissant (défaut) |
| `--filter-lang <code>` | | Filtre par langue |
| `--filter-size <n>` | | Filtre par taille de plateau |
| `--filter-status <état>` | | `waiting`, `running`, `paused`, `finished` |
| `--private` | | Inclut les parties privées si autorisé |
| `--top <n>` | | Limite l'affichage aux `n` premières parties |
| `--output <format>` | `-o` | `text`, `json`, `csv` |

**Exemples :**
```bash
lama game list
lama game list --with-scores --sort-by score --sort-desc
lama game list --filter-status running --filter-lang fr
lama game list --with-scores --sort-by score --sort-desc --output json
```

> **Alias de compatibilité :**
> - `lama list games` → `lama game list`
> - `lama list games --with-scores` → `lama game list --with-scores`
> - `lama list games --with-scores --sort-by-score` → `lama game list --with-scores --sort-by score --sort-desc`

---

### 3.4 Afficher une partie

```
lama game show [options]
lama game show [options] < <fichier_id>
lama game show --id <game_id> [options]
```

| Option | Alias | Description |
|--------|-------|-------------|
| `--id <game_id>` | `-i` | Identifiant de la partie |
| `--with-scores` | `-s` | Affiche les scores |
| `--with-board` | `-b` | Affiche le plateau de jeu |
| `--with-rack` | `-r` | Affiche le rack du joueur actif |
| `--with-history` | | Affiche les derniers coups joués |
| `--history-last <n>` | | Limite l'historique aux `n` derniers coups |
| `--with-bag-count` | | Affiche le nombre de lettres restantes dans le sac |
| `--full` | `-f` | Affiche tout (`--with-scores --with-board --with-rack --with-history`) |

---

### 3.5 Terminer une partie

```
lama game end [options]
lama game end [options] < <fichier_id>
lama game end --id <game_id> [options]
lama game end all [options]
```

| Option | Alias | Description |
|--------|-------|-------------|
| `--id <game_id>` | `-i` | Identifiant de la partie |
| `--with-scores` | `-s` | Affiche le classement final |
| `--with-history` | | Affiche l'historique complet de la partie |
| `--save` | | Sauvegarde la partie avant de la terminer |
| `--export <fichier>` | | Exporte le résultat final dans un fichier |
| `--export-format <fmt>` | | Format d'export : `json`, `csv`, `txt` |
| `--force` | | Termine sans demander confirmation |
| `--reason <texte>` | | Motif de fin de partie (pour les logs) |

**Pour terminer toutes les parties :**
```
lama game end all [options]
```

| Option | Description |
|--------|-------------|
| `--force` | Sans confirmation |
| `--save` | Sauvegarde toutes les parties avant |
| `--filter-status <état>` | Limite aux parties dans un état donné |
| `--export-dir <dossier>` | Exporte tous les résultats dans un dossier |

**Exemples :**
```bash
lama game end < game_id.txt --with-scores
lama game end --id a3f7c2 --with-scores --export resultat.json
lama game end all --save --force
lama game end all --filter-status waiting --force
```

> **Alias de compatibilité :**
> - `lama end game` → `lama game end`
> - `lama end game --with-scores` → `lama game end --with-scores`
> - `lama end all games` → `lama game end all`

---

### 3.6 Mettre en pause / Reprendre une partie

```
lama game pause [options]
lama game pause [options] < <fichier_id>
lama game resume [options]
lama game resume [options] < <fichier_id>
```

| Option | Description |
|--------|-------------|
| `--id <game_id>` | Identifiant de la partie |
| `--reason <texte>` | Motif de pause (pour les logs) |

---

### 3.7 Sauvegarder / Charger une partie

```
lama game save [options]
lama game save [options] < <fichier_id>
lama game load <fichier_sauvegarde> [options]
```

| Option | Description |
|--------|-------------|
| `--id <game_id>` | Identifiant de la partie |
| `--file <fichier>` | Fichier de destination/source |
| `--format <fmt>` | `json`, `binary` |
| `--auto` | Active la sauvegarde automatique |
| `--auto-interval <sec>` | Intervalle de sauvegarde automatique |

---

## 4. Jouer

Ces commandes sont disponibles une fois qu'un joueur a rejoint une partie active.

```
lama play <action> [arguments] [options]
```

---

### 4.1 Placer un mot

```
lama play <case> <mot> <direction> [options]
```

| Argument | Valeurs | Description |
|----------|---------|-------------|
| `<case>` | ex: `H8`, `A1`, `Z26` | Case de départ (lettre = colonne, chiffre = ligne) |
| `<mot>` | texte en majuscules | Le mot à placer |
| `<direction>` | `H` ou `V` | Horizontal ou Vertical |

| Option | Description |
|--------|-------------|
| `--player <pseudo>` | Précise le joueur (si plusieurs sur la même console) |
| `--dry-run` | Simule le coup sans le jouer (affiche le score potentiel) |
| `--confirm` | Joue sans demander confirmation |
| `--joker <pos>=<lettre>` | Définit la lettre représentée par un joker à la position `<pos>` |

**Gestion des jokers :**
```bash
# Placer MAISON avec un joker en position 3 qui représente 'I'
lama play H8 MAISON H --joker 3=I

# Placer MAISON avec deux jokers
lama play H8 MAISON H --joker 2=A --joker 3=I
```

**Exemples :**
```bash
lama play H8 MAISON H
lama play H13 NOISETTE V
lama play A1 ZEN H --dry-run
lama play B3 QUARTZ H --player philippe --confirm
```

> **Alias de compatibilité :** syntaxe `lama play H8 MAISON H` conservée telle quelle.

---

### 4.2 Passer son tour

```
lama play pass [options]
lama play nothing [options]
```

| Option | Description |
|--------|-------------|
| `--player <pseudo>` | Précise le joueur |
| `--confirm` | Passe sans demander confirmation |

> **Alias de compatibilité :** `lama play nothing` → `lama play pass`

---

### 4.3 Échanger des lettres

```
lama play swap <lettres> [options]
```

| Argument | Description |
|----------|-------------|
| `<lettres>` | Lettres à échanger, ex: `AEI` ou `A,E,I` |

| Option | Description |
|--------|-------------|
| `--player <pseudo>` | Précise le joueur |
| `--all` | Échange toutes ses lettres |
| `--confirm` | Échange sans demander confirmation |

**Exemples :**
```bash
lama play swap AEI
lama play swap A,E,I --player sophie
lama play swap --all --confirm
```

---

### 4.4 Contester un mot (challenge)

```
lama play challenge [options]
```

| Option | Description |
|--------|-------------|
| `--player <pseudo>` | Joueur qui conteste |
| `--word <mot>` | Mot contesté (si plusieurs possibles) |
| `--confirm` | Conteste sans demander confirmation |

---

### 4.5 Vérifier un mot sans jouer

```
lama play check <mot> [options]
```

| Option | Description |
|--------|-------------|
| `--lang <code>` | Langue du dictionnaire à utiliser |
| `--definition` | Affiche la définition si disponible |

**Exemples :**
```bash
lama play check QUARTZ
lama play check NOISETTE --definition
lama play check ZORGLUB --lang fr
```

---

### 4.6 Simuler un coup (calcul de score)

```
lama play simulate <case> <mot> <direction> [options]
```

Identique à `lama play <case> <mot> <direction> --dry-run` mais utilisable hors tour.

| Option | Description |
|--------|-------------|
| `--show-bonus` | Affiche les cases bonus activées |
| `--show-words` | Affiche tous les mots formés |

---

## 5. Affichage en jeu

### `lama show`

```
lama show <élément> [options]
```

---

### 5.1 Afficher le rack

```
lama show rack [options]
```

| Option | Description |
|--------|-------------|
| `--player <pseudo>` | Rack d'un joueur spécifique |
| `--sorted` | Trie les lettres alphabétiquement |
| `--with-values` | Affiche la valeur en points de chaque lettre |
| `--count` | Affiche uniquement le nombre de lettres |

**Exemples :**
```bash
lama show rack
lama show rack --with-values
lama show rack --player philippe --sorted
```

> **Alias de compatibilité :** `lama show rack` conservé tel quel.

---

### 5.2 Afficher le plateau

```
lama show board [options]
lama show plateau [options]
```

| Option | Description |
|--------|-------------|
| `--id <game_id>` | Partie spécifique |
| `--zoom <n>` | Niveau de zoom : `1` (compact), `2` (normal), `3` (large) |
| `--no-bonus` | Masque les cases bonus |
| `--no-letters` | Masque les lettres placées (affiche la grille vide) |
| `--highlight <case>` | Met en évidence une case ou une zone |
| `--last-move` | Met en évidence le dernier coup joué |
| `--coords` | Affiche les coordonnées (activé par défaut) |
| `--no-coords` | Masque les coordonnées |
| `--ascii` | Force le rendu en ASCII pur (pas d'Unicode) |

**Exemples :**
```bash
lama show board
lama show board --zoom 3 --last-move
lama show board --no-bonus --ascii
lama show plateau --highlight H8
```

> **Alias de compatibilité :** `lama show plateau` → `lama show board`

---

### 5.3 Afficher les scores

```
lama show scores [options]
```

| Option | Description |
|--------|-------------|
| `--id <game_id>` | Partie spécifique |
| `--sort-by-score` | Trie par score décroissant |
| `--with-history` | Affiche le détail tour par tour |
| `--player <pseudo>` | Score d'un joueur spécifique |

---

### 5.4 Afficher l'historique des coups

```
lama show history [options]
```

| Option | Description |
|--------|-------------|
| `--id <game_id>` | Partie spécifique |
| `--last <n>` | Affiche les `n` derniers coups |
| `--player <pseudo>` | Filtre par joueur |
| `--with-scores` | Affiche les points gagnés à chaque coup |
| `--reverse` | Du plus récent au plus ancien (défaut) |
| `--chronological` | Du plus ancien au plus récent |

---

### 5.5 Afficher les informations de la partie

```
lama show game [options]
```

| Option | Description |
|--------|-------------|
| `--id <game_id>` | Partie spécifique |
| `--full` | Toutes les informations disponibles |
| `--with-config` | Affiche la configuration de la partie |
| `--with-bag-count` | Nombre de lettres restantes dans le sac |
| `--with-bag-letters` | Lettres restantes (si autorisé par les règles) |
| `--current-player` | Affiche qui doit jouer |
| `--time-remaining` | Affiche le temps restant pour le tour |

---

### 5.6 Afficher les suggestions (aide au jeu)

```
lama show hints [options]
```

| Option | Description |
|--------|-------------|
| `--top <n>` | Affiche les `n` meilleures suggestions (défaut: `5`) |
| `--min-score <n>` | Score minimum pour afficher une suggestion |
| `--player <pseudo>` | Rack du joueur ciblé |
| `--confirm` | Désactive l'avertissement "vous utilisez une aide" |

> ⚠️ L'usage des hints est loggé et peut être désactivé par les règles de la partie.

---

## 6. Dictionnaire

```
lama dict <action> [arguments] [options]
```

| Sous-commande | Description |
|---------------|-------------|
| `lama dict check <mot>` | Vérifie si un mot est valide |
| `lama dict check <mot> --lang <code>` | Vérifie dans une langue spécifique |
| `lama dict check <mot> --definition` | Affiche la définition |
| `lama dict search <pattern>` | Recherche par motif (ex: `M?ISON`, `*ETTE`) |
| `lama dict search <pattern> --lang <code>` | Recherche dans une langue |
| `lama dict search <pattern> --max <n>` | Limite les résultats |
| `lama dict anagram <lettres>` | Trouve les anagrammes possibles |
| `lama dict anagram <lettres> --min-length <n>` | Longueur minimale des anagrammes |
| `lama dict list` | Liste les dictionnaires disponibles |
| `lama dict info <code>` | Informations sur un dictionnaire |
| `lama dict install <code>` | Installe un dictionnaire |
| `lama dict remove <code>` | Supprime un dictionnaire |
| `lama dict update <code>` | Met à jour un dictionnaire |
| `lama dict add-word <mot> <lang>` | Ajoute un mot au dictionnaire personnalisé |
| `lama dict remove-word <mot> <lang>` | Supprime un mot du dictionnaire personnalisé |

**Exemples :**
```bash
lama dict check QUARTZ --definition
lama dict search "?OISETTE" --lang fr
lama dict anagram NOISETTE --min-length 4
lama dict install en
```

---

## 7. Tournois

```
lama tournament <action> [arguments] [options]
```

| Sous-commande | Description |
|---------------|-------------|
| `lama tournament create <nom> [options]` | Crée un tournoi |
| `lama tournament join <pseudo> --id <id>` | Rejoint un tournoi |
| `lama tournament list` | Liste les tournois |
| `lama tournament show --id <id>` | Détails d'un tournoi |
| `lama tournament start --id <id>` | Démarre un tournoi |
| `lama tournament end --id <id>` | Termine un tournoi |
| `lama tournament standings --id <id>` | Classement du tournoi |
| `lama tournament standings --id <id> --round <n>` | Classement d'une ronde |

**Options de création de tournoi :**

| Option | Valeurs | Défaut | Description |
|--------|---------|--------|-------------|
| `--format <type>` | `round-robin`, `elimination`, `swiss` | `round-robin` | Format du tournoi |
| `--rounds <n>` | entier > 0 | `3` | Nombre de rondes |
| `--players <n>` | `2` à `32` | `4` | Nombre de joueurs max |
| `--game-options <fichier>` | chemin JSON | | Options de partie communes à toutes les rondes |
| `--time-limit <sec>` | entier | `0` | Temps limite par tour |
| `--private` | | false | Tournoi privé |

---

## 8. Configuration personnelle

```
lama config <action> [arguments] [options]
```

| Sous-commande | Description |
|---------------|-------------|
| `lama config show` | Affiche la configuration personnelle |
| `lama config set <clé> <valeur>` | Modifie un paramètre |
| `lama config reset` | Remet la configuration par défaut |
| `lama config reset <clé>` | Remet un paramètre par défaut |
| `lama config export <fichier>` | Exporte la configuration |
| `lama config import <fichier>` | Importe une configuration |

**Paramètres disponibles :**

| Clé | Valeurs | Défaut | Description |
|-----|---------|--------|-------------|
| `display.color` | `true`, `false` | `true` | Activer les couleurs |
| `display.high-contrast` | `true`, `false` | `false` | Contraste élevé |
| `display.zoom` | `1`, `2`, `3` | `2` | Zoom du plateau |
| `display.ascii` | `true`, `false` | `false` | Rendu ASCII pur |
| `display.lang` | `fr`, `en`, ... | `fr` | Langue de l'interface |
| `display.coords` | `true`, `false` | `true` | Afficher coordonnées |
| `game.confirm-moves` | `true`, `false` | `true` | Demander confirmation avant chaque coup |
| `game.show-score-preview` | `true`, `false` | `true` | Prévisualiser le score avant de jouer |
| `game.auto-sort-rack` | `true`, `false` | `false` | Trier automatiquement le rack |
| `accessibility.screen-reader` | `true`, `false` | `false` | Mode lecteur d'écran |
| `accessibility.verbose-board` | `true`, `false` | `false` | Description textuelle du plateau |

---

## 9. Aide contextuelle

```
lama --help
lama <commande> --help
lama <commande> <sous-commande> --help
```

**Exemples :**
```bash
lama --help
lama game --help
lama game create --help
lama play --help
lama show board --help
lama dict --help
lama tournament --help
```

---

## 10. Tableau récapitulatif des alias de compatibilité

| Ancienne syntaxe | Nouvelle syntaxe recommandée |
|------------------|------------------------------|
| `lama create new game` | `lama game create` |
| `lama join as <pseudo> game` | `lama game join <pseudo>` |
| `lama list games` | `lama game list` |
| `lama list games --with-scores` | `lama game list --with-scores` |
| `lama list games --with-scores --sort-by-score` | `lama game list --with-scores --sort-by score --sort-desc` |
| `lama end game` | `lama game end` |
| `lama end game --with-scores` | `lama game end --with-scores` |
| `lama end all games` | `lama game end all` |
| `lama restart system` | `lama system restart` |
| `lama play H8 MAISON H` | `lama play H8 MAISON H` ✅ (inchangé) |
| `lama play nothing` | `lama play pass` |
| `lama show plateau` | `lama show board` |

---

## 11. Codes de retour (exit codes)

| Code | Signification |
|------|---------------|
| `0` | Succès |
| `1` | Erreur générale |
| `2` | Erreur de syntaxe / argument invalide |
| `3` | Partie introuvable |
| `4` | Joueur introuvable |
| `5` | Coup invalide (mot hors dictionnaire) |
| `6` | Coup invalide (placement impossible) |
| `7` | Coup invalide (lettres indisponibles) |
| `8` | Pas votre tour |
| `9` | Partie déjà terminée |
| `10` | Timeout dépassé |
| `11` | Droits insuffisants |
| `20` | Erreur réseau / système indisponible |
| `21` | Erreur de base de données |
| `22` | Erreur de fichier (lecture/écriture) |

---

## 12. Exemples de sessions complètes

### Session type - Partie à 2 joueurs

```bash
# Créer une partie 19x19 avec timer
lama game create --size 19 --players 2 --time-limit 120 --lang fr > game_id.txt

# Les deux joueurs rejoignent
lama game join philippe < game_id.txt
lama game join sophie < game_id.txt

# Philippe joue en premier
lama play H8 MAISON H
lama show board --last-move

# Sophie vérifie d'abord un mot
lama dict check NOISETTE --definition
lama play I8 NOISETTE V
lama show scores

# Philippe échange des lettres
lama play swap AEI

# Sophie passe son tour
lama play pass --player sophie

# Fin de partie avec classement
lama game end < game_id.txt --with-scores --export partie_finale.json
```

### Session type - Administration

```bash
# Lister toutes les parties en cours avec scores
lama game list --with-scores --sort-by score --sort-desc

# Vérifier l'état du système
lama system status

# Sauvegarder et redémarrer
lama system restart --save

# Après redémarrage, vérifier les parties
lama game list --filter-status paused
```

---

*LAMA v1.0 - Référence CLI complète*
*Pour toute suggestion ou rapport de bug : `lama system diagnostics --report`*