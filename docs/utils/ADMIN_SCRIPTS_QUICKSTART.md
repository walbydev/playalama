# Admin scripts — Quick Start

Boite à outils pour gérer les **joueurs** et **parties/sessions** sur `dev`, `staging`, `prod`.

Scripts:
- `tools/scripts/admin/admin-env.sh`
- `tools/scripts/admin/admin-games.sh`
- `tools/scripts/admin/admin-users.sh`
- `tools/scripts/admin/admin-reset.sh`

## 1) Vérifier l'environnement résolu

```bash
make admin-env ADMIN_ENV=dev
make admin-env ADMIN_ENV=staging
make admin-env ADMIN_ENV=prod
```

Par défaut:
- `dev` → `http://127.0.0.1:5201`
- `staging` → `https://staging.playalama.online`
- `prod` → `https://playalama.online`

Surcharge possible:
```bash
make admin-env ADMIN_ENV=staging ADMIN_SERVER_URL=https://staging.example.com
```

## 2) Gestion des parties/sessions

Lister les parties:
```bash
make admin-games ADMIN_ENV=dev ADMIN_ARGS="list --json"
```

Afficher une partie:
```bash
make admin-games ADMIN_ENV=staging ADMIN_ARGS="show --game-id <GAME_ID> --json"
```

Terminer toutes les parties actives (admin):
```bash
make admin-games ADMIN_ENV=prod ADMIN_SECRET='<SECRET>' ADMIN_ARGS="terminate-all --json"
```

Actions dev uniquement (endpoints internes):
```bash
make admin-games ADMIN_ENV=dev ADMIN_ARGS="clear-memory --json"
make admin-games ADMIN_ENV=dev ADMIN_ARGS="close --game-id <GAME_ID> --json"
```

## 3) Gestion des joueurs/comptes (périmètre actuel)

Lister tous les users (admin):
```bash
make admin-users ADMIN_ENV=staging ADMIN_SECRET='<SECRET>' ADMIN_ARGS="list --json"
```

Créer un compte joueur:
```bash
make admin-users ADMIN_ENV=staging ADMIN_ARGS="register --username alice --password 'secret123' --country FR --json"
```

Connexion compte:
```bash
make admin-users ADMIN_ENV=staging ADMIN_ARGS="login --username alice --password 'secret123' --json"
```

Consulter statut auth (avec JWT):
```bash
make admin-users ADMIN_ENV=staging ADMIN_TOKEN='<JWT>' ADMIN_ARGS="status --json"
```

Consulter / mettre à jour profil connecté:
```bash
make admin-users ADMIN_ENV=staging ADMIN_TOKEN='<JWT>' ADMIN_ARGS="profile --json"
make admin-users ADMIN_ENV=staging ADMIN_TOKEN='<JWT>' ADMIN_ARGS="update-profile --email alice@example.com --country FR --json"
```

## Notes de sécurité

- En `staging/prod`, préférer `ADMIN_SECRET` (`X-Admin-Secret`) ou `ADMIN_TOKEN` (Bearer JWT).
- Les endpoints `/internal/*` sont réservés au `dev`.
- Ce lot ne couvre pas un CRUD admin global des joueurs (liste globale/révocation/reset mot de passe).

## 4) Remises à zéro (par environnement)

Commande générique:
```bash
make admin-reset ADMIN_ENV=dev ADMIN_ARGS="reset-games --yes --json"
```

Raccourcis Make:
```bash
make admin-reset-games ADMIN_ENV=dev ADMIN_ARGS="--json"
make admin-reset-users ADMIN_ENV=dev ADMIN_ARGS="--json"
make admin-reset-stats ADMIN_ENV=dev ADMIN_ARGS="--json"
make admin-reset-all ADMIN_ENV=dev ADMIN_ARGS="--json"
make admin-ensure-root ADMIN_ENV=dev ADMIN_ARGS="--json"
```

Comportement par environnement:
- `reset-games`
  - `dev`: purge DB sessions + purge mémoire serveur.
  - `staging/prod`: terminaison des parties actives via endpoint admin.
- `reset-users`, `reset-stats`, `reset-all`, `ensure-root`
  - **dev uniquement** (bloqué en staging/prod).

`ensure-root` garantit un compte joueur Web `root/root` en environnement dev.
Le flux évite `register` (mot de passe minimum 6) et force un hash compatible serveur en base dev.
