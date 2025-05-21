# Multi-Tenancy Isolation Strategy

## Overview

This document outlines the comprehensive strategy for implementing and validating multi-tenancy isolation within the Golf Tournament Organizer application. As our platform hosts data for multiple tournament organizers, clubs, and players, ensuring proper data isolation between tenants is critical for security, privacy, and compliance.

## Multi-Tenancy Architecture

### Tenancy Model

The Golf Tournament Organizer implements a **virtual multi-tenancy** model where multiple tenants share the same database infrastructure but with strict logical separation. The key tenancy levels are:

1. **Organization Level** (Primary Tenant)
   - Represents golf clubs, tournament organizers, or golf associations
   - Manages multiple tournaments, series, and member players
   - Has organization-specific settings and branding

2. **Tournament Level** (Sub-Tenant)
   - Represents individual tournaments or tournament series
   - Contains player registrations, scores, and leaderboards
   - Has tournament-specific rules and settings

3. **User Level**
   - Individual users may belong to multiple organizations with different roles
   - Players may participate in tournaments across different organizations

### Tenant Identification

Every data entity in the system is associated with a tenant using a combination of:

1. **Tenant ID Field**
   - Every table contains an `organization_id` column for primary tenant isolation
   - Tournament-specific tables include a `tournament_id` for sub-tenant isolation
   - Both fields are mandatory and non-nullable

2. **User-Tenant Association**
   - Users are associated with organizations through the `organization_members` table
   - Membership includes role information (admin, staff, player)
   - Tournament participation is tracked through `tournament_players` table

## Database Isolation Implementation

### Row-Level Security (RLS) Policies

Supabase PostgreSQL Row-Level Security (RLS) policies are the foundation of our multi-tenancy isolation. Example policies:

```sql
-- Organization-level isolation policy
CREATE POLICY organization_isolation_policy ON organizations
  USING (id IN (
    SELECT organization_id FROM organization_members
    WHERE user_id = auth.uid()
  ));

-- Tournament-level isolation policy
CREATE POLICY tournament_isolation_policy ON tournaments
  USING (organization_id IN (
    SELECT organization_id FROM organization_members
    WHERE user_id = auth.uid()
  ));

-- Player data isolation policy
CREATE POLICY player_data_isolation ON player_profiles
  USING (
    -- Players can see their own data
    user_id = auth.uid()
    OR
    -- Organization admins can see players in their organization
    EXISTS (
      SELECT 1 FROM organization_members
      WHERE organization_id = player_profiles.organization_id
      AND user_id = auth.uid()
      AND role = 'admin'
    )
  );
  
-- Score data isolation policy
CREATE POLICY score_isolation ON scores
  USING (
    -- Players can see their own scores
    player_id IN (SELECT id FROM players WHERE user_id = auth.uid())
    OR
    -- Tournament officials can see scores for tournaments they manage
    tournament_id IN (
      SELECT t.id FROM tournaments t
      JOIN tournament_officials o ON t.id = o.tournament_id
      WHERE o.user_id = auth.uid()
    )
    OR
    -- Organization admins can see all scores in their organization
    tournament_id IN (
      SELECT t.id FROM tournaments t
      JOIN organization_members om ON t.organization_id = om.organization_id
      WHERE om.user_id = auth.uid() AND om.role = 'admin'
    )
  );
```

### Foreign Key Constraints

Enforce referential integrity across tenant boundaries:

```sql
-- Ensure tournaments belong to valid organizations
ALTER TABLE tournaments
  ADD CONSTRAINT fk_tournaments_organization
  FOREIGN KEY (organization_id)
  REFERENCES organizations(id)
  ON DELETE CASCADE;

-- Ensure tournament players belong to valid tournaments
ALTER TABLE tournament_players
  ADD CONSTRAINT fk_tournament_players_tournament
  FOREIGN KEY (tournament_id)
  REFERENCES tournaments(id)
  ON DELETE CASCADE;

-- Ensure scores belong to valid tournament players
ALTER TABLE scores
  ADD CONSTRAINT fk_scores_tournament_player
  FOREIGN KEY (tournament_player_id)
  REFERENCES tournament_players(id)
  ON DELETE CASCADE;
```

### Database Schema Design

Our schema design reinforces tenant isolation:

```sql
-- Organizations table (tenant)
CREATE TABLE organizations (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  name TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Organization members (user-tenant relationship)
CREATE TABLE organization_members (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
  role TEXT NOT NULL CHECK (role IN ('admin', 'staff', 'player')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (organization_id, user_id)
);

-- Tournaments table (sub-tenant)
CREATE TABLE tournaments (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  start_date DATE NOT NULL,
  end_date DATE NOT NULL,
  status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'published', 'in_progress', 'completed', 'cancelled')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Tournament players
CREATE TABLE tournament_players (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
  user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
  player_number TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (tournament_id, user_id)
);

-- Player scores
CREATE TABLE scores (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  tournament_id UUID NOT NULL REFERENCES tournaments(id) ON DELETE CASCADE,
  round_id UUID NOT NULL REFERENCES tournament_rounds(id) ON DELETE CASCADE,
  tournament_player_id UUID NOT NULL REFERENCES tournament_players(id) ON DELETE CASCADE,
  hole_number INTEGER NOT NULL,
  strokes INTEGER NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (tournament_player_id, round_id, hole_number)
);
```

## Application-Level Isolation

### API Request Validation

Every API request is validated for proper tenant access:

```typescript
// Middleware to validate organization access
export async function validateOrganizationAccess(
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
) {
  try {
    const { organizationId } = req.params;
    const userId = req.user.id;
    
    // Check if the user is a member of the organization
    const { data, error } = await supabase
      .from('organization_members')
      .select('role')
      .eq('organization_id', organizationId)
      .eq('user_id', userId)
      .single();
    
    if (error || !data) {
      return res.status(403).json({
        error: 'You do not have access to this organization'
      });
    }
    
    // Attach organization role to the request for later use
    req.organizationRole = data.role;
    
    next();
  } catch (error) {
    console.error('Organization access validation error:', error);
    res.status(500).json({
      error: 'Failed to validate organization access'
    });
  }
}

// Middleware to validate tournament access
export async function validateTournamentAccess(
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
) {
  try {
    const { tournamentId } = req.params;
    const userId = req.user.id;
    
    // Get tournament details
    const { data: tournament, error: tournamentError } = await supabase
      .from('tournaments')
      .select('organization_id, status')
      .eq('id', tournamentId)
      .single();
    
    if (tournamentError || !tournament) {
      return res.status(404).json({
        error: 'Tournament not found'
      });
    }
    
    // Check organization membership
    const { data: membership, error: membershipError } = await supabase
      .from('organization_members')
      .select('role')
      .eq('organization_id', tournament.organization_id)
      .eq('user_id', userId)
      .single();
    
    if (membershipError || !membership) {
      // If not a member, check if the tournament is public and published
      if (tournament.status === 'published' || tournament.status === 'in_progress') {
        // Allow read-only access to public tournament data
        req.accessLevel = 'read-only';
        return next();
      }
      
      return res.status(403).json({
        error: 'You do not have access to this tournament'
      });
    }
    
    // Check if user is a tournament official
    const { data: official, error: officialError } = await supabase
      .from('tournament_officials')
      .select('role')
      .eq('tournament_id', tournamentId)
      .eq('user_id', userId)
      .single();
    
    if (official) {
      req.accessLevel = official.role === 'admin' ? 'admin' : 'official';
    } else {
      // If not an official, check if user is a player in the tournament
      const { data: player } = await supabase
        .from('tournament_players')
        .select('id')
        .eq('tournament_id', tournamentId)
        .eq('user_id', userId)
        .single();
      
      if (player) {
        req.accessLevel = 'player';
      } else {
        // Default to organization role-based access
        req.accessLevel = membership.role === 'admin' ? 'admin' : 'viewer';
      }
    }
    
    next();
  } catch (error) {
    console.error('Tournament access validation error:', error);
    res.status(500).json({
      error: 'Failed to validate tournament access'
    });
  }
}
```

### Service Layer Implementation

Our service layer enforces tenant isolation for all data operations:

```typescript
// Tournament service with tenant isolation
export class TournamentService {
  // Create a tournament (checks organization membership)
  async createTournament(userId: string, organizationId: string, tournamentData: TournamentCreateData) {
    // Verify user has permission to create tournaments in this organization
    const { data: membership, error: membershipError } = await supabase
      .from('organization_members')
      .select('role')
      .eq('organization_id', organizationId)
      .eq('user_id', userId)
      .single();
    
    if (membershipError || !membership || !['admin', 'staff'].includes(membership.role)) {
      throw new Error('You do not have permission to create tournaments in this organization');
    }
    
    // Create tournament with organization_id to ensure tenant isolation
    const { data, error } = await supabase
      .from('tournaments')
      .insert({
        ...tournamentData,
        organization_id: organizationId // Enforce tenant association
      })
      .select()
      .single();
    
    if (error) throw error;
    return data;
  }
  
  // Get tournament with tenant checks
  async getTournament(userId: string, tournamentId: string) {
    // Query will be filtered by RLS policies
    const { data, error } = await supabase
      .from('tournaments')
      .select(`
        *,
        organization:organizations(name),
        rounds:tournament_rounds(*),
        course:courses(*)
      `)
      .eq('id', tournamentId)
      .single();
    
    if (error) throw error;
    return data;
  }
  
  // Update tournament with tenant validation
  async updateTournament(userId: string, tournamentId: string, updateData: TournamentUpdateData) {
    // Get the tournament to check organization
    const { data: tournament, error: tournamentError } = await supabase
      .from('tournaments')
      .select('organization_id')
      .eq('id', tournamentId)
      .single();
    
    if (tournamentError || !tournament) {
      throw new Error('Tournament not found');
    }
    
    // Check if user has permission to update this tournament
    const { data: permission, error: permissionError } = await supabase
      .from('organization_members')
      .select('role')
      .eq('organization_id', tournament.organization_id)
      .eq('user_id', userId)
      .single();
    
    if (permissionError || !permission || !['admin', 'staff'].includes(permission.role)) {
      throw new Error('You do not have permission to update this tournament');
    }
    
    // Prevent changing the organization_id to maintain tenant isolation
    const safeUpdateData = { ...updateData };
    delete safeUpdateData.organization_id;
    
    const { data, error } = await supabase
      .from('tournaments')
      .update(safeUpdateData)
      .eq('id', tournamentId)
      .select()
      .single();
    
    if (error) throw error;
    return data;
  }
}
```

### Client-Side Implementation

Our client-side code maintains tenant isolation through:

```typescript
// React context for organization selection
export const OrganizationContext = createContext<OrganizationContextType | undefined>(undefined);

export function OrganizationProvider({ children }: { children: React.ReactNode }) {
  const [selectedOrganizationId, setSelectedOrganizationId] = useState<string | null>(null);
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [loading, setLoading] = useState(true);
  const { user } = useAuth();
  
  useEffect(() => {
    async function loadUserOrganizations() {
      if (!user) {
        setOrganizations([]);
        setSelectedOrganizationId(null);
        setLoading(false);
        return;
      }
      
      try {
        setLoading(true);
        
        // Get organizations the user is a member of
        const { data, error } = await supabase
          .from('organization_members')
          .select(`
            organization:organizations(*)
          `)
          .eq('user_id', user.id);
        
        if (error) throw error;
        
        const userOrganizations = data.map(item => item.organization);
        setOrganizations(userOrganizations);
        
        // Set selected organization from storage or use first one
        const storedOrgId = localStorage.getItem('selectedOrganizationId');
        if (storedOrgId && userOrganizations.some(org => org.id === storedOrgId)) {
          setSelectedOrganizationId(storedOrgId);
        } else if (userOrganizations.length > 0) {
          setSelectedOrganizationId(userOrganizations[0].id);
          localStorage.setItem('selectedOrganizationId', userOrganizations[0].id);
        }
      } catch (error) {
        console.error('Error loading organizations:', error);
      } finally {
        setLoading(false);
      }
    }
    
    loadUserOrganizations();
  }, [user]);
  
  const selectOrganization = useCallback((organizationId: string) => {
    setSelectedOrganizationId(organizationId);
    localStorage.setItem('selectedOrganizationId', organizationId);
  }, []);
  
  return (
    <OrganizationContext.Provider
      value={{
        selectedOrganizationId,
        organizations,
        loading,
        selectOrganization
      }}
    >
      {children}
    </OrganizationContext.Provider>
  );
}

// Custom hook to use organization context
export function useOrganization() {
  const context = useContext(OrganizationContext);
  
  if (context === undefined) {
    throw new Error('useOrganization must be used within an OrganizationProvider');
  }
  
  return context;
}

// Example usage in a component
function TournamentList() {
  const { selectedOrganizationId } = useOrganization();
  const [tournaments, setTournaments] = useState<Tournament[]>([]);
  
  useEffect(() => {
    if (!selectedOrganizationId) return;
    
    // Query only tournaments for the selected organization
    const fetchTournaments = async () => {
      const { data, error } = await supabase
        .from('tournaments')
        .select('*')
        .eq('organization_id', selectedOrganizationId)
        .order('start_date', { ascending: false });
      
      if (!error) {
        setTournaments(data);
      }
    };
    
    fetchTournaments();
  }, [selectedOrganizationId]);
  
  return (
    <div>
      <h2>Tournaments</h2>
      {tournaments.map(tournament => (
        <TournamentCard key={tournament.id} tournament={tournament} />
      ))}
    </div>
  );
}
```

## Testing & Validation Strategy

### Unit Testing

We use unit tests to verify tenant isolation logic:

```typescript
// Test tenant isolation in tournament service
describe('TournamentService Tenant Isolation', () => {
  let tournamentService: TournamentService;
  let testOrg1: string;
  let testOrg2: string;
  let adminUser: string;
  let regularUser: string;
  
  beforeAll(async () => {
    tournamentService = new TournamentService();
    
    // Create test organizations
    const { data: org1 } = await supabase
      .from('organizations')
      .insert({ name: 'Test Org 1' })
      .select()
      .single();
    testOrg1 = org1.id;
    
    const { data: org2 } = await supabase
      .from('organizations')
      .insert({ name: 'Test Org 2' })
      .select()
      .single();
    testOrg2 = org2.id;
    
    // Create test users
    adminUser = (await createTestUser()).id;
    regularUser = (await createTestUser()).id;
    
    // Add admin user to both organizations
    await supabase.from('organization_members').insert([
      { organization_id: testOrg1, user_id: adminUser, role: 'admin' },
      { organization_id: testOrg2, user_id: adminUser, role: 'admin' }
    ]);
    
    // Add regular user only to org1
    await supabase.from('organization_members').insert({
      organization_id: testOrg1, 
      user_id: regularUser, 
      role: 'player'
    });
  });
  
  test('User can only create tournament in organizations they belong to', async () => {
    // Admin can create in org1
    const tournament1 = await tournamentService.createTournament(
      adminUser,
      testOrg1,
      { name: 'Test Tournament 1', start_date: '2023-07-01', end_date: '2023-07-02' }
    );
    expect(tournament1).toBeDefined();
    expect(tournament1.organization_id).toBe(testOrg1);
    
    // Regular user cannot create in org2
    await expect(
      tournamentService.createTournament(
        regularUser,
        testOrg2,
        { name: 'Test Tournament 2', start_date: '2023-07-01', end_date: '2023-07-02' }
      )
    ).rejects.toThrow();
  });
  
  test('User can only update tournaments in their organization', async () => {
    // Create test tournament in org1
    const { data: tournament } = await supabase
      .from('tournaments')
      .insert({
        name: 'Update Test Tournament',
        organization_id: testOrg1,
        start_date: '2023-08-01',
        end_date: '2023-08-02'
      })
      .select()
      .single();
    
    // Create test tournament in org2
    const { data: tournament2 } = await supabase
      .from('tournaments')
      .insert({
        name: 'Org2 Tournament',
        organization_id: testOrg2,
        start_date: '2023-08-01',
        end_date: '2023-08-02'
      })
      .select()
      .single();
      
    // Admin can update in both orgs
    const updatedTournament1 = await tournamentService.updateTournament(
      adminUser,
      tournament.id,
      { name: 'Updated Tournament Name' }
    );
    expect(updatedTournament1.name).toBe('Updated Tournament Name');
    
    const updatedTournament2 = await tournamentService.updateTournament(
      adminUser,
      tournament2.id,
      { name: 'Updated Org2 Tournament' }
    );
    expect(updatedTournament2.name).toBe('Updated Org2 Tournament');
    
    // Regular user can update in org1 but not org2
    await expect(
      tournamentService.updateTournament(
        regularUser,
        tournament2.id,
        { name: 'Should Fail Update' }
      )
    ).rejects.toThrow();
  });
  
  // Clean up after tests
  afterAll(async () => {
    await supabase.from('tournaments').delete().in('organization_id', [testOrg1, testOrg2]);
    await supabase.from('organization_members').delete().in('user_id', [adminUser, regularUser]);
    await supabase.from('organizations').delete().in('id', [testOrg1, testOrg2]);
  });
});
```

### Integration Testing

Integration tests verify tenant isolation across the entire stack:

```typescript
// Integration test for tenant isolation
describe('Tenant Isolation Integration Tests', () => {
  let server;
  let org1AdminToken;
  let org1UserToken;
  let org2AdminToken;
  let testOrg1;
  let testOrg2;
  let testTournament1;
  let testTournament2;
  
  beforeAll(async () => {
    // Start test server
    server = await startTestServer();
    
    // Create test organizations
    testOrg1 = await createTestOrganization('Test Org 1');
    testOrg2 = await createTestOrganization('Test Org 2');
    
    // Create test users and get auth tokens
    org1AdminToken = await createUserAndGetToken({ organizationId: testOrg1, role: 'admin' });
    org1UserToken = await createUserAndGetToken({ organizationId: testOrg1, role: 'player' });
    org2AdminToken = await createUserAndGetToken({ organizationId: testOrg2, role: 'admin' });
    
    // Create test tournaments
    testTournament1 = await createTestTournament(testOrg1, 'Org1 Tournament');
    testTournament2 = await createTestTournament(testOrg2, 'Org2 Tournament');
  });
  
  test('User can only access tournaments in their organization', async () => {
    // Org1 admin can access org1 tournament
    const response1 = await request(server)
      .get(`/api/tournaments/${testTournament1}`)
      .set('Authorization', `Bearer ${org1AdminToken}`);
    
    expect(response1.status).toBe(200);
    expect(response1.body.name).toBe('Org1 Tournament');
    
    // Org1 admin cannot access org2 tournament
    const response2 = await request(server)
      .get(`/api/tournaments/${testTournament2}`)
      .set('Authorization', `Bearer ${org1AdminToken}`);
    
    expect(response2.status).toBe(403);
    
    // Org2 admin can access org2 tournament
    const response3 = await request(server)
      .get(`/api/tournaments/${testTournament2}`)
      .set('Authorization', `Bearer ${org2AdminToken}`);
    
    expect(response3.status).toBe(200);
    expect(response3.body.name).toBe('Org2 Tournament');
  });
  
  test('User cannot create data in another organization', async () => {
    // Try to create a tournament in org2 as org1 user
    const response = await request(server)
      .post('/api/tournaments')
      .set('Authorization', `Bearer ${org1UserToken}`)
      .send({
        name: 'Cross-Tenant Tournament',
        organization_id: testOrg2,
        start_date: '2023-09-01',
        end_date: '2023-09-02'
      });
    
    expect(response.status).toBe(403);
  });
  
  test('User cannot update data in another organization', async () => {
    // Try to update org2 tournament as org1 admin
    const response = await request(server)
      .put(`/api/tournaments/${testTournament2}`)
      .set('Authorization', `Bearer ${org1AdminToken}`)
      .send({
        name: 'Updated Org2 Tournament'
      });
    
    expect(response.status).toBe(403);
  });
  
  afterAll(async () => {
    await cleanupTestData();
    await server.close();
  });
});
```

### Automated Security Testing

We implement automated security tests to validate tenant isolation:

```typescript
// SQL Injection Tests
describe('SQL Injection Protection for Tenant Isolation', () => {
  test('SQL Injection attempts are blocked', async () => {
    const maliciousOrgId = `${validOrgId}' OR '1'='1`;
    
    // Attempt SQL injection in organization param
    const response = await request(server)
      .get(`/api/organizations/${maliciousOrgId}/tournaments`)
      .set('Authorization', `Bearer ${userToken}`);
    
    // Should return error, not expose other tenants' data
    expect(response.status).toBe(400);
    expect(response.body.tournaments).toBeUndefined();
  });
  
  test('Path traversal attempts are blocked', async () => {
    const maliciousPath = '../another-org/tournaments';
    
    // Attempt path traversal
    const response = await request(server)
      .get(`/api/organizations/${validOrgId}/${maliciousPath}`)
      .set('Authorization', `Bearer ${userToken}`);
    
    // Should return error
    expect(response.status).toBe(404);
  });
});

// Authentication Bypass Tests
describe('Authentication Bypass Protection', () => {
  test('Tampered JWTs are rejected', async () => {
    // Create a tampered token with modified organization claims
    const tamperedToken = createTamperedToken();
    
    const response = await request(server)
      .get(`/api/organizations/${testOrg2}/tournaments`)
      .set('Authorization', `Bearer ${tamperedToken}`);
    
    expect(response.status).toBe(401);
  });
});
```

### Penetration Testing

We conduct regular penetration testing focused on multi-tenant isolation:

1. **Cross-Tenant Data Access Attempts**
   - Attempt to enumerate and access other tenant IDs
   - Modify client-side requests to target other tenants
   - Test JWT token manipulation

2. **Authorization Bypass Testing**
   - Test privilege escalation across tenant boundaries
   - Attempt to modify tenant ID in requests
   - Test direct database access bypassing API controls

3. **Service-Level Isolation Testing**
   - Test API endpoints for tenant data leakage
   - Verify tenant context is maintained across service calls
   - Test batch operations for tenant isolation

## Monitoring & Alerting

### Tenant Isolation Breach Monitoring

We implement specific monitoring for tenant isolation breaches:

```sql
-- Create tenant access logging
CREATE TABLE tenant_access_logs (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  user_id UUID REFERENCES auth.users(id),
  organization_id UUID REFERENCES organizations(id),
  action TEXT NOT NULL,
  resource_type TEXT NOT NULL,
  resource_id UUID,
  success BOOLEAN NOT NULL,
  error_message TEXT,
  request_data JSONB,
  client_ip TEXT,
  user_agent TEXT,
  timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Create function to log access
CREATE OR REPLACE FUNCTION log_tenant_access() 
RETURNS TRIGGER AS $$
BEGIN
  INSERT INTO tenant_access_logs (
    user_id,
    organization_id,
    action,
    resource_type,
    resource_id,
    success,
    error_message,
    request_data,
    client_ip,
    user_agent,
    timestamp
  ) VALUES (
    auth.uid(),
    NEW.organization_id,
    TG_OP,
    TG_TABLE_NAME,
    NEW.id,
    TRUE,
    NULL,
    row_to_json(NEW),
    current_setting('request.headers', true)::json->>'x-forwarded-for',
    current_setting('request.headers', true)::json->>'user-agent',
    NOW()
  );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Apply trigger to key tables
CREATE TRIGGER log_tournament_access
AFTER INSERT OR UPDATE ON tournaments
FOR EACH ROW EXECUTE FUNCTION log_tenant_access();
```

### Suspicious Activity Alerts

Configure alerts for suspicious multi-tenant activities:

```typescript
// Azure Function to monitor for suspicious tenant access
import { AzureFunction, Context } from "@azure/functions";
import { supabase } from "../lib/supabaseClient";

const timerTrigger: AzureFunction = async function (context: Context): Promise<void> {
  // Check for users accessing multiple organizations in short timeframe
  const { data: suspiciousAccess } = await supabase.rpc('detect_suspicious_tenant_access');
  
  for (const access of suspiciousAccess) {
    // Log suspicious activity
    context.log.warn(`Suspicious cross-tenant access: User ${access.user_id} accessed ${access.org_count} organizations in ${access.time_window} minutes`);
    
    // Send alert to security team
    await sendSecurityAlert({
      type: 'suspicious_tenant_access',
      user_id: access.user_id,
      details: access,
      timestamp: new Date().toISOString()
    });
  }
  
  // Check for unusual access patterns
  const { data: failedAccess } = await supabase
    .from('tenant_access_logs')
    .select('user_id, action, resource_type, client_ip, count(*)')
    .eq('success', false)
    .gt('timestamp', new Date(Date.now() - 3600000).toISOString()) // Last hour
    .group('user_id, action, resource_type, client_ip')
    .having('count(*)', 'gt', 5);
  
  for (const access of failedAccess) {
    context.log.warn(`Multiple failed tenant access attempts: User ${access.user_id} from IP ${access.client_ip}`);
    
    await sendSecurityAlert({
      type: 'failed_tenant_access',
      user_id: access.user_id,
      client_ip: access.client_ip,
      count: access.count,
      timestamp: new Date().toISOString()
    });
  }
};

async function sendSecurityAlert(alert) {
  // Send to security monitoring system
  // This could be Azure Security Center, a SIEM, or other monitoring tool
}

export default timerTrigger;
```

## Compliance & Audit

### Tenant Access Audit Logs

Maintain comprehensive audit logs for tenant access:

```typescript
// Service to access tenant audit logs
export class TenantAuditService {
  async getTenantAccessLogs(organizationId: string, options: {
    startDate?: Date;
    endDate?: Date;
    userId?: string;
    resourceType?: string;
    action?: string;
    limit?: number;
    offset?: number;
  } = {}) {
    const {
      startDate,
      endDate = new Date(),
      userId,
      resourceType,
      action,
      limit = 100,
      offset = 0
    } = options;
    
    let query = supabase
      .from('tenant_access_logs')
      .select('*')
      .eq('organization_id', organizationId)
      .order('timestamp', { ascending: false })
      .range(offset, offset + limit - 1);
    
    if (startDate) {
      query = query.gte('timestamp', startDate.toISOString());
    }
    
    query = query.lte('timestamp', endDate.toISOString());
    
    if (userId) {
      query = query.eq('user_id', userId);
    }
    
    if (resourceType) {
      query = query.eq('resource_type', resourceType);
    }
    
    if (action) {
      query = query.eq('action', action);
    }
    
    const { data, error, count } = await query;
    
    if (error) throw error;
    
    return { logs: data, count };
  }
  
  async getAccessLogDetails(logId: string) {
    const { data, error } = await supabase
      .from('tenant_access_logs')
      .select(`
        *,
        user:user_id(email, name),
        organization:organization_id(name)
      `)
      .eq('id', logId)
      .single();
    
    if (error) throw error;
    return data;
  }
}
```

### Compliance Reports

Generate tenant isolation compliance reports:

```typescript
// Service to generate compliance reports
export class ComplianceReportService {
  async generateTenantIsolationReport(organizationId: string) {
    // Get organization details
    const { data: organization, error: orgError } = await supabase
      .from('organizations')
      .select('*')
      .eq('id', organizationId)
      .single();
    
    if (orgError) throw orgError;
    
    // Get access statistics
    const { data: accessStats } = await supabase.rpc(
      'get_tenant_access_statistics',
      { org_id: organizationId }
    );
    
    // Get failed access attempts
    const { data: failedAccess } = await supabase
      .from('tenant_access_logs')
      .select('*')
      .eq('organization_id', organizationId)
      .eq('success', false)
      .order('timestamp', { ascending: false })
      .limit(100);
    
    // Get cross-tenant access attempts
    const { data: crossTenantAccess } = await supabase.rpc(
      'detect_cross_tenant_access',
      { org_id: organizationId }
    );
    
    return {
      organization,
      reportGenerated: new Date().toISOString(),
      accessStatistics: accessStats,
      failedAccessAttempts: failedAccess,
      crossTenantAccessAttempts: crossTenantAccess,
      complianceStatus: crossTenantAccess.length === 0 ? 'compliant' : 'violations-detected'
    };
  }
}
```

## Disaster Recovery & Data Protection

### Tenant Data Separation

Implement tenant-specific data backup and recovery:

```typescript
// Tenant-specific backup service
export class TenantBackupService {
  async backupTenantData(organizationId: string) {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const backupId = `backup-${organizationId}-${timestamp}`;
    
    // Start a transaction to ensure consistent backup
    const { data: backup, error } = await supabase.rpc(
      'create_tenant_backup',
      {
        org_id: organizationId,
        backup_id: backupId
      }
    );
    
    if (error) throw error;
    
    // Log backup operation
    await supabase
      .from('tenant_backup_logs')
      .insert({
        organization_id: organizationId,
        backup_id: backupId,
        backup_time: new Date().toISOString(),
        status: 'completed',
        size_bytes: backup.size_bytes
      });
    
    return {
      backupId,
      timestamp: new Date().toISOString(),
      size: backup.size_bytes
    };
  }
  
  async restoreTenantData(organizationId: string, backupId: string) {
    // Verify backup belongs to this organization
    const { data: backupLog, error: backupError } = await supabase
      .from('tenant_backup_logs')
      .select('*')
      .eq('organization_id', organizationId)
      .eq('backup_id', backupId)
      .single();
    
    if (backupError || !backupLog) {
      throw new Error('Backup not found or does not belong to this organization');
    }
    
    // Restore data from backup
    const { data: restore, error } = await supabase.rpc(
      'restore_tenant_backup',
      {
        org_id: organizationId,
        backup_id: backupId
      }
    );
    
    if (error) throw error;
    
    // Log restore operation
    await supabase
      .from('tenant_restore_logs')
      .insert({
        organization_id: organizationId,
        backup_id: backupId,
        restore_time: new Date().toISOString(),
        status: 'completed',
        restored_items: restore.item_count
      });
    
    return {
      backupId,
      restoreTimestamp: new Date().toISOString(),
      restoredItems: restore.item_count
    };
  }
}
```

### Tenant Data Export

Implement data portability for tenant data:

```typescript
// Tenant data export service
export class TenantDataExportService {
  async exportTenantData(organizationId: string, options: {
    includeUsers?: boolean;
    includeTournaments?: boolean;
    includeScores?: boolean;
    format?: 'json' | 'csv';
  } = {}) {
    const {
      includeUsers = true,
      includeTournaments = true,
      includeScores = true,
      format = 'json'
    } = options;
    
    // Start export process
    const exportId = `export-${organizationId}-${Date.now()}`;
    
    // Create export record
    await supabase
      .from('tenant_exports')
      .insert({
        id: exportId,
        organization_id: organizationId,
        status: 'in_progress',
        options: { includeUsers, includeTournaments, includeScores, format },
        created_at: new Date().toISOString()
      });
    
    try {
      // Export organization data
      const exportData: any = {
        organization: null,
        users: [],
        tournaments: []
      };
      
      // Export organization details
      const { data: organization } = await supabase
        .from('organizations')
        .select('*')
        .eq('id', organizationId)
        .single();
      
      exportData.organization = organization;
      
      // Export users if requested
      if (includeUsers) {
        const { data: members } = await supabase
          .from('organization_members')
          .select(`
            *,
            user:user_id(id, email, name, created_at)
          `)
          .eq('organization_id', organizationId);
        
        exportData.users = members.map(m => ({
          ...m.user,
          role: m.role
        }));
      }
      
      // Export tournaments if requested
      if (includeTournaments) {
        const { data: tournaments } = await supabase
          .from('tournaments')
          .select('*')
          .eq('organization_id', organizationId);
        
        exportData.tournaments = tournaments;
        
        // Export scores if requested
        if (includeScores && tournaments.length > 0) {
          // For each tournament
          for (const tournament of exportData.tournaments) {
            const { data: rounds } = await supabase
              .from('tournament_rounds')
              .select('*')
              .eq('tournament_id', tournament.id);
            
            tournament.rounds = rounds;
            
            const { data: players } = await supabase
              .from('tournament_players')
              .select('*')
              .eq('tournament_id', tournament.id);
            
            tournament.players = players;
            
            if (rounds.length > 0 && players.length > 0) {
              const { data: scores } = await supabase
                .from('scores')
                .select('*')
                .eq('tournament_id', tournament.id);
              
              tournament.scores = scores;
            }
          }
        }
      }
      
      // Generate export file
      let exportContent: string;
      if (format === 'json') {
        exportContent = JSON.stringify(exportData, null, 2);
      } else {
        // Convert to CSV format (implementation simplified)
        exportContent = convertToCSV(exportData);
      }
      
      // Store export file
      const filename = `${exportId}.${format}`;
      const { error: uploadError } = await supabase.storage
        .from('tenant-exports')
        .upload(filename, exportContent);
      
      if (uploadError) throw uploadError;
      
      // Update export record
      await supabase
        .from('tenant_exports')
        .update({
          status: 'completed',
          completed_at: new Date().toISOString(),
          file_name: filename,
          file_size_bytes: exportContent.length
        })
        .eq('id', exportId);
      
      return {
        exportId,
        status: 'completed',
        fileName: filename,
        downloadUrl: `${process.env.STORAGE_URL}/tenant-exports/${filename}`
      };
    } catch (error) {
      // Update export record with error
      await supabase
        .from('tenant_exports')
        .update({
          status: 'failed',
          error_message: error.message
        })
        .eq('id', exportId);
      
      throw error;
    }
  }
}
```

## Conclusion

Our Multi-Tenancy Isolation Strategy provides a comprehensive approach to ensuring proper data isolation between tenants in the Golf Tournament Organizer application. Through a combination of database-level isolation using Row-Level Security, application-level validation, and comprehensive testing and monitoring, we ensure that tenant data remains secure and private.

The virtual multi-tenancy model allows us to efficiently use shared infrastructure while maintaining strict logical separation. Regular testing, auditing, and monitoring ensure that our tenant isolation mechanisms remain effective as the application evolves.

This strategy supports our compliance requirements and provides peace of mind to our customers that their tournament data is securely isolated from other organizations using our platform.
