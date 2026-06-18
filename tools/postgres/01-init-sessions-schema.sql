-- ============================================================================
-- LAMA Server - PostgreSQL Initialization Scripts
-- Schema: sessions (parties en cours - volatile)
-- ============================================================================
-- Date: 2026-06-18
-- Purpose: Initialize sessions schema with tables for active games
-- ============================================================================

-- Create schema if not exists
CREATE SCHEMA IF NOT EXISTS sessions;

-- Grant permissions
GRANT USAGE ON SCHEMA sessions TO lama_dev;
ALTER DEFAULT PRIVILEGES IN SCHEMA sessions GRANT ALL ON TABLES TO lama_dev;

-- ============================================================================
-- TABLE: sessions.games
-- Description: Parties en cours (en mémoire + PG)
-- ============================================================================

CREATE TABLE IF NOT EXISTS sessions.games (
    game_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_level VARCHAR(50) NOT NULL DEFAULT 'Standard',  -- Standard, Competitive, Tournament, Casual
    board_size INT NOT NULL DEFAULT 15,
    rack_size INT NOT NULL DEFAULT 7,
    min_word_length INT NOT NULL DEFAULT 2,
    language VARCHAR(10) NOT NULL DEFAULT 'fr',
    queue VARCHAR(50) NOT NULL DEFAULT 'open',  -- open, tournament, global
    host_player_id UUID NOT NULL,  -- FK to sessions.players_in_game (resolved after insert)
    tournament_id UUID,  -- nullable
    status VARCHAR(50) NOT NULL DEFAULT 'created',  -- created, active, paused, ended, abandoned
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at TIMESTAMPTZ,
    
    CONSTRAINT check_game_level CHECK (game_level IN ('Standard', 'Competitive', 'Tournament', 'Casual')),
    CONSTRAINT check_status CHECK (status IN ('created', 'active', 'paused', 'ended', 'abandoned')),
    CONSTRAINT check_queue CHECK (queue IN ('open', 'tournament', 'global')),
    CONSTRAINT check_board_size CHECK (board_size BETWEEN 15 AND 26),
    CONSTRAINT check_rack_size CHECK (rack_size BETWEEN 5 AND 10),
    CONSTRAINT check_min_word_length CHECK (min_word_length BETWEEN 1 AND 3)
);

CREATE INDEX idx_sessions_games_status ON sessions.games(status);
CREATE INDEX idx_sessions_games_updated_at ON sessions.games(updated_at);
CREATE INDEX idx_sessions_games_queue ON sessions.games(queue);
CREATE INDEX idx_sessions_games_created_at ON sessions.games(created_at);

-- ============================================================================
-- TABLE: sessions.players_in_game
-- Description: Joueurs dans chaque partie
-- ============================================================================

CREATE TABLE IF NOT EXISTS sessions.players_in_game (
    player_session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID NOT NULL REFERENCES sessions.games(game_id) ON DELETE CASCADE,
    player_id UUID NOT NULL,  -- FK to rating.players (cross-schema)
    nickname VARCHAR(100) NOT NULL,
    is_host BOOLEAN NOT NULL DEFAULT FALSE,
    player_index INT NOT NULL,  -- 0, 1, 2, 3 (ordre des tours)
    joined_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_player_index CHECK (player_index BETWEEN 0 AND 3),
    UNIQUE(game_id, player_id),
    UNIQUE(game_id, player_index)
);

CREATE INDEX idx_sessions_players_in_game_game_id ON sessions.players_in_game(game_id);
CREATE INDEX idx_sessions_players_in_game_player_id ON sessions.players_in_game(player_id);
CREATE INDEX idx_sessions_players_in_game_is_host ON sessions.players_in_game(is_host);

-- ============================================================================
-- TABLE: sessions.board_state
-- Description: État du plateau pour chaque partie
-- ============================================================================

CREATE TABLE IF NOT EXISTS sessions.board_state (
    game_id UUID PRIMARY KEY REFERENCES sessions.games(game_id) ON DELETE CASCADE,
    board_json JSONB NOT NULL,  -- Sérialisation complète BoardState
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_sessions_board_state_updated_at ON sessions.board_state(updated_at);

-- ============================================================================
-- TABLE: sessions.rack_state
-- Description: État des racks pour chaque joueur
-- ============================================================================

CREATE TABLE IF NOT EXISTS sessions.rack_state (
    rack_state_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID NOT NULL REFERENCES sessions.games(game_id) ON DELETE CASCADE,
    player_session_id UUID NOT NULL REFERENCES sessions.players_in_game(player_session_id) ON DELETE CASCADE,
    rack_json JSONB NOT NULL,  -- Sérialisation complète Rack
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    UNIQUE(game_id, player_session_id)
);

CREATE INDEX idx_sessions_rack_state_game_id ON sessions.rack_state(game_id);
CREATE INDEX idx_sessions_rack_state_player_session_id ON sessions.rack_state(player_session_id);

-- ============================================================================
-- TABLE: sessions.turn_log
-- Description: Journal des actions jouées dans chaque partie
-- ============================================================================

CREATE TABLE IF NOT EXISTS sessions.turn_log (
    turn_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID NOT NULL REFERENCES sessions.games(game_id) ON DELETE CASCADE,
    player_session_id UUID NOT NULL REFERENCES sessions.players_in_game(player_session_id) ON DELETE CASCADE,
    turn_number INT NOT NULL,  -- Numéro du coup (1, 2, 3, ...)
    action_type VARCHAR(50) NOT NULL,  -- move, pass, swap, challenge, check
    action_payload JSONB NOT NULL,  -- Détails du coup (position, mot, direction, etc.)
    executed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    result_status VARCHAR(50) NOT NULL DEFAULT 'success',  -- success, failed, rejected
    error_message TEXT,
    
    CONSTRAINT check_action_type CHECK (action_type IN ('move', 'pass', 'swap', 'challenge', 'check')),
    CONSTRAINT check_result_status CHECK (result_status IN ('success', 'failed', 'rejected')),
    CONSTRAINT check_turn_number CHECK (turn_number > 0)
);

CREATE INDEX idx_sessions_turn_log_game_id ON sessions.turn_log(game_id);
CREATE INDEX idx_sessions_turn_log_player_session_id ON sessions.turn_log(player_session_id);
CREATE INDEX idx_sessions_turn_log_turn_number ON sessions.turn_log(game_id, turn_number);
CREATE INDEX idx_sessions_turn_log_executed_at ON sessions.turn_log(executed_at);

-- ============================================================================
-- TRIGGER: Update sessions.games.updated_at on turn_log INSERT
-- ============================================================================

CREATE OR REPLACE FUNCTION sessions.update_game_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE sessions.games 
    SET updated_at = CURRENT_TIMESTAMP 
    WHERE game_id = NEW.game_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trx_sessions_game_updated_on_turn
AFTER INSERT ON sessions.turn_log
FOR EACH ROW
EXECUTE FUNCTION sessions.update_game_timestamp();

-- ============================================================================
-- VIEW: sessions.active_games_summary
-- Description: Vue d'ensemble des parties actives
-- ============================================================================

CREATE OR REPLACE VIEW sessions.active_games_summary AS
SELECT 
    g.game_id,
    g.game_level,
    g.board_size,
    g.status,
    g.queue,
    COUNT(pig.player_session_id) as player_count,
    MAX(tl.turn_number) as total_turns,
    g.created_at,
    g.updated_at
FROM sessions.games g
LEFT JOIN sessions.players_in_game pig ON g.game_id = pig.game_id
LEFT JOIN sessions.turn_log tl ON g.game_id = tl.game_id
WHERE g.status IN ('created', 'active', 'paused')
GROUP BY g.game_id, g.game_level, g.board_size, g.status, g.queue, g.created_at, g.updated_at;

GRANT SELECT ON sessions.active_games_summary TO lama_dev;

-- ============================================================================
-- Permissions finales
-- ============================================================================

GRANT USAGE ON SCHEMA sessions TO lama_dev;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA sessions TO lama_dev;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA sessions TO lama_dev;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA sessions TO lama_dev;

-- ============================================================================
-- Logs/Traceability
-- ============================================================================

COMMENT ON SCHEMA sessions IS 'Parties en cours (volatile) - Session tables for active games';
COMMENT ON TABLE sessions.games IS 'Parties en cours avec tous les métadonnées';
COMMENT ON TABLE sessions.players_in_game IS 'Jointure joueurs-parties avec ordre de jeu';
COMMENT ON TABLE sessions.board_state IS 'État courant du plateau (sérialié en JSON)';
COMMENT ON TABLE sessions.rack_state IS 'Racks courants des joueurs (sérialisés en JSON)';
COMMENT ON TABLE sessions.turn_log IS 'Historique des actions jouées dans chaque partie';

