-- Development Seed Data
-- This file contains seed data for development and testing purposes

BEGIN;

-- Insert test tenant
INSERT INTO tenants (id, name, domain, plan_type, is_active, settings)
VALUES 
    ('d0000000-0000-0000-0000-000000000001', 'GolfClub Demo', 'demo.golfapp.com', 'premium', TRUE, '{"max_tournaments": 100, "max_players": 1000, "features": {"leaderboard": true, "realtime_scoring": true, "statistics": true}}');

-- Note: In a real application, user accounts would be created via auth.users
-- For development, we'll assume these users exist in auth.users already
-- and we're just linking them to our applications tables

-- Insert test profiles
INSERT INTO profiles (id, display_name, first_name, last_name, email, phone, handicap)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'Admin User', 'Admin', 'User', 'admin@example.com', '+1234567890', NULL),
    ('10000000-0000-0000-0000-000000000002', 'Organizer User', 'Organizer', 'User', 'organizer@example.com', '+1234567891', 5.4),
    ('10000000-0000-0000-0000-000000000003', 'Player One', 'Player', 'One', 'player1@example.com', '+1234567892', 12.8),
    ('10000000-0000-0000-0000-000000000004', 'Player Two', 'Player', 'Two', 'player2@example.com', '+1234567893', 18.2),
    ('10000000-0000-0000-0000-000000000005', 'Player Three', 'Player', 'Three', 'player3@example.com', '+1234567894', 7.5),
    ('10000000-0000-0000-0000-000000000006', 'Recorder User', 'Recorder', 'User', 'recorder@example.com', '+1234567895', NULL);

-- Link users to tenants with appropriate roles
INSERT INTO tenant_users (tenant_id, user_id, role)
VALUES
    ('d0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'admin'),
    ('d0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'organizer'),
    ('d0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', 'player'),
    ('d0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000004', 'player'),
    ('d0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000005', 'player'),
    ('d0000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000006', 'recorder');

-- Insert test course
INSERT INTO courses (id, tenant_id, name, location, city, state, country, total_holes, par, course_rating, slope_rating, is_public, created_by)
VALUES
    ('c0000000-0000-0000-0000-000000000001', 'd0000000-0000-0000-0000-000000000001', 'Pine Valley Golf Club', 'Pine Valley', 'Clementon', 'NJ', 'USA', 18, 72, 74.6, 155, TRUE, '10000000-0000-0000-0000-000000000001');

-- Insert tees
INSERT INTO tees (id, course_id, name, color, gender, par, course_rating, slope_rating)
VALUES
    ('e0000000-0000-0000-0000-000000000001', 'c0000000-0000-0000-0000-000000000001', 'Championship', 'Gold', 'male', 72, 74.6, 155),
    ('e0000000-0000-0000-0000-000000000002', 'c0000000-0000-0000-0000-000000000001', 'Men''s', 'Blue', 'male', 72, 72.8, 145),
    ('e0000000-0000-0000-0000-000000000003', 'c0000000-0000-0000-0000-000000000001', 'Senior', 'White', 'male', 72, 70.2, 138),
    ('e0000000-0000-0000-0000-000000000004', 'c0000000-0000-0000-0000-000000000001', 'Ladies', 'Red', 'female', 72, 71.5, 130);

-- Insert holes for Championship tees
INSERT INTO holes (tee_id, hole_number, par, stroke_index, distance_yards)
VALUES
    ('e0000000-0000-0000-0000-000000000001', 1, 4, 7, 428),
    ('e0000000-0000-0000-0000-000000000001', 2, 4, 3, 395),
    ('e0000000-0000-0000-0000-000000000001', 3, 3, 13, 200),
    ('e0000000-0000-0000-0000-000000000001', 4, 5, 1, 615),
    ('e0000000-0000-0000-0000-000000000001', 5, 3, 15, 235),
    ('e0000000-0000-0000-0000-000000000001', 6, 4, 5, 415),
    ('e0000000-0000-0000-0000-000000000001', 7, 4, 9, 373),
    ('e0000000-0000-0000-0000-000000000001', 8, 5, 11, 577),
    ('e0000000-0000-0000-0000-000000000001', 9, 4, 17, 435),
    ('e0000000-0000-0000-0000-000000000001', 10, 4, 8, 405),
    ('e0000000-0000-0000-0000-000000000001', 11, 4, 2, 387),
    ('e0000000-0000-0000-0000-000000000001', 12, 3, 16, 235),
    ('e0000000-0000-0000-0000-000000000001', 13, 5, 10, 573),
    ('e0000000-0000-0000-0000-000000000001', 14, 4, 6, 425),
    ('e0000000-0000-0000-0000-000000000001', 15, 3, 14, 175),
    ('e0000000-0000-0000-0000-000000000001', 16, 5, 4, 590),
    ('e0000000-0000-0000-0000-000000000001', 17, 4, 12, 355),
    ('e0000000-0000-0000-0000-000000000001', 18, 4, 18, 440);

-- Insert test tournament
INSERT INTO tournaments (
    id, tenant_id, name, description, course_id, 
    start_date, end_date, registration_deadline,
    status, format, is_public, max_players,
    organizer_name, organizer_email, created_by
)
VALUES (
    't0000000-0000-0000-0000-000000000001', 
    'd0000000-0000-0000-0000-000000000001',
    'Summer Championship 2025',
    'Annual club championship with various flights',
    'c0000000-0000-0000-0000-000000000001',
    '2025-06-15', '2025-06-16', '2025-06-10',
    'registration', 'stroke', TRUE, 72,
    'Tournament Committee', 'tournaments@golfapp.com',
    '10000000-0000-0000-0000-000000000002'
);

-- Insert tournament flights
INSERT INTO flights (id, tournament_id, name)
VALUES
    ('f0000000-0000-0000-0000-000000000001', 't0000000-0000-0000-0000-000000000001', 'Championship Flight'),
    ('f0000000-0000-0000-0000-000000000002', 't0000000-0000-0000-0000-000000000001', 'A Flight');

-- Insert tournament players
INSERT INTO tournament_players (
    id, tournament_id, player_id, flight_id,
    player_name, email, handicap, tee_id, status
)
VALUES
    (
        'p0000000-0000-0000-0000-000000000001',
        't0000000-0000-0000-0000-000000000001',
        '10000000-0000-0000-0000-000000000003',
        'f0000000-0000-0000-0000-000000000001',
        'Player One', 'player1@example.com', 12.8,
        'e0000000-0000-0000-0000-000000000001', 'confirmed'
    ),
    (
        'p0000000-0000-0000-0000-000000000002',
        't0000000-0000-0000-0000-000000000001',
        '10000000-0000-0000-0000-000000000004',
        'f0000000-0000-0000-0000-000000000002',
        'Player Two', 'player2@example.com', 18.2,
        'e0000000-0000-0000-0000-000000000002', 'confirmed'
    ),
    (
        'p0000000-0000-0000-0000-000000000003',
        't0000000-0000-0000-0000-000000000001',
        '10000000-0000-0000-0000-000000000005',
        'f0000000-0000-0000-0000-000000000001',
        'Player Three', 'player3@example.com', 7.5,
        'e0000000-0000-0000-0000-000000000001', 'confirmed'
    );

-- Insert tee times
INSERT INTO tee_times (
    id, tournament_id, tee_time, tee_box, flight_id
)
VALUES
    (
        'tt000000-0000-0000-0000-000000000001',
        't0000000-0000-0000-0000-000000000001',
        '2025-06-15 08:00:00+00',
        '1',
        'f0000000-0000-0000-0000-000000000001'
    ),
    (
        'tt000000-0000-0000-0000-000000000002',
        't0000000-0000-0000-0000-000000000001',
        '2025-06-15 08:10:00+00',
        '1',
        'f0000000-0000-0000-0000-000000000002'
    );

-- Assign players to tee times
INSERT INTO tee_time_players (
    tee_time_id, tournament_player_id
)
VALUES
    (
        'tt000000-0000-0000-0000-000000000001',
        'p0000000-0000-0000-0000-000000000001'
    ),
    (
        'tt000000-0000-0000-0000-000000000001',
        'p0000000-0000-0000-0000-000000000003'
    ),
    (
        'tt000000-0000-0000-0000-000000000002',
        'p0000000-0000-0000-0000-000000000002'
    );

COMMIT;
