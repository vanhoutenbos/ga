# Supabase Implementation Guide

This document outlines the implementation details for using Supabase as the backend infrastructure for the Golf Tournament Organizer application. Supabase provides an open-source Firebase alternative that includes authentication, PostgreSQL database, storage, and real-time subscriptions.

## Why Supabase for This Project

Supabase offers several advantages that align with the Golf Tournament Organizer requirements:

1. **PostgreSQL Database**: Advanced relational database capabilities with row-level security for fine-grained access control
2. **Built-in Authentication**: Complete auth system with multiple sign-in methods and JWT token handling
3. **Real-time Capabilities**: Native support for real-time subscriptions critical for leaderboard updates
4. **Storage Solution**: Integrated storage for tournament media and documents
5. **Open Source**: Aligns with our open-core model and can be self-hosted for enterprise deployments
6. **Free Tier**: Generous free tier for development and small tournaments

## Architecture Overview

### Core Supabase Services Used

1. **Supabase Auth**: User authentication and identity management
2. **Supabase Database**: PostgreSQL database with row-level security
3. **Supabase Realtime**: Real-time subscriptions for leaderboard updates
4. **Supabase Storage**: Media and document storage
5. **Supabase Edge Functions**: Serverless functions for custom logic (when necessary)

### Integration with Application Architecture

```
┌─────────────────────┐           ┌───────────────────────────┐
│                     │           │                           │
│  React Frontend     │◄─────────►│  Supabase Client Library  │
│  (PWA)              │           │                           │
│                     │           └───────────┬───────────────┘
└─────────────────────┘                       │
                                              │
┌─────────────────────┐           ┌───────────▼───────────────┐
│                     │           │                           │
│  API Gateway        │◄─────────►│  Supabase Platform        │
│  (Express/Node.js)  │           │  - Auth                   │
│                     │           │  - PostgreSQL             │
└─────────────────────┘           │  - Realtime               │
                                  │  - Storage                │
                                  │  - Edge Functions         │
                                  └───────────────────────────┘
```

## Database Schema Design

The PostgreSQL schema leverages Supabase's row-level security (RLS) features to enforce access control directly at the database level.

### Core Tables

```sql
-- Users are managed by Supabase Auth
-- This profiles table extends the auth.users table
CREATE TABLE profiles (
  id UUID REFERENCES auth.users PRIMARY KEY,
  display_name TEXT NOT NULL,
  first_name TEXT,
  last_name TEXT,
  email TEXT UNIQUE,
  phone TEXT,
  handicap NUMERIC(4,1),
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Tournaments
CREATE TABLE tournaments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  description TEXT,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('draft', 'registration', 'active', 'completed', 'cancelled')),
  format TEXT NOT NULL CHECK (format IN ('stroke', 'stableford', 'match')),
  is_public BOOLEAN DEFAULT true,
  created_by UUID REFERENCES auth.users NOT NULL,
  organizer_name TEXT,
  max_players INTEGER,
  registration_deadline TIMESTAMP WITH TIME ZONE,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Golf Courses
CREATE TABLE courses (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  location TEXT,
  par INTEGER NOT NULL,
  holes INTEGER NOT NULL DEFAULT 18,
  created_by UUID REFERENCES auth.users,
  is_public BOOLEAN DEFAULT true,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Course Holes
CREATE TABLE holes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  course_id UUID REFERENCES courses NOT NULL,
  hole_number INTEGER NOT NULL,
  par INTEGER NOT NULL,
  stroke_index INTEGER NOT NULL,
  length_yards INTEGER,
  length_meters INTEGER,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(course_id, hole_number)
);

-- Tournament Players
CREATE TABLE tournament_players (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tournament_id UUID REFERENCES tournaments NOT NULL,
  player_id UUID REFERENCES profiles,
  player_name TEXT NOT NULL,
  email TEXT,
  phone TEXT,
  handicap NUMERIC(4,1),
  status TEXT NOT NULL CHECK (status IN ('registered', 'confirmed', 'withdrawn')),
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(tournament_id, player_id)
);

-- Flights
CREATE TABLE flights (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tournament_id UUID REFERENCES tournaments NOT NULL,
  name TEXT NOT NULL,
  start_time TIMESTAMP WITH TIME ZONE,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Flight Players
CREATE TABLE flight_players (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  flight_id UUID REFERENCES flights NOT NULL,
  tournament_player_id UUID REFERENCES tournament_players NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(flight_id, tournament_player_id)
);

-- Scores
CREATE TABLE scores (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tournament_id UUID REFERENCES tournaments NOT NULL,
  player_id UUID REFERENCES tournament_players NOT NULL,
  hole_number INTEGER NOT NULL,
  strokes INTEGER NOT NULL,
  recorded_by UUID REFERENCES auth.users,
  recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(tournament_id, player_id, hole_number)
);

-- Access Codes (for temporary tournament access)
CREATE TABLE access_codes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tournament_id UUID REFERENCES tournaments NOT NULL,
  code TEXT NOT NULL UNIQUE,
  purpose TEXT NOT NULL CHECK (purpose IN ('player', 'recorder', 'admin')),
  flight_id UUID REFERENCES flights,
  expires_at TIMESTAMP WITH TIME ZONE,
  is_used BOOLEAN DEFAULT false,
  used_by UUID REFERENCES auth.users,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
```

### Database Views

```sql
-- Leaderboard view for stroke play
CREATE VIEW stroke_leaderboard AS
SELECT
  t.id AS tournament_id,
  t.name AS tournament_name,
  tp.id AS player_id,
  tp.player_name,
  tp.handicap,
  COUNT(s.id) AS holes_played,
  SUM(s.strokes) AS total_strokes,
  SUM(s.strokes) - SUM(h.par) AS score_to_par,
  ROW_NUMBER() OVER (PARTITION BY t.id ORDER BY SUM(s.strokes)) AS position
FROM tournaments t
JOIN tournament_players tp ON t.id = tp.tournament_id
LEFT JOIN scores s ON tp.id = s.player_id
LEFT JOIN holes h ON t.course_id = h.course_id AND s.hole_number = h.hole_number
WHERE t.format = 'stroke'
GROUP BY t.id, tp.id
ORDER BY t.id, total_strokes;

-- Leaderboard view for stableford
CREATE VIEW stableford_leaderboard AS
SELECT
  t.id AS tournament_id,
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
  ROW_NUMBER() OVER (PARTITION BY t.id ORDER BY SUM(
    CASE
      WHEN s.strokes >= h.par + 2 THEN 0
      WHEN s.strokes = h.par + 1 THEN 1
      WHEN s.strokes = h.par THEN 2
      WHEN s.strokes = h.par - 1 THEN 3
      WHEN s.strokes = h.par - 2 THEN 4
      WHEN s.strokes <= h.par - 3 THEN 5
      ELSE 0
    END
  ) DESC) AS position
FROM tournaments t
JOIN tournament_players tp ON t.id = tp.tournament_id
LEFT JOIN scores s ON tp.id = s.player_id
LEFT JOIN holes h ON t.course_id = h.course_id AND s.hole_number = h.hole_number
WHERE t.format = 'stableford'
GROUP BY t.id, tp.id
ORDER BY t.id, points DESC;
```

## Row-Level Security Policies

Supabase RLS policies provide fine-grained access control:

```sql
-- Public tournaments are viewable by everyone
CREATE POLICY "Public tournaments are viewable by everyone" ON tournaments
  FOR SELECT USING (is_public = true);

-- Tournament creators can manage their tournaments
CREATE POLICY "Creators can update own tournaments" ON tournaments
  FOR UPDATE USING (created_by = auth.uid());

-- Only tournament organizers or assigned recorders can add scores
CREATE POLICY "Only authorized users can add scores" ON scores
  FOR INSERT USING (
    EXISTS (
      SELECT 1 FROM tournaments
      WHERE tournaments.id = scores.tournament_id AND tournaments.created_by = auth.uid()
    ) OR
    EXISTS (
      SELECT 1 FROM access_codes
      WHERE access_codes.tournament_id = scores.tournament_id
        AND access_codes.used_by = auth.uid() 
        AND (access_codes.purpose = 'recorder' OR access_codes.purpose = 'admin')
    )
  );

-- Players can view their own scores
CREATE POLICY "Players can view their own scores" ON scores
  FOR SELECT USING (
    EXISTS (
      SELECT 1 FROM tournament_players
      WHERE tournament_players.id = scores.player_id
        AND tournament_players.player_id = auth.uid()
    )
  );

-- Course creators can manage their courses
CREATE POLICY "Course creators can manage their courses" ON courses
  FOR ALL USING (created_by = auth.uid());

-- Public courses are viewable by everyone
CREATE POLICY "Public courses are viewable by everyone" ON courses
  FOR SELECT USING (is_public = true);
```

## Authentication Implementation

### User Registration and Login

```javascript
// Example: User signup
const signUp = async (email, password, userData) => {
  try {
    // Register user with Supabase Auth
    const { user, error } = await supabase.auth.signUp({
      email,
      password,
    });
    
    if (error) throw error;
    
    // Create user profile
    if (user) {
      const { error: profileError } = await supabase
        .from('profiles')
        .insert([
          {
            id: user.id,
            display_name: userData.displayName,
            first_name: userData.firstName,
            last_name: userData.lastName,
            email: email,
          }
        ]);
        
      if (profileError) throw profileError;
    }
    
    return { user };
  } catch (error) {
    return { error };
  }
};

// Example: User login
const signIn = async (email, password) => {
  try {
    const { user, error } = await supabase.auth.signInWithPassword({
      email,
      password,
    });
    
    return { user, error };
  } catch (error) {
    return { error };
  }
};
```

### Tournament Access Codes

```javascript
// Generate a unique access code
const generateAccessCode = async (tournamentId, purpose, flightId = null) => {
  try {
    // Generate random code
    const code = Math.random().toString(36).substring(2, 10).toUpperCase();
    
    // Calculate expiration (24 hours from now)
    const expires = new Date();
    expires.setHours(expires.getHours() + 24);
    
    // Insert code into database
    const { data, error } = await supabase
      .from('access_codes')
      .insert([
        {
          tournament_id: tournamentId,
          code,
          purpose,
          flight_id: flightId,
          expires_at: expires.toISOString(),
        }
      ])
      .select();
    
    if (error) throw error;
    
    return { accessCode: data[0] };
  } catch (error) {
    return { error };
  }
};

// Redeem an access code
const redeemAccessCode = async (code) => {
  try {
    // Get the access code
    const { data: accessCode, error: fetchError } = await supabase
      .from('access_codes')
      .select('*')
      .eq('code', code)
      .single();
    
    if (fetchError) throw fetchError;
    
    // Check if code is valid and not expired
    const now = new Date();
    if (accessCode.is_used || new Date(accessCode.expires_at) < now) {
      throw new Error('Access code is invalid or expired');
    }
    
    // Mark code as used
    const { error: updateError } = await supabase
      .from('access_codes')
      .update({ 
        is_used: true,
        used_by: supabase.auth.user()?.id 
      })
      .eq('id', accessCode.id);
    
    if (updateError) throw updateError;
    
    return { accessCode };
  } catch (error) {
    return { error };
  }
};
```

## Real-time Implementation

Supabase Realtime provides PostgreSQL's replication capabilities through WebSockets.

### Enabling Real-time for Tables

```sql
-- Enable real-time for tournament tables
BEGIN;
  -- Enable replication for these tables
  ALTER PUBLICATION supabase_realtime ADD TABLE tournaments;
  ALTER PUBLICATION supabase_realtime ADD TABLE tournament_players;
  ALTER PUBLICATION supabase_realtime ADD TABLE flights;
  ALTER PUBLICATION supabase_realtime ADD TABLE flight_players;
  ALTER PUBLICATION supabase_realtime ADD TABLE scores;
  
  -- Enable replication for leaderboard views
  ALTER PUBLICATION supabase_realtime ADD TABLE stroke_leaderboard;
  ALTER PUBLICATION supabase_realtime ADD TABLE stableford_leaderboard;
COMMIT;
```

### Frontend Real-time Subscriptions

```javascript
// Subscribe to leaderboard updates
const subscribeToLeaderboard = (tournamentId, format, onUpdate) => {
  const table = format === 'stableford' ? 'stableford_leaderboard' : 'stroke_leaderboard';
  
  const subscription = supabase
    .from(`${table}:tournament_id=eq.${tournamentId}`)
    .on('*', payload => {
      onUpdate(payload.new);
    })
    .subscribe();
    
  return subscription;
};

// Subscribe to score updates for a specific player
const subscribeToPlayerScores = (tournamentId, playerId, onUpdate) => {
  const subscription = supabase
    .from(`scores:tournament_id=eq.${tournamentId},player_id=eq.${playerId}`)
    .on('*', payload => {
      onUpdate(payload.new);
    })
    .subscribe();
    
  return subscription;
};

// Unsubscribe when component unmounts
const unsubscribe = (subscription) => {
  supabase.removeSubscription(subscription);
};
```

## Offline Strategy with Supabase

Implementing offline capabilities with Supabase requires client-side caching and synchronization.

### Score Entry with Offline Support

```javascript
// Submit score with offline support
const submitScore = async (tournamentId, playerId, holeNumber, strokes) => {
  const scoreData = {
    tournament_id: tournamentId,
    player_id: playerId,
    hole_number: holeNumber,
    strokes: strokes,
    recorded_at: new Date().toISOString()
  };
  
  try {
    // Try to submit score to Supabase
    const { data, error } = await supabase
      .from('scores')
      .upsert([scoreData])
      .select();
      
    if (!error) {
      return { score: data[0] };
    }
    
    // If offline, store in IndexedDB
    if (!navigator.onLine) {
      await storeOfflineScore(scoreData);
      return { 
        score: scoreData, 
        status: 'offline',
        message: 'Score saved offline and will sync when connection is restored'
      };
    }
    
    throw error;
  } catch (error) {
    return { error };
  }
};

// Store score in IndexedDB for offline use
const storeOfflineScore = async (scoreData) => {
  // Using localforage for IndexedDB wrapper
  const offlineScores = await localforage.getItem('offlineScores') || [];
  offlineScores.push({
    ...scoreData,
    offlineId: Date.now().toString(),
    synced: false
  });
  await localforage.setItem('offlineScores', offlineScores);
};

// Sync offline scores when online
const syncOfflineScores = async () => {
  const offlineScores = await localforage.getItem('offlineScores') || [];
  const unsynced = offlineScores.filter(score => !score.synced);
  
  if (unsynced.length === 0) return;
  
  for (const score of unsynced) {
    try {
      const { error } = await supabase
        .from('scores')
        .upsert([{
          tournament_id: score.tournament_id,
          player_id: score.player_id,
          hole_number: score.hole_number,
          strokes: score.strokes,
          recorded_at: score.recorded_at
        }]);
        
      if (!error) {
        // Mark as synced
        score.synced = true;
      }
    } catch (error) {
      console.error('Failed to sync score:', error);
    }
  }
  
  // Update storage with sync status
  await localforage.setItem('offlineScores', offlineScores);
  
  // Remove fully synced scores
  const remaining = offlineScores.filter(score => !score.synced);
  if (offlineScores.length !== remaining.length) {
    await localforage.setItem('offlineScores', remaining);
  }
};

// Listen for online events to trigger sync
window.addEventListener('online', syncOfflineScores);
```

## Storage Implementation

Supabase Storage provides a secure way to manage tournament-related files.

```javascript
// Upload tournament logo
const uploadTournamentLogo = async (tournamentId, file) => {
  try {
    const fileExt = file.name.split('.').pop();
    const fileName = `${tournamentId}-logo.${fileExt}`;
    const filePath = `tournaments/${tournamentId}/${fileName}`;
    
    const { data, error } = await supabase
      .storage
      .from('tournament-assets')
      .upload(filePath, file, {
        cacheControl: '3600',
        upsert: true
      });
      
    if (error) throw error;
    
    // Get public URL
    const { publicURL } = supabase
      .storage
      .from('tournament-assets')
      .getPublicUrl(filePath);
      
    // Update tournament with logo URL
    const { error: updateError } = await supabase
      .from('tournaments')
      .update({ logo_url: publicURL })
      .eq('id', tournamentId);
      
    if (updateError) throw updateError;
    
    return { url: publicURL };
  } catch (error) {
    return { error };
  }
};

// Generate and store tournament results PDF
const generateTournamentPDF = async (tournamentId) => {
  try {
    // Generate PDF (using client-side library like jsPDF)
    const pdfBlob = await generatePDF(tournamentId);
    const fileName = `tournament-${tournamentId}-results.pdf`;
    const filePath = `tournaments/${tournamentId}/results/${fileName}`;
    
    // Upload to Supabase Storage
    const { data, error } = await supabase
      .storage
      .from('tournament-results')
      .upload(filePath, pdfBlob, {
        contentType: 'application/pdf',
        cacheControl: '3600',
        upsert: true
      });
      
    if (error) throw error;
    
    // Get public URL
    const { publicURL } = supabase
      .storage
      .from('tournament-results')
      .getPublicUrl(filePath);
      
    return { url: publicURL };
  } catch (error) {
    return { error };
  }
};
```

## Self-Hosting Supabase

For customers requiring self-hosted deployment, provide instructions for setting up Supabase locally.

```bash
# Clone Supabase repository
git clone https://github.com/supabase/supabase
cd supabase/docker

# Configure environment variables
cp .env.example .env
# Edit .env file with appropriate values

# Start Supabase
docker-compose up -d

# Initialize database with schema
# Use migration scripts or SQL files to set up schema
```

### Migration Process for Self-Hosting

1. **Export data from hosted Supabase**
   - Use pg_dump to extract database
   - Export storage files

2. **Deploy self-hosted Supabase instance**
   - Follow Supabase self-hosting guide
   - Configure with appropriate resources

3. **Import data and schema**
   - Import database dump
   - Restore storage files
   - Verify RLS policies

4. **Update application configuration**
   - Change API endpoints to self-hosted instance
   - Update authentication settings

## Monitoring and Maintenance

### Key Metrics to Monitor

1. **Database Performance**
   - Query performance
   - Connection count
   - Table sizes
   - Index usage

2. **API Usage**
   - Request volume
   - Error rates
   - Response times
   - Bandwidth usage

3. **Real-time Performance**
   - Subscription count
   - Message delivery latency
   - WebSocket connection stability

4. **Storage Metrics**
   - Storage usage
   - Download bandwidth
   - Upload frequency

### Backup Strategy

```sql
-- Create a backup schema for point-in-time snapshots
CREATE SCHEMA backup_[timestamp];

-- Copy tables to backup schema
CREATE TABLE backup_[timestamp].tournaments AS SELECT * FROM public.tournaments;
CREATE TABLE backup_[timestamp].tournament_players AS SELECT * FROM public.tournament_players;
-- Repeat for all tables

-- Automated backup with pg_dump
-- Schedule regular pg_dump commands via cron or similar
```

## Security Considerations

1. **API Key Management**
   - Restrict API key usage to specific origins
   - Use service_role key only for trusted server operations
   - Generate separate API keys for different environments

2. **JWT Token Configuration**
   - Set appropriate token expiration
   - Implement token refresh strategy
   - Validate tokens on critical operations

3. **RLS Policy Testing**
   - Regularly audit RLS policies
   - Test policies with different user roles
   - Document policy intention and verification steps

4. **Data Encryption**
   - Enable Supabase Vault for sensitive data
   - Implement client-side encryption for critical fields
   - Use TLS for all connections
