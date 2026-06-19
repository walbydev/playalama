# Liste courte — jeu classique

**Date** : 2026-06-19
**Statut** : À finaliser

---

Objectif: finaliser un mode classique **jouable de bout en bout** avant toute reprise des evolutions futures.

## Etat d'execution P0

- `CG-01` ✅ complete
- `CG-02` ✅ complete
- `CG-03` ✅ complete
- `CG-04` ✅ complete

## Priorite P0 (bloquant sortie "jeu fonctionnel")

- **CG-01 - Parcours CLI complet fiable**
  - Create -> Join -> Move -> Pass/Swap -> Show -> End fonctionne sans incoherence d'etat.
  - Critere de done:
    - scenario E2E passe en mode reel,
    - codes de sortie coherents,
    - messages d'erreur exploitables.

- **CG-02 - Parcours interactif complet fiable**
  - Une partie complete est jouable depuis le mode interactif sans retour "placeholder".
  - Critere de done:
    - boucle de tour stable (move/check/challenge/pass/swap),
    - affichage board/rack/scores apres action,
    - session active correctement maintenue.

- **CG-03 - Coherence `play.check` vs `play.move`**
  - Un coup valide en check ne doit pas echouer au move pour des raisons de consommation rack/croisements.
  - Critere de done:
    - tests de regression (domain + console) verts,
    - pas de retour code `6` sur les cas valides connus.

- **CG-04 - Scoring robuste sur croisements/jokers**
  - Le score est correct pour: lettres existantes, jokers, bonus de mot/lettre, challenge.
  - Critere de done:
    - couverture de tests des cas limites,
    - aucun ecart connu sur scenarios de reference.

## Priorite P1 (qualite immediate)

- **CG-05 - Sorties JSON/CSV durcies**
  - Formats machine stables pour `game.*` et `show.*`.
  - Critere de done:
    - serialisation unifiee,
    - tests E2E JSON/CSV sur commandes principales.

- **CG-06 - Historique exploitable et fiable**
  - `show.history` reflet exact des coups et du score associe.
  - Critere de done:
    - tri/filtrage (`--last`) fiable,
    - scenarios E2E avec historiques non triviaux.

- **CG-07 - UX d'erreur et guidance unifiees**
  - Messages d'erreur coherents (CLI + interactif) sur les commandes de tour.
  - Critere de done:
    - wording harmonise,
    - aide contextuelle claire pour les cas frequents.

## Priorite P2 (stabilisation avant release)

- **CG-08 - Performance de base en E2E reel**
  - Temps d'execution acceptable sur scripts E2E standards.

- **CG-09 - Nettoyage docs "etat reel"**
  - `README.md`, `AGENTS.md`, `docs/defines-CLI.md` alignes sur le comportement courant.

## Sprint court recommande (ordre)

1. `CG-01` + `CG-03`
2. `CG-04`
3. `CG-02`
4. `CG-05` + `CG-06`
5. `CG-07` puis `CG-09`

## Definition du jalon "Jeu fonctionnel"

Le jalon est valide si:

- P0 completes et testees,
- E2E CLI reel "partie complete" passe de facon deterministe,
- une recette manuelle interactive complete est faisable sans blocage,
- aucun bug critique ouvert sur `play.*`, `show.*`, `game.*`.
