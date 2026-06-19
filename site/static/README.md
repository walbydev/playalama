# Site statique Playalama

Ce dossier contient la version source du site vitrine statique.

## Structure

- `index.html`: page d'accueil orientee produit + section technique
- `download/index.html`: page de telechargement multi-plateforme
- `assets/css/site.css`: style partage (themes clair/sombre inclus)
- `assets/js/theme.js`: bascule de theme accessible avec persistence locale
- `assets/js/download.js`: logique de selection version/canal/plateforme

## Exposition nginx

Le montage Docker recommande:

- `./site/static` vers `/usr/share/nginx/html`
- `./artifacts/zip` vers `/usr/share/nginx/html/downloads`

## URL hybrides de telechargement

- `https://playalama.online/downloads/<fichier>.zip`
- `https://downloads.playalama.online/<fichier>.zip`

