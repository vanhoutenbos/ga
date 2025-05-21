-- Migration Name: 20250501000000_initial_schema
-- Description: Initial database schema for Golf Tournament Organizer
-- Created By: CI/CD Pipeline

-- Enable necessary extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Schema setup
BEGIN;

-- Tenants Table (Organizations/Golf Clubs)
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    domain VARCHAR(255),
    plan_type VARCHAR(50) NOT NULL DEFAULT 'free' CHECK (plan_type IN ('free', 'basic', 'premium', 'enterprise')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    settings JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add tenant-related RLS policies
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;

-- Profiles Table (User Profiles)
CREATE TABLE profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    display_name VARCHAR(255) NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    email VARCHAR(255),
    phone VARCHAR(50),
    handicap NUMERIC(4,1),
    profile_picture_url VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add profile-related RLS policies
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;

-- Tenant Users (Linking Tenants and Users with Roles)
CREATE TABLE tenant_users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL CHECK (role IN ('admin', 'organizer', 'player', 'recorder', 'spectator')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, user_id)
);

-- Add tenant_users-related RLS policies
ALTER TABLE tenant_users ENABLE ROW LEVEL SECURITY;

-- Courses Table
CREATE TABLE courses (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    location VARCHAR(255),
    address VARCHAR(255),
    city VARCHAR(100),
    state VARCHAR(100),
    postal_code VARCHAR(50),
    country VARCHAR(100),
    website VARCHAR(255),
    phone VARCHAR(50),
    total_holes INTEGER NOT NULL CHECK (total_holes IN (9, 18, 27, 36)),
    par INTEGER,
    course_rating NUMERIC(4,1),
    slope_rating INTEGER,
    is_public BOOLEAN NOT NULL DEFAULT FALSE,
    created_by UUID REFERENCES auth.users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add courses-related RLS policies
ALTER TABLE courses ENABLE ROW LEVEL SECURITY;

-- Tees Table
CREATE TABLE tees (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    course_id UUID NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    color VARCHAR(50),
    gender VARCHAR(20) CHECK (gender IN ('male', 'female', 'unisex')),
    par INTEGER,
    course_rating NUMERIC(4,1),
    slope_rating INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add tees-related RLS policies
ALTER TABLE tees ENABLE ROW LEVEL SECURITY;

-- Holes Table
CREATE TABLE holes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tee_id UUID NOT NULL REFERENCES tees(id) ON DELETE CASCADE,
    hole_number INTEGER NOT NULL CHECK (hole_number BETWEEN 1 AND 18),
    par INTEGER NOT NULL CHECK (par BETWEEN 3 AND 6),
    stroke_index INTEGER NOT NULL CHECK (stroke_index BETWEEN 1 AND 18),
    distance_meters INTEGER,
    distance_yards INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tee_id, hole_number)
);

-- Add holes-related RLS policies
ALTER TABLE holes ENABLE ROW LEVEL SECURITY;

-- Tournaments Table
CREATE TABLE tournaments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    course_id UUID REFERENCES courses(id),
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    registration_deadline TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL CHECK (status IN ('draft', 'registration', 'active', 'completed', 'cancelled')),
    format VARCHAR(50) NOT NULL CHECK (format IN ('stroke', 'stableford', 'match')),
    is_public BOOLEAN NOT NULL DEFAULT FALSE,
    max_players INTEGER,
    organizer_name VARCHAR(255),
    organizer_email VARCHAR(255),
    organizer_phone VARCHAR(50),
    logo_url VARCHAR(500),
    banner_url VARCHAR(500),
    settings JSONB,
    created_by UUID NOT NULL REFERENCES auth.users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (end_date >= start_date)
);

-- Add tournaments-related RLS policies
ALTER TABLE tournaments ENABLE ROW LEVEL SECURITY;

-- Flights Table (Groups of players within a tournament)
CREATE TABLE flights (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    start_time TIMESTAMPTZ,
    end_time TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add flights-related RLS policies
ALTER TABLE flights ENABLE ROW LEVEL SECURITY;

-- Tournament Players Table
CREATE TABLE tournament_players (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    player_id UUID REFERENCES auth.users(id),
    flight_id UUID REFERENCES flights(id),
    player_name VARCHAR(255) NOT NULL,
    email VARCHAR(255),
    phone VARCHAR(50),
    handicap NUMERIC(4,1),
    tee_id UUID REFERENCES tees(id),
    status VARCHAR(50) NOT NULL CHECK (status IN ('registered', 'confirmed', 'withdrawn')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add tournament_players-related RLS policies
ALTER TABLE tournament_players ENABLE ROW LEVEL SECURITY;

-- Scores Table
CREATE TABLE scores (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    player_id VARCHAR(255) NOT NULL, -- Can be auth.users.id or temporary player ID
    hole_number INTEGER NOT NULL CHECK (hole_number BETWEEN 1 AND 18),
    strokes INTEGER CHECK (strokes BETWEEN 1 AND 20),
    putts INTEGER,
    penalties INTEGER DEFAULT 0,
    fairway_hit BOOLEAN,
    green_in_regulation BOOLEAN,
    recorded_by UUID REFERENCES auth.users(id),
    recorded_at TIMESTAMPTZ NOT NULL,
    status VARCHAR(50) NOT NULL CHECK (status IN ('draft', 'submitted', 'confirmed', 'rejected')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tournament_id, player_id, hole_number)
);

-- Add scores-related RLS policies
ALTER TABLE scores ENABLE ROW LEVEL SECURITY;

-- Tee Times Table
CREATE TABLE tee_times (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    tee_time TIMESTAMPTZ NOT NULL,
    tee_box VARCHAR(50),
    flight_id UUID REFERENCES flights(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Add tee_times-related RLS policies
ALTER TABLE tee_times ENABLE ROW LEVEL SECURITY;

-- Tee Time Players Junction Table
CREATE TABLE tee_time_players (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tee_time_id UUID NOT NULL REFERENCES tee_times(id) ON DELETE CASCADE,
    tournament_player_id UUID NOT NULL REFERENCES tournament_players(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tee_time_id, tournament_player_id)
);

-- Add tee_time_players-related RLS policies
ALTER TABLE tee_time_players ENABLE ROW LEVEL SECURITY;

-- Triggers for updated_at columns
CREATE OR REPLACE FUNCTION update_modified_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply the trigger to all tables with updated_at column
CREATE TRIGGER update_tenants_modtime
    BEFORE UPDATE ON tenants
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_profiles_modtime
    BEFORE UPDATE ON profiles
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_tenant_users_modtime
    BEFORE UPDATE ON tenant_users
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_courses_modtime
    BEFORE UPDATE ON courses
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_tees_modtime
    BEFORE UPDATE ON tees
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_holes_modtime
    BEFORE UPDATE ON holes
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_tournaments_modtime
    BEFORE UPDATE ON tournaments
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_flights_modtime
    BEFORE UPDATE ON flights
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_tournament_players_modtime
    BEFORE UPDATE ON tournament_players
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_scores_modtime
    BEFORE UPDATE ON scores
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

CREATE TRIGGER update_tee_times_modtime
    BEFORE UPDATE ON tee_times
    FOR EACH ROW EXECUTE FUNCTION update_modified_column();

COMMIT;
