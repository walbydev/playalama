-- ============================================================================
-- LAMA Server - EF Core baseline for SQL-first dev strategy
-- ============================================================================
-- Purpose:
--   Mark initial EF migration as already applied when schemas/tables are
--   created by docker-entrypoint SQL scripts.
--
-- Strategy selected:
--   DEV uses SQL auto-init as source of truth.
--   EF must not try to recreate existing tables on first run.
-- ============================================================================

CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- Baseline initial migration generated in this repository.
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260618165737_InitialThreeSchemas', '10.0.4')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Migration 2: email + password_hash (colonnes déjà présentes dans le SQL init)
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260619222532_AddPlayerEmailPassword', '10.0.4')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Migration 3: country_code (colonne déjà présente dans le SQL init)
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260625103636_AddPlayerCountryCode', '10.0.4')
ON CONFLICT ("MigrationId") DO NOTHING;

