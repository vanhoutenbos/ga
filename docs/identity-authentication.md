# Identity and Authentication Strategy

The Golf Tournament Organizer Application requires a robust, secure, and cost-effective authentication and authorization system that works across both hosted SaaS and self-hosted deployments. This section outlines the selected approach based on security best practices, cost considerations, and compatibility with the open-core model.

## Authentication Approach

### Recommended Solution for MVP: Supabase Auth

For the MVP phase, Supabase Auth has been selected as the authentication solution for the following reasons:

1. **Cost-Effectiveness**
   - Free tier supports up to 50,000 monthly active users
   - No upfront implementation costs beyond integration
   - Scales efficiently with user growth

2. **Developer Experience**
   - Pre-built UI components for auth flows
   - Simple SDK integration with React
   - Built-in support for social logins

3. **Security**
   - Industry-standard JWT-based approach
   - Handles secure password hashing and token management
   - Built-in email verification and password reset flows
   - MFA capabilities for future enhancements

4. **Speed to Market**
   - Reduces authentication development time from weeks to days
   - Allows focus on core tournament functionality

5. **Flexible Integration**
   - Works well with the planned PostgreSQL database (also via Supabase)
   - Supports planned PWA offline capabilities
   - Can be adapted for self-hosted deployments

### Architecture Components

1. **Supabase Identity Service**
   - Handles user registration, login, and token issuance
   - Manages JWT generation and verification
   - Provides social login connectors
   - Supports row-level security (RLS) for authorization

2. **Token Strategy**
   - JWT-based access tokens
   - Automatic token refresh handling
   - JWT claims for role-based authorization data
   - Client-side token storage with appropriate security measures

3. **Integration Options**
   - JavaScript/TypeScript SDK for React frontend
   - OAuth 2.0 social logins (Google, Apple, etc.)
   - Email/password authentication
   - Custom tournament-specific access codes (implemented on application layer)

## User Identity Model

### User Types and Profiles

1. **Tournament Organizers**
   - Complete profile with contact information
   - Organization affiliation
   - Verified email required
   - Subscription tier tracking

2. **Players**
   - Basic profile (name, optional email)
   - Optional persistent accounts
   - Temporary access via secure tokens/codes
   - Historical performance data

3. **Recorders**
   - Authorized by tournament organizers
   - Temporary or persistent credentials
   - Limited scope of access

4. **Spectators**
   - Anonymous access to public tournament data
   - Optional accounts for preferences/favorites

### Supabase Implementation

```sql
-- Built-in Supabase Auth handles core user authentication
-- Custom profiles table for user metadata
CREATE TABLE profiles (
  id UUID REFERENCES auth.users PRIMARY KEY,
  user_type TEXT NOT NULL CHECK (user_type IN ('organizer', 'player', 'recorder', 'spectator')),
  display_name TEXT NOT NULL,
  first_name TEXT,
  last_name TEXT,
  phone_number TEXT,
  organization TEXT,
  handicap_index FLOAT,
  subscription_tier TEXT DEFAULT 'free',
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Tournament access codes for players and recorders
CREATE TABLE access_codes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  code TEXT NOT NULL UNIQUE,
  tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
  user_id UUID REFERENCES auth.users,
  access_type TEXT NOT NULL CHECK (access_type IN ('player', 'recorder')),
  flight_id UUID REFERENCES flights(id),
  is_used BOOLEAN DEFAULT FALSE,
  expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Row-Level Security Policy Examples
-- Only organizers can create tournaments
CREATE POLICY "Only organizers can create tournaments" ON tournaments
  FOR INSERT TO authenticated
  USING (EXISTS (
    SELECT 1 FROM profiles
    WHERE profiles.id = auth.uid() AND profiles.user_type = 'organizer'
  ));

-- Organizers can only manage their own tournaments
CREATE POLICY "Organizers can manage own tournaments" ON tournaments
  FOR UPDATE TO authenticated
  USING (created_by = auth.uid());
```

## Authorization Model

### Role-Based Access Control with Supabase RLS

Supabase Row Level Security (RLS) provides a powerful and flexible way to implement the permission matrix directly at the database level:

1. **System Roles**
   - `Admin`: System administrators (hosted version only)
   - `Organizer`: Tournament creation and management
   - `Recorder`: Authorized to enter scores for players
   - `Player`: Participating in tournaments
   - `Spectator`: Viewing tournaments (default for unauthenticated users)

2. **Resource-Based Permissions**
   - Tournament-level permissions via RLS policies
   - Course-level permissions via RLS policies
   - Player data permissions via RLS policies

3. **Permission Matrix Implementation**

| Resource Action | Implementation Strategy |
|-----------------|--------------------------|
| View Tournaments | Public tables with RLS for filtering sensitive data |
| Create Tournament | RLS policy restricting creation to organizers |
| Manage Tournament | RLS policy allowing updates only by creator |
| Enter Scores | RLS policies for players/recorders based on tournament access |
| View Leaderboard | Public view with RLS filtering for unpublished data |
| Manage Players | RLS policy linking to tournament ownership |
| View Player Stats | RLS policy for personal stats vs. tournament-wide stats |

### Supabase RLS Policy Examples

```sql
-- Allow public access to tournaments
CREATE POLICY "Public tournaments are viewable by everyone" ON tournaments
  FOR SELECT USING (is_public = true);

-- Tournament creators can see all their tournaments
CREATE POLICY "Users can view own tournaments" ON tournaments
  FOR SELECT TO authenticated
  USING (created_by = auth.uid());

-- Only recorders assigned to specific flights can enter scores
CREATE POLICY "Recorders can only update assigned flights" ON scores
  FOR INSERT TO authenticated
  USING (
    EXISTS (
      SELECT 1 FROM access_codes
      WHERE access_codes.user_id = auth.uid()
        AND access_codes.tournament_id = scores.tournament_id
        AND access_codes.flight_id = scores.flight_id
        AND access_codes.access_type = 'recorder'
    )
  );
```

## Authentication Flows

### Tournament Organizer Flow with Supabase

1. **Registration**
   - Email/password or social login via Supabase Auth UI
   - Email verification handled by Supabase
   - Organization details stored in profiles table

2. **Authentication**
   - Login via Supabase Auth UI or headless API
   - Social login options (Google, Apple, etc.)
   - Token handling managed by Supabase client library

3. **Session Management**
   - JWT-based session management
   - Automatic token refresh
   - Multi-device support

### Player Authentication Options

1. **Tournament-Specific Access Codes**
   - Generated through application logic
   - Stored in custom access_codes table
   - QR code or alphanumeric entry
   - Temporary JWT issuance upon verification

2. **Persistent Player Accounts (Optional)**
   - Standard Supabase Auth registration
   - Social login options
   - Profile linkage to tournament access codes

### Recorder Authentication

1. **Organizer-Assigned Credentials**
   - Custom access codes table for tournament-specific access
   - Limited JWT claims for specific tournament/flight access
   - Optional persistent Supabase Auth accounts

## Implementation Approach

### Supabase Integration

1. **Frontend Integration**
   - Supabase JavaScript client for React
   - Auth UI components for registration/login flows
   - Custom components for tournament-specific access
   - Secure token storage in browser

2. **Backend Integration**
   - Supabase PostgreSQL database with RLS
   - Custom middleware for JWT validation in Azure Functions
   - API endpoints for tournament access code generation
   - Webhook handlers for auth events (signup, password reset)

3. **Self-Hosted Deployment Support**
   - Documentation for self-hosted Supabase setup
   - Migration scripts for moving from hosted to self-hosted
   - Configuration options for auth settings

## Privacy and Data Protection

1. **Data Minimization**
   - Collect only necessary user information
   - Optional player profiles with clear consent
   - Anonymous spectator access

2. **Data Retention Policy**
   - Clear lifecycle for tournament-specific credentials
   - Account deletion capabilities
   - Data export functionality (GDPR compliance)

3. **Consent Management**
   - Granular consent tracking
   - Preference management interface
   - Age verification for youth tournaments

## Implementation Roadmap

### MVP Authentication Features

1. **Phase 1: Core Identity System**
   - Basic user registration and login
   - JWT token issuance and validation
   - Role-based authorization framework

2. **Phase 2: Tournament Access Controls**
   - Temporary access codes for players
   - Recorder credential management
   - Tournament-specific permission enforcement

3. **Phase 3: Enhanced Security Features**
   - Refresh token rotation
   - Account recovery flows
   - Basic audit logging
