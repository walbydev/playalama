# WebApp 1.1.0 - Lot 1

**Date** : 2026-06-20
**Statut** : En cours (branche feature)

## Objectif lot 1

Passer d'une priorite CLI (1.0.0) a une WebApp (1.1.0) avec:

- vitrine reprise du site existant,
- zone jeu simple et robuste,
- gestion profil simple,
- architecture Docker separee `lama-webapp` / `lama-server`.

## Hors scope lot 1

- mode tournoi,
- reglages avances,
- administration complete,
- classement avance.

## UX

- style plastifie, couleurs flashy et contraste fort,
- theme clair/sombre,
- densite visuelle confortable/compacte.

## Livrables techniques

- nouveaux projets `src/Web/Lama.GameWebApp` et `src/Web/Lama.PortalWebApp` (Blazor Server),
- tests `tests/Lama.GameWebApp.UnitTests`,
- docker `tools/docker/Dockerfile.webapp`,
- compose et nginx mis a jour pour separation des services.

