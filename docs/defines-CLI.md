# LAMA - Reference CLI (etat code actuel)

Ce document decrit **ce qui est reellement code aujourd'hui** dans `src/Console/Lama.Console`.

Source de verite:
- `src/Console/Lama.Console/Services/CommandContextParser.cs`
- `src/Console/Lama.Console/Services/CommandDispatcher.cs`
- `src/Console/Lama.Console/Commands/**/*.cs`
- `src/Console/Lama.Console/Services/ExitCodes.cs`

---

## 1) Format CLI actuellement supporte

Le parser supporte le format:

```text
lama <groupe> <action> [arguments...] [options]
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

## 3) Commandes disponibles (etat reel)

## 3.1 `game`

| Commande | Etat | Notes |
|---|---|---|
| `lama game create [--level ...]` | ✅ | cree partie + session locale |
| `lama game join <nom>` | ✅ | rejoint partie active |
| `lama game end` | ✅ | termine partie + efface session |
| `lama game list` | 🟡 | stub (retour non implemente) |
| `lama game show` | 🟡 | stub |
| `lama game pause` | 🟡 | stub |
| `lama game save` | 🟡 | stub |

### 3.2 `play`

| Commande | Etat | Notes |
|---|---|---|
| `lama play move <case> <mot> <direction>` | ✅ | supporte `--dry-run` |
| `lama play pass` | ✅ | passe le tour |
| `lama play swap <lettres> [--all]` | 🟡 | stub cote commande |
| `lama play challenge` | 🟡 | stub |
| `lama play check <case> <mot> <direction>` | 🟡 | stub |

### 3.3 `show`

| Commande | Etat | Notes |
|---|---|---|
| `lama show board` | ✅ | rendu Spectre.Console |
| `lama show rack [--with-values]` | ✅ | rack joueur courant |
| `lama show scores` | ✅ | scores + tri |
| `lama show history` | 🟡 | stub |

### 3.4 `dict`

| Commande | Etat | Notes |
|---|---|---|
| `lama dict check <mot>` | ✅ | exit code 0/5 |
| `lama dict search <motif>` | ✅ | `?` -> wildcard |
| `lama dict anagram <lettres> [--min-length N]` | ✅ | recherche sous-anagrammes |

### 3.5 `player`

| Commande | Etat | Notes |
|---|---|---|
| `lama player create <nom>` | 🟡 | stub |

### 3.6 `tournament`

| Commande | Etat | Notes |
|---|---|---|
| `lama tournament create <nom>` | 🟡 | stub |

### 3.7 `system`

| Commande | Etat | Notes |
|---|---|---|
| `lama system setup` | ✅ | initialisation SuperAdmin |
| `lama system status` | 🟡 | stub |
| `lama system restart` | 🟡 | stub |

---

## 4) Commandes enregistrees mais non atteignables (limite parser)

Le parser construit uniquement `groupe.action`.
Ces `CommandId` existent dans le code, mais ne peuvent pas etre adresses avec le format actuel:

- `login`
- `logout`
- `system.account.create`
- `system.account.list`
- `system.account.revoke`

Impact pratique:
- `lama login` et `lama logout` ne passent pas le parser (moins de 2 arguments).
- `lama system account create` produit `CommandId = system.account`, pas `system.account.create`.

---

## 5) Exemples validables sur l'etat actuel

```bash
# Aide / version
lama --help
lama --version

# Mode interactif
lama
lama interactive

# Partie (commande par commande)
lama game create
lama game join alice
lama show board
lama show rack --with-values
lama play move H8 LAMA H --dry-run
lama play pass
lama game end

# Dictionnaire
lama dict check QUARTZ
lama dict search "?OISETTE"
lama dict anagram NOISETTE --min-length 4
```

---

## 6) Codes de retour (actuels)

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

## 7) Notes sur permissions

Le controle d'acces est applique par `AccessControlMiddleware` avant execution de la commande.
Cas notables:
- `Admin` ne peut pas jouer (`play.*`, `show.rack`).
- `Spectator` est en lecture seule.
- Les aides (`dict.check/search/anagram`, `play.check`, `show.hints`, etc.) sont limitees selon `GameLevel`.

---

## 8) Ecart avec l'ancienne spec

L'ancienne version de ce document listait un perimetre CLI beaucoup plus large
(`system.logs`, `player.stats`, `game.resume`, `game.load`, `config.*`, etc.).
Ces commandes ne sont **pas** implementees dans le code actuel.

Ce fichier est volontairement limite au comportement observe dans la base de code.


