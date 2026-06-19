# Site statique Playalama

Ce dossier contient la version source du site vitrine statique.

## Structure

- `index.html`: page d'accueil publique du portail `https://playalama.online/`
- `download/index.html`: page de telechargement multi-plateforme
- `assets/css/site.css`: style partage (themes clair/sombre inclus)
- `assets/js/theme.js`: bascule de theme accessible avec persistence locale
- `assets/js/download.js`: logique de selection version/canal/plateforme
- `assets/js/status.js`: indicateur online/offline base sur `/health` pour les pages publiques
- `assets/data/releases.json`: source de verite des versions telechargeables
- `assets/img/favicon.svg`: favicon du portail
- `assets/img/og-card.svg`: image de partage social (Open Graph)

## Exposition nginx

Le montage Docker recommande:

- `./site/static` vers `/usr/share/nginx/html`
- `./artifacts/zip` vers `/opt/playalama-downloads`

Le reverse proxy expose ensuite:

- `https://playalama.online/` pour l'accueil du portail
- `https://playalama.online/download/` pour la page de téléchargement
- `https://playalama.online/downloads/<fichier>.zip` et `https://downloads.playalama.online/<fichier>.zip` pour les archives ZIP


