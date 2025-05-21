-- Migration Name: 20250520000000_leaderboard_views
-- Description: Create views for tournament leaderboards
-- Created By: GitHub Copilot

-- Enable necessary extensions
BEGIN;

-- Stroke play leaderboard view
CREATE OR REPLACE VIEW stroke_leaderboard AS
SELECT
    t.id AS tournament_id,
    t.tenant_id,
    t.name AS tournament_name,
    tp.id AS player_id,
    tp.player_name,
    tp.handicap,
    COUNT(s.id) AS holes_played,
    SUM(s.strokes) AS total_strokes,
    SUM(s.strokes) - SUM(h.par) AS score_to_par,
    ROW_NUMBER() OVER (
        PARTITION BY t.id 
        ORDER BY SUM(s.strokes)
    ) AS position
FROM tournaments t
JOIN tournament_players tp ON t.id = tp.tournament_id
LEFT JOIN scores s ON tp.id = s.player_id
LEFT JOIN holes h ON tp.tee_id = h.tee_id AND s.hole_number = h.hole_number
WHERE t.format = 'stroke'
GROUP BY t.id, t.tenant_id, t.name, tp.id, tp.player_name, tp.handicap
ORDER BY t.id, total_strokes;

-- Stableford leaderboard view
CREATE OR REPLACE VIEW stableford_leaderboard AS
SELECT
    t.id AS tournament_id,
    t.tenant_id,
    t.name AS tournament_name,
    tp.id AS player_id,
    tp.player_name,
    tp.handicap,
    COUNT(s.id) AS holes_played,
    SUM(
        CASE
            WHEN s.strokes >= h.par + 2 THEN 0
            WHEN s.strokes = h.par + 1 THEN 1
            WHEN s.strokes = h.par THEN 2
            WHEN s.strokes = h.par - 1 THEN 3
            WHEN s.strokes = h.par - 2 THEN 4
            WHEN s.strokes <= h.par - 3 THEN 5
            ELSE 0
        END
    ) AS points,
    ROW_NUMBER() OVER (
        PARTITION BY t.id 
        ORDER BY SUM(
            CASE
                WHEN s.strokes >= h.par + 2 THEN 0
                WHEN s.strokes = h.par + 1 THEN 1
                WHEN s.strokes = h.par THEN 2
                WHEN s.strokes = h.par - 1 THEN 3
                WHEN s.strokes = h.par - 2 THEN 4
                WHEN s.strokes <= h.par - 3 THEN 5
                ELSE 0
            END
        ) DESC
    ) AS position
FROM tournaments t
JOIN tournament_players tp ON t.id = tp.tournament_id
LEFT JOIN scores s ON tp.id = s.player_id
LEFT JOIN holes h ON tp.tee_id = h.tee_id AND s.hole_number = h.hole_number
WHERE t.format = 'stableford'
GROUP BY t.id, t.tenant_id, t.name, tp.id, tp.player_name, tp.handicap
ORDER BY t.id, points DESC;

-- Match play leaderboard view (more complex - shows match results)
CREATE OR REPLACE VIEW match_leaderboard AS
WITH match_results AS (
    SELECT 
        t.id AS tournament_id,
        t.name AS tournament_name,
        tp1.id AS player1_id,
        tp1.player_name AS player1_name,
        tp2.id AS player2_id,
        tp2.player_name AS player2_name,
        -- Count holes won by each player
        SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) AS player1_holes_won,
        SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) AS player2_holes_won,
        -- Count holes halved
        SUM(CASE WHEN s1.strokes = s2.strokes THEN 1 ELSE 0 END) AS holes_halved,
        -- Count total holes played
        COUNT(s1.hole_number) AS holes_played,
        -- Calculate current match status
        CASE 
            WHEN SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) > 
                 SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) 
            THEN player1_name || ' leads by ' || 
                 (SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) - 
                  SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END)) || ' hole(s)'
            WHEN SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) > 
                 SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) 
            THEN player2_name || ' leads by ' || 
                 (SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) - 
                  SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END)) || ' hole(s)'
            ELSE 'All square'
        END AS match_status,
        -- Determine if match is finished
        CASE 
            WHEN SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) - 
                 SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) > 
                 (18 - COUNT(s1.hole_number))
            THEN player1_name || ' wins ' || 
                 (SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) - 
                  SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END)) || '&' || 
                 (18 - COUNT(s1.hole_number))
            WHEN SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) - 
                 SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) > 
                 (18 - COUNT(s1.hole_number))
            THEN player2_name || ' wins ' || 
                 (SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END) - 
                  SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END)) || '&' || 
                 (18 - COUNT(s1.hole_number))
            WHEN COUNT(s1.hole_number) = 18 AND 
                 SUM(CASE WHEN s1.strokes < s2.strokes THEN 1 ELSE 0 END) = 
                 SUM(CASE WHEN s2.strokes < s1.strokes THEN 1 ELSE 0 END)
            THEN 'Match halved'
            ELSE 'In progress'
        END AS result
    FROM tournaments t
    JOIN tournament_players tp1 ON t.id = tp1.tournament_id
    JOIN tournament_players tp2 ON t.id = tp2.tournament_id
    JOIN flights f ON tp1.flight_id = f.id AND tp2.flight_id = f.id AND tp1.id < tp2.id
    LEFT JOIN scores s1 ON tp1.id = s1.player_id
    LEFT JOIN scores s2 ON tp2.id = s2.player_id AND s1.hole_number = s2.hole_number
    WHERE t.format = 'match'
    GROUP BY t.id, t.name, tp1.id, tp1.player_name, tp2.id, tp2.player_name
)
SELECT 
    tournament_id,
    tournament_name,
    player1_id,
    player1_name,
    player2_id,
    player2_name,
    player1_holes_won,
    player2_holes_won,
    holes_halved,
    holes_played,
    match_status,
    result,
    ROW_NUMBER() OVER (PARTITION BY tournament_id ORDER BY holes_played DESC) AS match_number
FROM match_results;

-- Tournament statistics view for analytics
CREATE OR REPLACE VIEW tournament_statistics AS
SELECT
    t.id AS tournament_id,
    t.tenant_id,
    t.name AS tournament_name,
    COUNT(DISTINCT tp.id) AS player_count,
    COUNT(DISTINCT f.id) AS flight_count,
    AVG(s.strokes) AS avg_strokes_per_hole,
    COUNT(s.id) AS total_scores_recorded,
    MIN(s.recorded_at) AS first_score_time,
    MAX(s.recorded_at) AS last_score_time,
    t.start_date,
    t.end_date,
    t.status
FROM tournaments t
LEFT JOIN tournament_players tp ON t.id = tp.tournament_id
LEFT JOIN flights f ON t.id = f.tournament_id
LEFT JOIN scores s ON tp.id = s.player_id
GROUP BY t.id, t.tenant_id, t.name, t.start_date, t.end_date, t.status;

-- Apply RLS policies to the views
ALTER VIEW stroke_leaderboard OWNER TO authenticated;
ALTER VIEW stableford_leaderboard OWNER TO authenticated;
ALTER VIEW match_leaderboard OWNER TO authenticated;
ALTER VIEW tournament_statistics OWNER TO authenticated;

COMMIT;
