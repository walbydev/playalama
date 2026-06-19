# LAMA - Reference CLI (état code actuel HARMONISÉ 2026-06-18)

Ce document décrit **ce qui est réellement implémenté aujourd'hui** dans `src/Console/Lama.Console`.

**MAJ du 2026-06-18** : Audit complet synchronisé avec docs/roadmap/PROGRESSION.md et état réel du code. Tous les stubs obsolètes (`player.create`, `tournament.create`, `system.status`, `system.restart`) ont été marqués comme ✅ implémentés. Ajout des commandes manquantes : `rating.*`, `player.list/show/update`, `system.clean`.

Source de vérité:
- `src/Console/Lama.Console/Program.cs` (30+ commandes ICommand enregistrées)
- `src/Console/Lama.Console/Services/CommandContextParser.cs`
- `src/Console/Lama.Console/Services/CommandDispatcher.cs`
- `src/Console/Lama.Console/Commands/**/*.cs` (44 fichiers commande)
- `src/Console/Lama.Console/Services/ExitCodes.cs`

---

## 1) Format CLI actuellement supporte

Le parser supporte trois formes:

```text
lama <groupe> <action> [arguments...] [options]
lama <action> [arguments...] [options]               # login/logout
lama system account <action> [arguments...] [options]
```

Cas particuliers:
- `lama` (sans argument) -> mode interactif textuel.
- `lama interactive` / `lama shell` / `lama ui` -> mode interactif textuel.
- `lama --help` / `lama -h` -> aide globale.
- `lama --version` / `lama -v` -> version.

---

## 2) Options globales parsees

Ces options sont parsees dans `CommandContext` (pas toujours exploitees par toutes les commandes):

| Option | Alias | Etat |
|---|---|---|
| `--help` | `-h` | implemente dans `CommandLineMode` |
| `--version` | `-v` | implemente dans `CommandLineMode` |
| `--verbose` | `-V` | parse |
| `--quiet` | `-q` | parse |
| `--no-color` | | parse |
| `--high-contrast` | | parse |
| `--lang <code>` | `-l` | parse |
| `--output <format>` | `-o` | parse |
| `--game-id <id>` | | parse (surcharge session) |
| `--player <id>` | | parse (surcharge session) |

---

## 3) Commandes disponibles (état réel)

**Total implémenté** : 30+ commandes, toutes opérationnelles (aucune stub dans Program.cs).

### 3.1 `game` (8 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama game create [--level ...]` | ✅ | crée partie + session locale, mode local/online supporté |
| `lama game join <nom>` | ✅ | rejoint partie active, branchés sur mode runtime |
| `lama game list [--output text\|json\|csv]` | ✅ | liste les parties persistées, supports formats multiples |
| `lama game show [gameId]` | ✅ | détails partie (session courante ou `gameId` explicite), formats multiples |
| `lama game pause` | ✅ | snapshot persistant de la partie courante |
| `lama game save [--file <chemin>]` | ✅ | sauvegarde + export optionnel JSON |
| `lama game end` | ✅ | termine partie + efface session, intègre rating |

### 3.2 `play` (6 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama play move <case> <mot> <direction>` | ✅ | pose mot, valide croisements, supporte notation joker minuscule (ex: `lAMA`) |
| `lama play pass` | ✅ | passe le tour |
| `lama play swap <lettres> [--all]` | ✅ | échange réel des lettres + consomme le tour |
| `lama play challenge` | ✅ | conteste dernier mot joué, logique métier complète |
| `lama play check <case> <mot> <direction>` | ✅ | vérifie validité d'un coup sans le jouer |

### 3.3 `show` (4 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama show board` | ✅ | rendu Spectre.Console, mode local complet |
| `lama show rack [--with-values]` | ✅ | rack joueur courant avec valeurs optionnelles |
| `lama show scores [--output text\|json\|csv]` | ✅ | scores + tri, formats multiples |
| `lama show history [--last N] [--output text\|json\|csv]` | ✅ | historique des coups avec limite optionnelle, formats multiples |

### 3.4 `rating` (3 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama rating show` | ✅ | affiche rating ELO du joueur courant |
| `lama rating leaderboard [--queue open\|tournament\|global]` | ✅ | classements par queue (open=casual, tournament=compétitif, global=tous) |
| `lama rating stats` | ✅ | statistiques joueur (parties, victoires, score moyen) |

### 3.5 `dict` (3 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama dict check <mot>` | ✅ | vérifie mot dans dictionnaire, exit code 0/5 |
| `lama dict search <motif>` | ✅ | recherche mots, `?` = wildcard |
| `lama dict anagram <lettres> [--min-length N]` | ✅ | recherche sous-anagrammes avec limite longueur |

### 3.6 `player` (4 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama player create <nom>` | ✅ | crée profil joueur local persisté en session |
| `lama player list` | ✅ | liste les profils joueurs |
| `lama player show [<nom>]` | ✅ | détails profil (courant ou spécifié) |
| `lama player update <nom> [--nickname <new>] [--level <lvl>]` | ✅ | modifie profil joueur |

### 3.7 `tournament` (1 commande)

| Commande | Etat | Notes |
|---|---|---|
| `lama tournament create <nom>` | ✅ | crée tournoi (aujourd'hui : partie GameLevel.Tournament) |

### 3.8 `system` (8 commandes)

| Commande | Etat | Notes |
|---|---|---|
| `lama system setup` | ✅ | initialisation SuperAdmin, génère token initial |
| `lama system status [--output text\|json\|csv]` | ✅ | diagnostic système (version, comptes, parties, sessions) |
| `lama system restart` | ✅ | redémarrage logique in-process (purge cache + restauration partie active) |
| `lama system clean` | ✅ | nettoyage cache en mémoire, optionnel logs |
| `lama system account create <username>` | ✅ | crée un compte Admin local |
| `lama system account list` | ✅ | liste les comptes Admin |
| `lama system account revoke <username>` | ✅ | révoque un compte Admin |

### 3.9 `authentication` (2 commandes – mono-niveau)

| Commande | Etat | Notes |
|---|---|---|
| `lama login [--username <nom>]` | ✅ | authentification admin/superadmin, génère token local |
| `lama logout` | ✅ | suppression token session |

---

## 4) Exemples validables sur l'état actuel

```bash
# === AIDE / VERSION ===
lama --help
lama --version

# === MODE INTERACTIF ===
lama
lama interactive

# === AUTHENTIFICATION ===
lama login --username superadmin
lama logout

# === ADMINISTRATION COMPTES ===
lama system setup                           # Setup initial SuperAdmin
lama system account create admin2
lama system account list
lama system account revoke admin2

# === ADMINISTRATION SYSTÈME ===
lama system status --output json            # Diagnostic système
lama system status --output text
lama system clean                           # Nettoyage cache/logs
lama system restart                         # Redémarrage logique

# === PROFILS JOUEURS ===
lama player create alice
lama player create bob
lama player list                            # Liste tous les profils
lama player show alice                      # Détails profil alice
lama player update alice --nickname Alice   # Modifie surnom

# === PARTIES (COMMANDE PAR COMMANDE) ===
lama game create                            # Crée partie, hôte devient joueur 1
lama game create --level competitive        # Crée partie compétitive
lama game list                              # Liste toutes les parties
lama game list --output json                # Formats soutenus
lama game list --output csv
lama game show                              # Détails partie active (session courante)
lama game show <game-id>                    # Détails partie spécifique
lama game join alice                        # Rejoint partie (alice = host nickname)
lama show board                             # Montre le plateau
lama show rack                              # Montre rack joueur courant
lama show rack --with-values                # Montre rack avec valeurs lettres
lama show scores                            # Montre scores courants
lama show scores --output json              # Format JSON

# === COUPS DE JEU ===
lama play move H8 LAMA H                    # Pose mot horizontal
lama play move J8 MAISON V                  # Pose mot vertical (croisement valide)
lama play move H8 lAMA H                    # Notation joker : 'l' minuscule force joker
lama play check H8 LAMA H                   # Vérifie coup sans le jouer (dry-run)
lama play pass                              # Passe tour
lama play swap AEI                          # Échange lettres spécifiques
lama play swap --all                        # Échange toutes les lettres
lama play challenge                         # Conteste dernier mot joué

# === HISTORIQUE PARTIE ===
lama show history                           # Historique complet des coups
lama show history --last 3                  # Derniers 3 coups
lama show history --output json             # Format JSON
lama show history --output csv --last 5     # Format CSV, 5 derniers coups

# === RATING / CLASSEMENTS ===
lama rating show                            # ELO du joueur courant
lama rating leaderboard                     # Classement (par défaut : open)
lama rating leaderboard --queue open        # Classement casual
lama rating leaderboard --queue tournament  # Classement tournoi
lama rating leaderboard --queue global      # Classement global
lama rating stats                           # Stats joueur courant

# === TORNOIS ===
lama tournament create "LAMA Classic 2026"  # Crée tornoi (GameLevel.Tournament)

# === DICTIONNAIRE ===
lama dict check QUARTZ                      # Vérifie si mot est valide
lama dict check XYZABC                      # Retour exit code 5 si invalide
lama dict search "?OISETTE"                 # Recherche mots (? = wildcard)
lama dict anagram NOISETTE                  # Anagrammes
lama dict anagram NOISETTE --min-length 4   # Anagrammes min 4 lettres

# === MODE ONLINE (alpha, avec serveur Lama.Server) ===
export LAMA_RUNTIME_MODE=online
export LAMA_SERVER_URL=http://127.0.0.1:5055
lama game create                            # Route vers serveur online
lama game list                              # Récupère depuis serveur
# Scripts disponibles : tools/scripts/e2e-online-smoke.sh
```

---

## 5) Codes de retour (actuels)

Definis dans `src/Console/Lama.Console/Services/ExitCodes.cs`:

| Code | Signification |
|---|---|
| `0` | Succes |
| `1` | Erreur generale |
| `2` | Argument invalide |
| `3` | Partie introuvable |
| `5` | Mot hors dictionnaire |
| `6` | Placement impossible |
| `8` | Pas votre tour |
| `10` | Timeout |
| `11` | Acces refuse |

---

## 6) Notes sur permissions

Le controle d'acces est applique par `AccessControlMiddleware` avant execution de la commande.
Cas notables:
- `Admin` ne peut pas jouer (`play.*`, `show.rack`).
- `Spectator` est en lecture seule.
- Les aides (`dict.check/search/anagram`, `play.check`, `show.hints`, etc.) sont limitees selon `GameLevel`.
- `game.create`, `game.join`, `game.list`, `login`, `logout`, `system.setup` sont traites comme commandes publiques ACL.

---

## 7) Harmonisation avec l'ancienne spec

L'ancienne version de ce document (avant 2026-06-18) listait un périmètre CLI beaucoup plus large (`system.logs`, `player.stats`, `game.resume`, `game.load`, `config.*`, etc.). Ces commandes ne sont pas implémentées dans le code.

**À jour 2026-06-18** : Ce fichier reflète maintenant **exactement** l'état réel du code :
- ✅ Les stubs obsolètes (`player.create`, `tournament.create`, `system.status`, `system.restart`) ont été levés et marqués comme ✅ opérationnels.
- ✅ Les commandes manquantes (`rating.*`, `player.list/show/update`, `system.clean`) ont été ajoutées.
- ✅ Les formats de sortie (`--output json/csv/text`) ont été documentés là où supportés.
- ✅ Les options avancées (`--last N`, `--queue`, etc.) ont été clarifiées.

## 8) Statut de maintenance

Ce document est maintenu en synchronisation avec :
- `src/Console/Lama.Console/Program.cs` (enregistrement commandes)
- `docs/roadmap/PROGRESSION.md` (historique d'implémentation)
- `AGENTS.md` (état composants)

**Mise à jour suivante prévue après** : ajout nouvelle commande CLI ou changement signature existante.

---

**Questions / Corrections ?** Consulter `AGENTS.md` section "Règles pour agents".
