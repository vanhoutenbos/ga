# Database Design for Multi-Tenant Golf Tournament Application

## Overview
This document outlines the database schema and design principles for the Golf Tournament Organizer application. It is intended to guide developers in implementing a scalable, secure, and efficient PostgreSQL database leveraging Supabase with .NET 8 and Entity Framework.

## Core Design Principles

### Multi-Tenancy Model
Our application employs a robust multi-tenant architecture with the following characteristics:

- **Logical Data Isolation**: Using PostgreSQL's Row-Level Security (RLS) to enforce tenant isolation
- **Tenant Ownership**: Every resource is associated with a specific tenant
- **Security by Design**: Authorization enforced at the database level through RLS policies
- **Scalability**: Schema designed to support many concurrent tenants and tournaments

### PostgreSQL Best Practices

1. **UUID Primary Keys**
   - Using UUIDs instead of sequential IDs for improved security and distributed systems
   - Default generation with `gen_random_uuid()` function
   - Consistent ID pattern across all tables

2. **Timestamps and Audit Fields**
   - All tables include:
     - `created_at` with default `CURRENT_TIMESTAMP`
     - `updated_at` maintained through triggers
     - `created_by` linked to the auth system for accountability

3. **Data Integrity**
   - Foreign key constraints to maintain referential integrity
   - CHECK constraints to enforce business rules
   - UNIQUE constraints to prevent duplicates
   - NOT NULL constraints where appropriate

4. **Performance Considerations**
   - Appropriate indexes for frequent query patterns
   - Database views for complex reporting queries
   - Materialized views for computationally expensive calculations
   - Partitioning strategy for large historical datasets

5. **JSON/JSONB Usage**
   - JSON data types for flexible, schema-less data where appropriate
   - JSONB for better query performance and indexing capabilities
   - GIN indexes on JSONB columns frequently queried

## Multi-Tenant Schema Design

### Tenant Management

```sql
-- Tenants
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    domain TEXT,
    plan_type TEXT NOT NULL DEFAULT 'free' CHECK (plan_type IN ('free', 'basic', 'premium', 'enterprise')),
    is_active BOOLEAN NOT NULL DEFAULT true,
    settings JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Tenant Users
CREATE TABLE tenant_users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    role TEXT NOT NULL CHECK (role IN ('admin', 'organizer', 'player', 'recorder', 'spectator')),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tenant_id, user_id)
);

-- Function to get current tenant ID
CREATE OR REPLACE FUNCTION get_current_tenant_id()
RETURNS UUID AS $$
BEGIN
    RETURN current_setting('app.tenant_id', true)::UUID;
EXCEPTION
    WHEN OTHERS THEN
        RETURN NULL;
END;
$$ LANGUAGE plpgsql;
```

### Core Tables

```sql
-- User Profiles (extending auth.users)
CREATE TABLE profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id),
    display_name TEXT NOT NULL,
    first_name TEXT,
    last_name TEXT,
    email TEXT UNIQUE,
    phone TEXT,
    handicap NUMERIC(4,1),
    profile_picture_url TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Golf Courses
CREATE TABLE courses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    name TEXT NOT NULL,
    location TEXT,
    address TEXT,
    city TEXT,
    state TEXT,
    postal_code TEXT,
    country TEXT,
    website TEXT,
    phone TEXT,
    total_holes INTEGER NOT NULL DEFAULT 18 CHECK (total_holes IN (9, 18, 27, 36)),
    par INTEGER,
    course_rating NUMERIC(4,1),
    slope_rating INTEGER,
    is_public BOOLEAN DEFAULT true,
    created_by UUID REFERENCES auth.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Course Tees
CREATE TABLE tees (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    course_id UUID NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    color TEXT,
    gender TEXT CHECK (gender IN ('male', 'female', 'unisex')),
    par INTEGER,
    course_rating NUMERIC(4,1),
    slope_rating INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(course_id, name)
);

-- Holes (representing each hole from each tee)
CREATE TABLE holes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tee_id UUID NOT NULL REFERENCES tees(id) ON DELETE CASCADE,
    hole_number INTEGER NOT NULL,    par INTEGER NOT NULL,
    stroke_index INTEGER NOT NULL,
    distance_meters INTEGER,
    distance_yards INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tee_id, hole_number),
    UNIQUE(tee_id, stroke_index)
);

-- Tournaments
CREATE TABLE tournaments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    name TEXT NOT NULL,
    description TEXT,
    course_id UUID REFERENCES courses(id),
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    registration_deadline TIMESTAMP WITH TIME ZONE,
    status TEXT NOT NULL CHECK (status IN ('draft', 'registration', 'active', 'completed', 'cancelled')),
    format TEXT NOT NULL CHECK (format IN ('stroke', 'stableford', 'match')),
    is_public BOOLEAN DEFAULT true,
    max_players INTEGER,
    organizer_name TEXT,
    organizer_email TEXT,
    organizer_phone TEXT,
    logo_url TEXT,
    banner_url TEXT,
    settings JSONB,
    created_by UUID NOT NULL REFERENCES auth.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Tournament Players
CREATE TABLE tournament_players (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    player_id UUID REFERENCES auth.users(id),
    player_name TEXT NOT NULL,
    email TEXT,
    phone TEXT,
    handicap NUMERIC(4,1),
    tee_id UUID REFERENCES tees(id),
    status TEXT NOT NULL CHECK (status IN ('registered', 'confirmed', 'withdrawn')),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(tournament_id, player_id) -- One player per tournament
);

-- Flights (groups of players)
CREATE TABLE flights (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    start_time TIMESTAMP WITH TIME ZONE,
    tee_id UUID REFERENCES tees(id),
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Flight Players
CREATE TABLE flight_players (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    flight_id UUID NOT NULL REFERENCES flights(id) ON DELETE CASCADE,
    tournament_player_id UUID NOT NULL REFERENCES tournament_players(id) ON DELETE CASCADE,
    starting_position INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(flight_id, tournament_player_id) -- One player per flight
);

-- Scores
CREATE TABLE scores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    player_id UUID NOT NULL REFERENCES tournament_players(id) ON DELETE CASCADE,
    hole_number INTEGER NOT NULL CHECK (hole_number > 0 AND hole_number <= 36),
    strokes INTEGER NOT NULL CHECK (strokes > 0),
    penalties INTEGER DEFAULT 0,
    putts INTEGER,
    fairway_hit BOOLEAN,
    green_in_regulation BOOLEAN,
    sand_shots INTEGER DEFAULT 0,
    recorded_by UUID REFERENCES auth.users(id),
    recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    client_generated_id TEXT, -- For offline sync tracking
    is_modified BOOLEAN DEFAULT false,
    sync_status TEXT DEFAULT 'synced' CHECK (sync_status IN ('synced', 'pending', 'conflict')),
    UNIQUE(tournament_id, player_id, hole_number) -- One score per hole per player per tournament
);

-- Access Codes (for temporary tournament access)
CREATE TABLE access_codes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
    code TEXT NOT NULL UNIQUE,
    purpose TEXT NOT NULL CHECK (purpose IN ('player', 'recorder', 'admin')),
    flight_id UUID REFERENCES flights(id),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    is_used BOOLEAN DEFAULT false,
    used_by UUID REFERENCES auth.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
```

### Database Views for Analytics and Reporting

```sql
-- Leaderboard view for stroke play tournaments
CREATE VIEW stroke_leaderboard AS
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

-- Leaderboard view for stableford tournaments
CREATE VIEW stableford_leaderboard AS
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

-- Tournament statistics view
CREATE VIEW tournament_statistics AS
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

-- Player performance history
CREATE VIEW player_performance AS
SELECT
    p.id AS player_id,
    p.display_name,
    t.id AS tournament_id,
    t.tenant_id,
    t.name AS tournament_name,
    t.start_date,
    c.name AS course_name,
    c.par AS course_par,
    COUNT(s.id) AS holes_played,
    SUM(s.strokes) AS total_strokes,
    SUM(s.strokes) - SUM(h.par) AS score_to_par,
    AVG(s.strokes) AS avg_strokes_per_hole,
    SUM(CASE WHEN s.strokes = h.par THEN 1 ELSE 0 END) AS pars,
    SUM(CASE WHEN s.strokes = h.par - 1 THEN 1 ELSE 0 END) AS birdies,
    SUM(CASE WHEN s.strokes <= h.par - 2 THEN 1 ELSE 0 END) AS eagles_or_better,
    SUM(CASE WHEN s.strokes = h.par + 1 THEN 1 ELSE 0 END) AS bogeys,
    SUM(CASE WHEN s.strokes >= h.par + 2 THEN 1 ELSE 0 END) AS double_bogeys_or_worse
FROM profiles p
JOIN auth.users u ON p.id = u.id
JOIN tournament_players tp ON p.id = tp.player_id
JOIN tournaments t ON tp.tournament_id = t.id
JOIN courses c ON t.course_id = c.id
LEFT JOIN scores s ON tp.id = s.player_id
LEFT JOIN holes h ON tp.tee_id = h.tee_id AND s.hole_number = h.hole_number
GROUP BY p.id, p.display_name, t.id, t.tenant_id, t.name, t.start_date, c.name, c.par;
```

## Row-Level Security (RLS) Policies

Security in a multi-tenant environment is crucial. Supabase leverages PostgreSQL's RLS to enforce data isolation at the database level.

```sql
-- Enable RLS on all tables
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_users ENABLE ROW LEVEL SECURITY;
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE courses ENABLE ROW LEVEL SECURITY;
ALTER TABLE tees ENABLE ROW LEVEL SECURITY;
ALTER TABLE holes ENABLE ROW LEVEL SECURITY;
ALTER TABLE tournaments ENABLE ROW LEVEL SECURITY;
ALTER TABLE tournament_players ENABLE ROW LEVEL SECURITY;
ALTER TABLE flights ENABLE ROW LEVEL SECURITY;
ALTER TABLE flight_players ENABLE ROW LEVEL SECURITY;
ALTER TABLE scores ENABLE ROW LEVEL SECURITY;
ALTER TABLE access_codes ENABLE ROW LEVEL SECURITY;

-- Tenant isolation policies
CREATE POLICY "Tenant isolation for courses" ON courses
    USING (tenant_id = get_current_tenant_id());

CREATE POLICY "Tenant isolation for tournaments" ON tournaments
    USING (tenant_id = get_current_tenant_id());
    
-- Public access policies
CREATE POLICY "Public tournaments are visible to everyone" ON tournaments
    FOR SELECT USING (is_public = true);
    
CREATE POLICY "Public courses are visible to everyone" ON courses
    FOR SELECT USING (is_public = true);
    
-- Creator access policies
CREATE POLICY "Course creators can manage their courses" ON courses
    USING (created_by = auth.uid());
    
CREATE POLICY "Tournament creators can manage their tournaments" ON tournaments
    FOR ALL USING (created_by = auth.uid());
    
-- Player access policies
CREATE POLICY "Players can view their tournament data" ON tournament_players
    FOR SELECT USING (player_id = auth.uid());
    
CREATE POLICY "Players can enter scores for themselves" ON scores
    FOR INSERT USING (
        EXISTS (
            SELECT 1 FROM tournament_players tp
            WHERE tp.id = player_id AND tp.player_id = auth.uid()
        )
    );
    
-- Recorder access policies
CREATE POLICY "Recorders can enter scores for assigned flights" ON scores
    FOR INSERT USING (
        EXISTS (
            SELECT 1 FROM access_codes ac
            JOIN flight_players fp ON ac.flight_id = fp.flight_id
            WHERE ac.purpose = 'recorder'
              AND ac.is_used = true
              AND ac.used_by = auth.uid()
              AND fp.tournament_player_id = scores.player_id
        )
    );
```

## Database Triggers and Functions

To maintain data consistency and automate certain operations:

```sql
-- Automatic updated_at timestamp
CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply timestamp trigger to all tables with updated_at
CREATE TRIGGER update_tournaments_timestamp
BEFORE UPDATE ON tournaments
FOR EACH ROW EXECUTE FUNCTION update_timestamp();

-- Similar triggers for other tables...

-- Function to calculate Stableford points
CREATE OR REPLACE FUNCTION calculate_stableford_points(strokes INT, par INT, handicap_strokes INT)
RETURNS INT AS $$
DECLARE
    adjusted_strokes INT;
    points INT;
BEGIN
    adjusted_strokes := strokes - handicap_strokes;
    
    IF adjusted_strokes <= par - 3 THEN
        points := 5;
    ELSIF adjusted_strokes = par - 2 THEN
        points := 4;
    ELSIF adjusted_strokes = par - 1 THEN
        points := 3;
    ELSIF adjusted_strokes = par THEN
        points := 2;
    ELSIF adjusted_strokes = par + 1 THEN
        points := 1;
    ELSE
        points := 0;
    END IF;
    
    RETURN points;
END;
$$ LANGUAGE plpgsql;
```

## Entity Framework Integration

When using Entity Framework Core with PostgreSQL, follow these practices:

1. **Entity Configuration**
   - Use fluent API to configure entities matching database schema
   - Configure tenant discriminators for multi-tenancy filtering
   - Set up appropriate indexes and relationships

2. **DbContext Configuration**
   - Implement tenant isolation through global query filters
   - Configure PostgreSQL-specific options
   - Set up connection resiliency and retries

3. **Example Entity Configuration**

```csharp
// Tournament Entity Configuration
public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("tournaments");
        
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        
        builder.Property(t => t.TenantId)
               .HasColumnName("tenant_id")
               .IsRequired();
               
        builder.Property(t => t.Name)
               .HasColumnName("name")
               .IsRequired();
        
        // Additional properties...
        
        // Define relationships
        builder.HasOne(t => t.Tenant)
               .WithMany()
               .HasForeignKey(t => t.TenantId);
               
        builder.HasMany(t => t.Players)
               .WithOne(tp => tp.Tournament)
               .HasForeignKey(tp => tp.TournamentId);
               
        // Global query filter for multi-tenancy
        builder.HasQueryFilter(t => t.TenantId == _tenantProvider.GetTenantId());
    }
}
```

## Offline Support and Synchronization Strategy

For a golf application that needs to work in areas with limited connectivity:

1. **Client-Side Storage**
   - Use IndexedDB for offline data storage
   - Create local tables mirroring server schema
   - Implement versioning for conflict resolution

2. **Synchronization Mechanism**
   - Track changes with version vectors or timestamps
   - Use unique client-generated IDs for new records
   - Implement conflict detection and resolution logic

3. **Server-Side Support**
   - Batch processing endpoint for sync operations
   - Last-write-wins or custom merge strategies
   - Audit trail of sync activities

## Performance Optimization

To ensure the database performs well as it scales:

1. **Indexing Strategy**
   - Create indexes on frequently queried columns
   - Use partial indexes for filtered queries
   - Add composite indexes for multi-column filters

2. **Query Optimization**
   - Use materialized views for complex aggregations
   - Implement query decomposition for complex operations
   - Consider denormalization for read-heavy operations

3. **Example Indexes**

```sql
-- Common query path indexes
CREATE INDEX idx_tournaments_tenant_status ON tournaments(tenant_id, status);
CREATE INDEX idx_tournament_players_tournament ON tournament_players(tournament_id);
CREATE INDEX idx_scores_tournament_player ON scores(tournament_id, player_id);
CREATE INDEX idx_access_codes_code ON access_codes(code);

-- Partial indexes for common filters
CREATE INDEX idx_active_tournaments ON tournaments(tenant_id, start_date) 
WHERE status = 'active';

-- Indexes for sorting and filtering leaderboards
CREATE INDEX idx_scores_for_leaderboard ON scores(tournament_id, player_id, strokes);
```

## Backup and Disaster Recovery

Implement a robust backup strategy:

```sql
-- Create a backup schema for point-in-time snapshots
CREATE SCHEMA backup_[timestamp];

-- Create table copies for critical data
CREATE TABLE backup_[timestamp].tournaments AS SELECT * FROM tournaments;
CREATE TABLE backup_[timestamp].tournament_players AS SELECT * FROM tournament_players;
CREATE TABLE backup_[timestamp].scores AS SELECT * FROM scores;

-- Regular backups via pg_dump
-- pg_dump -U postgres -d your_database -F c -f backup_file.dump
```

## Migration Strategy

As the application evolves, migrations need careful planning:

1. **Schema Evolution**
   - Use EF Core migrations or custom SQL scripts
   - Always implement backward compatibility periods
   - Create migration test plans for critical changes

2. **Data Migration**
   - Plan for large data migrations during off-peak hours
   - Implement migration in batches for large tables
   - Create validation scripts to verify data integrity

## Conclusion

This database design provides a solid foundation for a multi-tenant golf tournament application using PostgreSQL via Supabase, with .NET 8 and Entity Framework Core. The design emphasizes:

- Secure multi-tenant data isolation
- Flexible schema to support various tournament formats
- Performance optimization for scale
- Robust offline capabilities
- Compliance with PostgreSQL best practices

By following these guidelines, the application will have a database structure that supports growth while maintaining performance and security.
