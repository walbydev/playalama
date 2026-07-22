-- ============================================================================
-- LAMA - Lexicon schema initialization
-- Purpose: Store dictionary words across languages (fr/en/de) with optional
--          definitions, synonyms and Wiktionary URL.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS lexicon;

CREATE TABLE IF NOT EXISTS lexicon.languages (
    code VARCHAR(8) PRIMARY KEY,
    label VARCHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO lexicon.languages (code, label)
VALUES
    ('fr', 'Francais'),
    ('en', 'English'),
    ('de', 'Deutsch')
ON CONFLICT (code) DO NOTHING;

CREATE TABLE IF NOT EXISTS lexicon.words (
    word_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    language_code VARCHAR(8) NOT NULL REFERENCES lexicon.languages(code),
    lemma TEXT NOT NULL,
    lemma_normalized TEXT NOT NULL,
    length INT NOT NULL,
    wiktionary_url TEXT,
    source VARCHAR(64) NOT NULL DEFAULT 'kaikki',
    source_entry_id TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_lexicon_words_length CHECK (length > 0),
    CONSTRAINT uq_lexicon_words_language_lemma_normalized UNIQUE (language_code, lemma_normalized)
);

CREATE INDEX IF NOT EXISTS idx_lexicon_words_language_length
    ON lexicon.words (language_code, length);

CREATE INDEX IF NOT EXISTS idx_lexicon_words_language_lemma
    ON lexicon.words (language_code, lemma);

CREATE TABLE IF NOT EXISTS lexicon.definitions (
    definition_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    word_id UUID NOT NULL REFERENCES lexicon.words(word_id) ON DELETE CASCADE,
    sense_index INT NOT NULL,
    part_of_speech VARCHAR(64),
    definition_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_lexicon_definitions_sense_index CHECK (sense_index >= 0),
    CONSTRAINT uq_lexicon_definitions_word_sense_text UNIQUE (word_id, sense_index, definition_text)
);

CREATE INDEX IF NOT EXISTS idx_lexicon_definitions_word
    ON lexicon.definitions (word_id);

CREATE TABLE IF NOT EXISTS lexicon.synonyms (
    synonym_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    word_id UUID NOT NULL REFERENCES lexicon.words(word_id) ON DELETE CASCADE,
    sense_index INT NOT NULL,
    synonym TEXT NOT NULL,
    synonym_normalized TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_lexicon_synonyms_sense_index CHECK (sense_index >= 0),
    CONSTRAINT uq_lexicon_synonyms_word_sense_synonym UNIQUE (word_id, sense_index, synonym_normalized)
);

CREATE INDEX IF NOT EXISTS idx_lexicon_synonyms_word
    ON lexicon.synonyms (word_id);

CREATE INDEX IF NOT EXISTS idx_lexicon_synonyms_normalized
    ON lexicon.synonyms (synonym_normalized);

CREATE TABLE IF NOT EXISTS lexicon.import_runs (
    run_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    environment VARCHAR(16) NOT NULL,
    language_code VARCHAR(8) NOT NULL REFERENCES lexicon.languages(code),
    source_file TEXT NOT NULL,
    sha256 CHAR(64) NOT NULL,
    options JSONB NOT NULL,
    started_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMPTZ,
    status VARCHAR(16) NOT NULL,
    words_count BIGINT NOT NULL DEFAULT 0,
    definitions_count BIGINT NOT NULL DEFAULT 0,
    synonyms_count BIGINT NOT NULL DEFAULT 0,
    error_message TEXT,
    CONSTRAINT ck_lexicon_import_runs_environment CHECK (environment IN ('dev', 'staging', 'prod')),
    CONSTRAINT ck_lexicon_import_runs_status CHECK (status IN ('running', 'completed', 'failed'))
);

CREATE INDEX IF NOT EXISTS idx_lexicon_import_runs_started_at
    ON lexicon.import_runs (started_at DESC);

CREATE INDEX IF NOT EXISTS idx_lexicon_import_runs_lookup
    ON lexicon.import_runs (environment, language_code, sha256);

CREATE UNIQUE INDEX IF NOT EXISTS ux_lexicon_import_runs_completed_fingerprint
    ON lexicon.import_runs (environment, language_code, sha256, options)
    WHERE status = 'completed';

-- ============================================================================
-- Vue matérialisée : mots valides (sans abréviations ni noms propres)
-- Règle Scrabble officiel (ODS) : pas de noms propres, abréviations, sigles,
-- acronymes, ni initiales.
--
-- Logique : un mot est EXCLU si TOUTES ses définitions ont un POS en liste noire.
-- Un mot qui a au moins une définition « normale » (nom, verbe, adj…) est gardé.
-- Ex : « angers » (verbe) est valide même si « Angers » (ville) est aussi présent.
--
-- Liste noire POS (valeurs kaiki/wiktextract) :
--   name, proper_noun, prop, proper  → noms propres (lieux, personnes)
--   abbrev                           → abréviations
--   acronym, initialism              → sigles et acronymes
--
-- La MV n'est recréée que si la définition a changé (version 2).
-- Démarrage rapide en prod après la première migration.
-- ============================================================================

-- Index pour accélérer le filtrage par POS
CREATE INDEX IF NOT EXISTS idx_lexicon_definitions_pos
    ON lexicon.definitions (part_of_speech);

DO $$
DECLARE
    mv_def text;
BEGIN
    -- Récupère la définition actuelle de la MV (si elle existe)
    SELECT definition INTO mv_def
    FROM pg_matviews
    WHERE schemaname = 'lexicon' AND matviewname = 'mv_valid_words';

    -- Si la MV existe déjà avec le filtre v2 (proper_noun + acronym + initialism), on ne fait rien
    IF mv_def IS NOT NULL
       AND mv_def LIKE '%proper_noun%'
       AND mv_def LIKE '%acronym%'
       AND mv_def LIKE '%initialism%' THEN
        RETURN;
    END IF;

    -- Sinon : recréer la MV avec le filtre à jour
    -- Approche UNION (beaucoup plus rapide que des sous-requêtes corrélées) :
    --   1. Mots sans aucune définition (inclus par défaut)
    --   2. Mots ayant au moins une définition non blacklistée
    DROP MATERIALIZED VIEW IF EXISTS lexicon.mv_valid_words;

    EXECUTE $q$
        CREATE MATERIALIZED VIEW lexicon.mv_valid_words AS
        -- Mots sans définitions
        SELECT w.word_id
        FROM lexicon.words w
        WHERE NOT EXISTS (
            SELECT 1 FROM lexicon.definitions d WHERE d.word_id = w.word_id
        )
        UNION
        -- Mots ayant au moins une définition « normale »
        SELECT DISTINCT d.word_id
        FROM lexicon.definitions d
        WHERE d.part_of_speech IS NULL
           OR d.part_of_speech NOT IN (
               'abbrev', 'name', 'prop', 'proper', 'proper_noun',
               'acronym', 'initialism'
           )
    $q$;

    EXECUTE $q$
        CREATE UNIQUE INDEX ux_mv_valid_words_id
        ON lexicon.mv_valid_words (word_id)
    $q$;
END $$;
