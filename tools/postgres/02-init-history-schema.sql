-- ============================================================================
-- LAMA Server - PostgreSQL Initialization Scripts
-- Schema: history (parties terminées - immuable/analytique)
-- ============================================================================
-- Date: 2026-06-18
-- Purpose: Initialize history schema with archive tables for completed games
-- ============================================================================

-- Create schema if not exists
CREATE SCHEMA IF NOT EXISTS history;

-- Grant permissions
GRANT USAGE ON SCHEMA history TO lama_dev;
ALTER DEFAULT PRIVILEGES IN SCHEMA history GRANT ALL ON TABLES TO lama_dev;

-- ============================================================================
-- TABLE: history.tournaments
-- Description: Définition des tournois
-- ============================================================================

CREATE TABLE IF NOT EXISTS history.tournaments (
    tournament_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    start_date TIMESTAMPTZ NOT NULL,
    end_date TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL DEFAULT 'created',  -- created, active, finished
    created_by_player_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT check_tournament_status CHECK (status IN ('created', 'active', 'finished'))
);

CREATE INDEX idx_history_tournaments_status ON history.tournaments(status);
CREATE INDEX idx_history_tournaments_created_by ON history.tournaments(created_by_player_id);
CREATE INDEX idx_history_tournaments_start_date ON history.tournaments(start_date);

-- ============================================================================
-- TABLE: history.completed_games
-- Description: Parties terminées (snapshot immuable)
-- ============================================================================

CREATE TABLE IF NOT EXISTS history.completed_games (
    game_id UUID PRIMARY KEY,  -- Même ID que sessions.games
    game_level VARCHAR(50) NOT NULL,
    board_size INT NOT NULL,
    rack_size INT NOT NULL,
    min_word_length INT NOT NULL,
    language VARCHAR(10) NOT NULL,
    queue VARCHAR(50) NOT NULL,
    tournament_id UUID REFERENCES history.tournaments(tournament_id),
    status VARCHAR(50) NOT NULL,  -- finished_normal, finished_by_player, abandoned
    created_at TIMESTAMPTZ NOT NULL,
    ended_at TIMESTAMPTZ NOT NULL,
    duration_seconds INT NOT NULL,
    winning_player_id UUID,  -- Peut être NULL en cas d'abandon/égalité
    
    CONSTRAINT check_completed_game_status CHECK (status IN ('finished_normal', 'finished_by_player', 'abandoned')),
    CONSTRAINT check_duration CHECK (duration_seconds > 0)
);

CREATE INDEX idx_history_completed_games_tournament_id ON history.completed_games(tournament_id);
CREATE INDEX idx_history_completed_games_queue ON history.completed_games(queue);
CREATE INDEX idx_history_completed_games_ended_at ON history.completed_games(ended_at);
CREATE INDEX idx_history_completed_games_winning_player ON history.completed_games(winning_player_id);

-- ============================================================================
-- TABLE: history.game_participants
-- Description: Participants et leurs résultats dans chaque partie
-- ============================================================================

CREATE TABLE IF NOT EXISTS history.game_participants (
    participant_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID NOT NULL REFERENCES history.completed_games(game_id) ON DELETE CASCADE,
    player_id UUID NOT NULL,  -- FK to rating.players
    nickname VARCHAR(100) NOT NULL,
    final_score INT NOT NULL,
    rank INT NOT NULL,  -- 1 = winner, 2 = second, etc.
    was_host BOOLEAN NOT NULL DEFAULT FALSE,
    
    CONSTRAINT check_score CHECK (final_score >= 0),
    CONSTRAINT check_rank CHECK (rank > 0),
    UNIQUE(game_id, rank)
);

CREATE INDEX idx_history_game_participants_game_id ON history.game_participants(game_id);
CREATE INDEX idx_history_game_participants_player_id ON history.game_participants(player_id);
CREATE INDEX idx_history_game_participants_rank ON history.game_participants(game_id, rank);

-- ============================================================================
-- TABLE: history.moves_log
-- Description: Tous les coups joués dans toutes les parties (immuable)
-- ============================================================================

CREATE TABLE IF NOT EXISTS history.moves_log (
    move_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID NOT NULL REFERENCES history.completed_games(game_id) ON DELETE CASCADE,
    player_id UUID NOT NULL,  -- FK to rating.players
    move_number INT NOT NULL,  -- Numéro du coup (1, 2, 3, ...)
    action_type VARCHAR(50) NOT NULL,  -- move, pass, swap, challenge
    action_payload JSONB NOT NULL,  -- Position, mot, direction, etc.
    board_after JSONB,  -- État plateau après coup (optionnel, peut être lourd)
    scores_after JSONB NOT NULL,  -- Scores des joueurs après coup
    executed_at TIMESTAMPTZ NOT NULL,
    
    CONSTRAINT check_move_action_type CHECK (action_type IN ('move', 'pass', 'swap', 'challenge')),
    CONSTRAINT check_move_number CHECK (move_number > 0)
);

CREATE INDEX idx_history_moves_log_game_id ON history.moves_log(game_id);
CREATE INDEX idx_history_moves_log_player_id ON history.moves_log(player_id);
CREATE INDEX idx_history_moves_log_move_number ON history.moves_log(game_id, move_number);
CREATE INDEX idx_history_moves_log_executed_at ON history.moves_log(executed_at);

-- ============================================================================
-- VIEW: history.game_statistics
-- Description: Statistiques agrégées par partie
-- ============================================================================

CREATE OR REPLACE VIEW history.game_statistics AS
SELECT 
    cg.game_id,
    cg.game_level,
    cg.queue,
    COUNT(DISTINCT gp.player_id) as player_count,
    MAX(ml.move_number) as total_moves,
    AVG(gp.final_score::NUMERIC) as avg_score,
    MAX(gp.final_score) as max_score,
    cg.duration_seconds,
    cg.created_at,
    cg.ended_at
FROM history.completed_games cg
LEFT JOIN history.game_participants gp ON cg.game_id = gp.game_id
LEFT JOIN history.moves_log ml ON cg.game_id = ml.game_id
GROUP BY cg.game_id, cg.game_level, cg.queue, cg.duration_seconds, cg.created_at, cg.ended_at;

GRANT SELECT ON history.game_statistics TO lama_dev;

-- ============================================================================
-- VIEW: history.player_game_history
-- Description: Historique des parties pour un joueur
-- ============================================================================

CREATE OR REPLACE VIEW history.player_game_history AS
SELECT 
    gp.player_id,
    cg.game_id,
    cg.game_level,
    cg.queue,
    gp.final_score,
    gp.rank,
    CASE WHEN gp.rank = 1 THEN TRUE ELSE FALSE END as is_winner,
    gp.was_host,
    cg.created_at,
    cg.ended_at,
    ML.total_moves
FROM history.game_participants gp
JOIN history.completed_games cg ON gp.game_id = cg.game_id
LEFT JOIN (
    SELECT game_id, MAX(move_number) as total_moves
    FROM history.moves_log
    GROUP BY game_id
) ML ON cg.game_id = ML.game_id
ORDER BY cg.ended_at DESC;

GRANT SELECT ON history.player_game_history TO lama_dev;

-- ============================================================================
-- Permissions finales
-- ============================================================================

GRANT USAGE ON SCHEMA history TO lama_dev;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA history TO lama_dev;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA history TO lama_dev;

-- ============================================================================
-- Logs/Traceability
-- ============================================================================

COMMENT ON SCHEMA history IS 'Archive immuable des parties terminées - Audit and analytics';
COMMENT ON TABLE history.tournaments IS 'Métadonnées tournois';
COMMENT ON TABLE history.completed_games IS 'Snapshot immuable des parties jouées';
COMMENT ON TABLE history.game_participants IS 'Participants et résultats (classement, scores)';
COMMENT ON TABLE history.moves_log IS 'Journal complet et immuable de tous les coups';

