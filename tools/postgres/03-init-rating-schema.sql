-- ============================================================================
-- LAMA Server - PostgreSQL Initialization Scripts
-- Schema: rating (classements ELO - mises à jour)
-- ============================================================================
-- Date: 2026-06-18
-- Purpose: Initialize rating schema for ELO ratings and leaderboards
-- ============================================================================

-- Create schema if not exists
CREATE SCHEMA IF NOT EXISTS rating;

-- Grant permissions
GRANT USAGE ON SCHEMA rating TO lama_dev;
ALTER DEFAULT PRIVILEGES IN SCHEMA rating GRANT ALL ON TABLES TO lama_dev;

-- ============================================================================
-- TABLE: rating.players
-- Description: Référentiel central des joueurs
-- ============================================================================

CREATE TABLE IF NOT EXISTS rating.players (
    player_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(100) UNIQUE NOT NULL,
    email VARCHAR(256) UNIQUE,
    password_hash VARCHAR(512),
    country_code CHAR(2),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_username_length CHECK (LENGTH(username) BETWEEN 1 AND 100)
);

CREATE INDEX idx_rating_players_username ON rating.players(username);

-- ============================================================================
-- TABLE: rating.player_ratings
-- Description: Notation ELO par joueur et par queue
-- ============================================================================

CREATE TABLE IF NOT EXISTS rating.player_ratings (
    rating_record_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES rating.players(player_id) ON DELETE CASCADE,
    queue VARCHAR(50) NOT NULL,  -- open, tournament, global
    elo_rating NUMERIC(8, 2) NOT NULL DEFAULT 1400.0,
    games_played INT NOT NULL DEFAULT 0,
    games_won INT NOT NULL DEFAULT 0,
    games_lost INT NOT NULL DEFAULT 0,
    games_abandoned INT NOT NULL DEFAULT 0,
    total_points INT NOT NULL DEFAULT 0,
    avg_score NUMERIC(8, 2),
    last_game_date TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_queue CHECK (queue IN ('open', 'tournament', 'global')),
    CONSTRAINT check_elo CHECK (elo_rating >= 0 AND elo_rating <= 3000),
    CONSTRAINT check_games_played CHECK (games_played >= 0),
    CONSTRAINT check_games_won CHECK (games_won >= 0 AND games_won <= games_played),
    CONSTRAINT check_games_lost CHECK (games_lost >= 0 AND games_lost <= games_played),
    CONSTRAINT check_games_abandoned CHECK (games_abandoned >= 0),
    CONSTRAINT check_total_points CHECK (total_points >= 0),
    UNIQUE(player_id, queue)
);

CREATE INDEX idx_rating_player_ratings_player_id ON rating.player_ratings(player_id);
CREATE INDEX idx_rating_player_ratings_queue ON rating.player_ratings(queue);
CREATE INDEX idx_rating_player_ratings_elo ON rating.player_ratings(queue, elo_rating DESC);
CREATE INDEX idx_rating_player_ratings_updated_at ON rating.player_ratings(updated_at);

-- ============================================================================
-- TABLE: rating.leaderboard_snapshot
-- Description: Snapshots historiques des classements (pour audit/comparaison)
-- ============================================================================

CREATE TABLE IF NOT EXISTS rating.leaderboard_snapshot (
    snapshot_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    queue VARCHAR(50) NOT NULL,
    snapshot_date TIMESTAMPTZ NOT NULL,
    leaderboard_json JSONB NOT NULL,  -- Top 100 : [{rank, player_id, username, elo_rating, games_played}, ...]
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_snapshot_queue CHECK (queue IN ('open', 'tournament', 'global'))
);

CREATE INDEX idx_rating_leaderboard_snapshot_queue ON rating.leaderboard_snapshot(queue);
CREATE INDEX idx_rating_leaderboard_snapshot_snapshot_date ON rating.leaderboard_snapshot(snapshot_date);
CREATE INDEX idx_rating_leaderboard_snapshot_created_at ON rating.leaderboard_snapshot(created_at);

-- ============================================================================
-- TABLE: rating.player_statistics
-- Description: Statistiques globales par joueur (agrégées)
-- ============================================================================

CREATE TABLE IF NOT EXISTS rating.player_statistics (
    stats_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID UNIQUE NOT NULL REFERENCES rating.players(player_id) ON DELETE CASCADE,
    total_games_all_time INT NOT NULL DEFAULT 0,
    total_points_all_time INT NOT NULL DEFAULT 0,
    total_wins_all_time INT NOT NULL DEFAULT 0,
    longest_winning_streak INT NOT NULL DEFAULT 0,
    current_winning_streak INT NOT NULL DEFAULT 0,
    avg_score_all_time NUMERIC(8, 2),
    favorite_game_level VARCHAR(50),
    first_game_date TIMESTAMPTZ,
    last_game_date TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_total_games CHECK (total_games_all_time >= 0),
    CONSTRAINT check_total_points CHECK (total_points_all_time >= 0),
    CONSTRAINT check_total_wins CHECK (total_wins_all_time >= 0 AND total_wins_all_time <= total_games_all_time),
    CONSTRAINT check_winning_streak CHECK (longest_winning_streak >= 0),
    CONSTRAINT check_current_streak CHECK (current_winning_streak >= 0)
);

CREATE INDEX idx_rating_player_statistics_total_games ON rating.player_statistics(total_games_all_time DESC);
CREATE INDEX idx_rating_player_statistics_total_wins ON rating.player_statistics(total_wins_all_time DESC);
CREATE INDEX idx_rating_player_statistics_last_game ON rating.player_statistics(last_game_date DESC);

-- ============================================================================
-- VIEW: rating.top_players_open
-- Description: Top 100 joueurs en queue "open"
-- ============================================================================

CREATE OR REPLACE VIEW rating.top_players_open AS
SELECT 
    ROW_NUMBER() OVER (ORDER BY pr.elo_rating DESC, pr.updated_at ASC) as rank,
    p.player_id,
    p.username,
    pr.elo_rating,
    pr.games_played,
    pr.games_won,
    pr.avg_score
FROM rating.players p
JOIN rating.player_ratings pr ON p.player_id = pr.player_id
WHERE pr.queue = 'open'
LIMIT 100;

GRANT SELECT ON rating.top_players_open TO lama_dev;

-- ============================================================================
-- VIEW: rating.top_players_tournament
-- Description: Top 100 joueurs en queue "tournament"
-- ============================================================================

CREATE OR REPLACE VIEW rating.top_players_tournament AS
SELECT 
    ROW_NUMBER() OVER (ORDER BY pr.elo_rating DESC, pr.updated_at ASC) as rank,
    p.player_id,
    p.username,
    pr.elo_rating,
    pr.games_played,
    pr.games_won,
    pr.avg_score
FROM rating.players p
JOIN rating.player_ratings pr ON p.player_id = pr.player_id
WHERE pr.queue = 'tournament'
LIMIT 100;

GRANT SELECT ON rating.top_players_tournament TO lama_dev;

-- ============================================================================
-- VIEW: rating.top_players_global
-- Description: Top 100 joueurs globalement (moyenne des queues pondérée)
-- ============================================================================

CREATE OR REPLACE VIEW rating.top_players_global AS
SELECT 
    ROW_NUMBER() OVER (ORDER BY avg_elo DESC, updated_at ASC) as rank,
    player_id,
    username,
    avg_elo as elo_rating,
    total_games_all_time as games_played
FROM (
    SELECT 
        p.player_id,
        p.username,
        AVG(pr.elo_rating) as avg_elo,
        SUM(pr.games_played) as total_games_all_time,
        MAX(pr.updated_at) as updated_at
    FROM rating.players p
    JOIN rating.player_ratings pr ON p.player_id = pr.player_id
    GROUP BY p.player_id, p.username
) ranked
LIMIT 100;

GRANT SELECT ON rating.top_players_global TO lama_dev;

-- ============================================================================
-- TABLE: rating.elo_adjustments_log
-- Description: Audit log de chaque ajustement ELO
-- ============================================================================

CREATE TABLE IF NOT EXISTS rating.elo_adjustments_log (
    adjustment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES rating.players(player_id) ON DELETE CASCADE,
    queue VARCHAR(50) NOT NULL,
    game_id UUID,  -- Reference to history.completed_games
    elo_before NUMERIC(8, 2) NOT NULL,
    elo_after NUMERIC(8, 2) NOT NULL,
    elo_delta NUMERIC(8, 2) NOT NULL,
    reason VARCHAR(255),  -- "win", "loss", "draw", "abandoned", "adjustment"
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_elo_adjustment_queue CHECK (queue IN ('open', 'tournament', 'global')),
    CONSTRAINT check_elo_before CHECK (elo_before >= 0),
    CONSTRAINT check_elo_after CHECK (elo_after >= 0)
);

CREATE INDEX idx_rating_elo_adjustments_player_id ON rating.elo_adjustments_log(player_id);
CREATE INDEX idx_rating_elo_adjustments_queue ON rating.elo_adjustments_log(queue);
CREATE INDEX idx_rating_elo_adjustments_created_at ON rating.elo_adjustments_log(created_at);
CREATE INDEX idx_rating_elo_adjustments_game_id ON rating.elo_adjustments_log(game_id) WHERE game_id IS NOT NULL;

-- ============================================================================
-- STORED PROCEDURE: rating.update_elo
-- Description: Met à jour l'ELO d'un joueur après une partie
-- ============================================================================

CREATE OR REPLACE FUNCTION rating.update_elo(
    p_player_id UUID,
    p_queue VARCHAR(50),
    p_elo_delta NUMERIC,
    p_reason VARCHAR(255),
    p_game_id UUID DEFAULT NULL
) RETURNS TABLE (
    new_elo NUMERIC,
    adjustment_id UUID
) AS $$
DECLARE
    v_current_elo NUMERIC(8, 2);
    v_new_elo NUMERIC(8, 2);
    v_adjustment_id UUID;
BEGIN
    -- Get current ELO or create default
    SELECT elo_rating INTO v_current_elo
    FROM rating.player_ratings
    WHERE player_id = p_player_id AND queue = p_queue;
    
    IF v_current_elo IS NULL THEN
        v_current_elo := 1400.0;
    END IF;
    
    -- Calculate new ELO
    v_new_elo := GREATEST(0, LEAST(3000, v_current_elo + p_elo_delta));
    
    -- Log adjustment
    v_adjustment_id := gen_random_uuid();
    INSERT INTO rating.elo_adjustments_log (
        adjustment_id, player_id, queue, game_id, elo_before, elo_after, elo_delta, reason
    ) VALUES (v_adjustment_id, p_player_id, p_queue, p_game_id, v_current_elo, v_new_elo, p_elo_delta, p_reason);
    
    -- Update or insert rating
    INSERT INTO rating.player_ratings (player_id, queue, elo_rating)
    VALUES (p_player_id, p_queue, v_new_elo)
    ON CONFLICT (player_id, queue) DO UPDATE
    SET elo_rating = v_new_elo,
        updated_at = CURRENT_TIMESTAMP;
    
    RETURN QUERY SELECT v_new_elo, v_adjustment_id;
END;
$$ LANGUAGE plpgsql;

GRANT EXECUTE ON FUNCTION rating.update_elo TO lama_dev;

-- ============================================================================
-- Permissions finales
-- ============================================================================

GRANT USAGE ON SCHEMA rating TO lama_dev;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA rating TO lama_dev;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA rating TO lama_dev;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA rating TO lama_dev;

-- ============================================================================
-- Logs/Traceability
-- ============================================================================

COMMENT ON SCHEMA rating IS 'Notation ELO et classements - Rating and leaderboards';
COMMENT ON TABLE rating.players IS 'Référentiel central des joueurs LAMA';
COMMENT ON TABLE rating.player_ratings IS 'Notation ELO courante par joueur et queue';
COMMENT ON TABLE rating.leaderboard_snapshot IS 'Snapshots historiques des classements';
COMMENT ON TABLE rating.player_statistics IS 'Statistiques agrégées globales par joueur';
COMMENT ON TABLE rating.elo_adjustments_log IS 'Audit log de chaque ajustement ELO';

