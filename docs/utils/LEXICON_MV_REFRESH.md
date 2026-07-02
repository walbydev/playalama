# Rafraîchissement de la vue matérialisée du lexique

## Contexte

`lexicon.mv_valid_words` est une **vue matérialisée** (snapshot statique) qui liste
les `word_id` des mots ne possédant aucune définition de type `abbrev`.
Elle est utilisée par `PostgresLexiconReader.LoadDictionary` pour exclure les abréviations
du dictionnaire de jeu.

La vue est créée automatiquement au démarrage du serveur via `EnsureSchemaAsync()`
(fichier `init-lexicon-schema.sql`), mais son contenu est **statique** : après toute
mise à jour du lexique (nouvel import, correction de définitions…),
**la vue doit être rafraîchie manuellement** pour refléter les changements.

## Commande de rafraîchissement

```sql
REFRESH MATERIALIZED VIEW CONCURRENTLY lexicon.mv_valid_words;
```

> L'option `CONCURRENTLY` permet le rafraîchissement **sans verrouiller les lectures**
> (nécessite l'index unique `ux_mv_valid_words_id`, déjà en place).

## Quand rafraîchir ?

| Opération | Rafraîchissement nécessaire ? |
|---|---|
| Import de nouveaux mots (`Lama.DictionaryImporter`) | ✅ Oui |
| Correction/ajout de définitions `abbrev` | ✅ Oui |
| Suppression de mots | ✅ Oui |
| Mise à jour des synonymes uniquement | ❌ Non |
| Redémarrage du serveur | ❌ Non (la vue est en base) |

## Intégration dans le workflow d'import

Si tu utilises `Lama.DictionaryImporter`, pense à ajouter le refresh en fin de script :

```bash
psql "$DATABASE_URL" -c "REFRESH MATERIALIZED VIEW CONCURRENTLY lexicon.mv_valid_words;"
```

Ou via make (si une cible `make db-import` existe) :

```bash
make db-import && psql "$DATABASE_URL" -c "REFRESH MATERIALIZED VIEW CONCURRENTLY lexicon.mv_valid_words;"
```

## Vérification rapide

Pour contrôler combien de mots valides sont actuellement en vue :

```sql
SELECT COUNT(*) FROM lexicon.mv_valid_words;

-- Comparer avec le total des mots :
SELECT COUNT(*) FROM lexicon.words WHERE language_code = 'fr';
```
