# Deploiement one-click (images locales -> VPS)

Script principal: `tools/deployments/deploy-oneclick-images.sh`

## Ce que fait le script

1. Build local des images Docker:
   - `lama-server`
   - `lama-game-webapp`
   - `lama-portal-webapp`
2. Export des images en archive gzip.
3. Envoi sur le VPS via `rsync`.
4. `docker load` sur le VPS.
5. Redeploiement `docker compose` sans build distant.
6. (Optionnel) execution `certbot` profile.
7. (Optionnel) healthchecks HTTPS.
8. (Optionnel) mode `--ultra-safe` avec preflight ACME avant certbot.

## Prerequis

- Local: `docker`, `ssh`, `rsync`, `curl`, `gzip`, `tar`
- VPS: `docker`, `docker compose`
- DNS configure pour:
  - `playalama.online`

## Exemple standard

```bash
cd /home/philippe/RiderProjects/Games/Lama
chmod +x tools/deployments/deploy-oneclick-images.sh
./tools/deployments/deploy-oneclick-images.sh \
  --target debian@playalama.online \
  --ssh-key ~/.ssh/machines/playalama.key
```

## Dry run

```bash
cd /home/philippe/RiderProjects/Games/Lama
./tools/deployments/deploy-oneclick-images.sh \
  --target debian@playalama.online \
  --ssh-key ~/.ssh/machines/playalama.key \
  --dry-run
```

## Mode ultra-safe

Le mode `--ultra-safe` ajoute un preflight ACME:

1. creation temporaire d'un fichier sous `/.well-known/acme-challenge/`
2. verification HTTP depuis l'exterieur sur `playalama.online`
3. si le test echoue, `certbot` est saute pour ce deploy

```bash
cd /home/philippe/RiderProjects/Games/Lama
./tools/deployments/deploy-oneclick-images.sh \
  --target debian@playalama.online \
  --ssh-key ~/.ssh/machines/playalama.key \
  --ultra-safe
```

## Notes

- Donnees persistantes par defaut dans `/srv/playalama`.
- Application/compose par defaut dans `/opt/playalama`.
- Les checks utilisent des requetes GET (`curl`), pas HEAD (`curl -I`).
- `--ultra-safe` est recommande jusqu'a ce que le port 80 public reponde correctement sur le VPS.

