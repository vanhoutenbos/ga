# Access Control Implementation

This document details the implementation of role-based access control (RBAC) in the Golf Tournament Organizer application, with specific focus on Supabase Row Level Security (RLS) policies and their integration with application-level permission checks.

## Access Control Model

The Golf Tournament Organizer uses a multi-layered access control approach:

1. **Authentication**: Supabase Auth for identity verification
2. **Authorization**: Combination of Supabase RLS and application-level permission checks
3. **Role Assignment**: Dynamic role assignment based on user relationships to tournaments
4. **Token-based Permissions**: JWT claims for efficient permission verification

## User Roles

The application defines the following roles with hierarchical permissions:

| Role | Description | Scope | Example Actions |
|------|-------------|-------|----------------|
| **System Admin** | Platform administrators | Global | Manage all tournaments, users, settings |
| **Club Admin** | Administrators of a golf club | Club-level | Manage club tournaments, course settings |
| **Tournament Organizer** | Creator or designated organizer | Tournament-level | Configure tournament, manage players |
| **Tournament Assistant** | Helper appointed by organizer | Tournament-level | Help with check-ins, verify scores |
| **Player** | Participant in tournament | Self + assigned groups | Submit scores, view leaderboard |
| **Spectator** | Read-only access to tournament | View-only | View leaderboard, follow specific players |
| **Anonymous** | Unauthenticated user | Public data only | View public tournament details |

## Supabase Row Level Security (RLS) Implementation

### Tournament Table RLS Policies

```sql
-- Allow users to see tournaments they're involved with
CREATE POLICY "Users can view tournaments they're involved with" ON tournaments
  FOR SELECT
  USING (
    auth.uid() IN (
      SELECT user_id FROM tournament_participants WHERE tournament_id = tournaments.id
      UNION
      SELECT created_by FROM tournaments WHERE id = tournaments.id
    )
    OR tournaments.visibility = 'public'
  );

-- Allow tournament creators to update their tournaments
CREATE POLICY "Creators can update tournaments" ON tournaments
  FOR UPDATE
  USING (auth.uid() = tournaments.created_by)
  WITH CHECK (auth.uid() = tournaments.created_by);

-- Allow tournament creators and organizers to insert scores
CREATE POLICY "Organizers can insert scores" ON scores
  FOR INSERT
  WITH CHECK (
    auth.uid() IN (
      SELECT created_by FROM tournaments WHERE id = scores.tournament_id
      UNION
      SELECT user_id FROM tournament_organizers WHERE tournament_id = scores.tournament_id
    )
    OR (
      -- Players can insert their own scores when tournament allows
      auth.uid() = scores.player_id AND
      EXISTS (
        SELECT 1 FROM tournaments 
        WHERE id = scores.tournament_id AND self_scoring_enabled = true
      )
    )
  );
```

### Tournament Participants Table RLS Policies

```sql
-- View participants of tournaments you're involved in
CREATE POLICY "View participants of your tournaments" ON tournament_participants
  FOR SELECT
  USING (
    tournament_id IN (
      SELECT tournament_id FROM tournament_participants WHERE user_id = auth.uid()
      UNION
      SELECT id FROM tournaments WHERE created_by = auth.uid()
    )
  );

-- Only organizers can add participants
CREATE POLICY "Organizers can add participants" ON tournament_participants
  FOR INSERT
  WITH CHECK (
    auth.uid() IN (
      SELECT created_by FROM tournaments WHERE id = tournament_participants.tournament_id
      UNION
      SELECT user_id FROM tournament_organizers 
      WHERE tournament_id = tournament_participants.tournament_id AND can_manage_players = true
    )
  );
```

### Score Table RLS Policies

```sql
-- View scores for tournaments you're involved in
CREATE POLICY "View scores for your tournaments" ON scores
  FOR SELECT
  USING (
    tournament_id IN (
      SELECT tournament_id FROM tournament_participants WHERE user_id = auth.uid()
      UNION
      SELECT id FROM tournaments WHERE created_by = auth.uid()
      UNION
      SELECT tournament_id FROM tournament_spectators WHERE user_id = auth.uid()
    )
  );

-- Players can update their own scores if self-scoring is enabled
CREATE POLICY "Players can update own scores" ON scores
  FOR UPDATE
  USING (
    auth.uid() = player_id AND
    EXISTS (
      SELECT 1 FROM tournaments 
      WHERE id = tournament_id AND self_scoring_enabled = true
    )
  )
  WITH CHECK (
    auth.uid() = player_id AND
    EXISTS (
      SELECT 1 FROM tournaments 
      WHERE id = tournament_id AND self_scoring_enabled = true
    )
  );

-- Organizers can update any scores
CREATE POLICY "Organizers can update scores" ON scores
  FOR UPDATE
  USING (
    auth.uid() IN (
      SELECT created_by FROM tournaments WHERE id = tournament_id
      UNION
      SELECT user_id FROM tournament_organizers 
      WHERE tournament_id = scores.tournament_id AND can_manage_scores = true
    )
  )
  WITH CHECK (
    auth.uid() IN (
      SELECT created_by FROM tournaments WHERE id = tournament_id
      UNION
      SELECT user_id FROM tournament_organizers 
      WHERE tournament_id = scores.tournament_id AND can_manage_scores = true
    )
  );
```

## Role Assignment Implementation

Roles are assigned dynamically based on user relationships to tournaments:

```typescript
// src/features/tournaments/hooks/useTournamentRole.ts
export const useTournamentRole = (tournamentId: string): TournamentRole => {
  const { user } = useAuth();
  const { data: tournamentData } = useTournamentDetails(tournamentId);
  const { data: participantData } = useTournamentParticipants(tournamentId);
  const { data: organizerData } = useTournamentOrganizers(tournamentId);
  
  if (!user) return 'anonymous';
  
  // System admin check
  if (user.app_metadata?.role === 'admin') return 'system_admin';
  
  // Club admin check
  if (tournamentData && user.app_metadata?.club_admin_ids?.includes(tournamentData.club_id)) {
    return 'club_admin';
  }
  
  // Tournament creator check
  if (tournamentData && tournamentData.created_by === user.id) {
    return 'tournament_organizer';
  }
  
  // Tournament organizer check
  if (organizerData?.find(org => org.user_id === user.id)) {
    const organizer = organizerData.find(org => org.user_id === user.id);
    return organizer.is_assistant ? 'tournament_assistant' : 'tournament_organizer';
  }
  
  // Player check
  if (participantData?.find(p => p.user_id === user.id && p.role === 'player')) {
    return 'player';
  }
  
  // Spectator check
  if (participantData?.find(p => p.user_id === user.id && p.role === 'spectator')) {
    return 'spectator';
  }
  
  return tournamentData?.visibility === 'public' ? 'spectator' : 'anonymous';
};
```

## Permission Checking Implementation

Permissions are checked at both the database and application layers:

```typescript
// src/features/tournaments/hooks/useTournamentPermissions.ts
export const useTournamentPermissions = (tournamentId: string) => {
  const role = useTournamentRole(tournamentId);
  const { data: tournamentData } = useTournamentDetails(tournamentId);
  
  return {
    canViewTournament: ['system_admin', 'club_admin', 'tournament_organizer', 
                        'tournament_assistant', 'player', 'spectator'].includes(role) ||
                        tournamentData?.visibility === 'public',
    
    canEditTournament: ['system_admin', 'club_admin', 'tournament_organizer'].includes(role),
    
    canManageParticipants: ['system_admin', 'club_admin', 'tournament_organizer'].includes(role),
    
    canSubmitScores: ['system_admin', 'club_admin', 'tournament_organizer', 'tournament_assistant'].includes(role) ||
                     (role === 'player' && tournamentData?.self_scoring_enabled),
    
    canVerifyScores: ['system_admin', 'club_admin', 'tournament_organizer', 'tournament_assistant'].includes(role),
    
    canManageOrganizers: ['system_admin', 'club_admin', 'tournament_organizer'].includes(role),
    
    canExportData: ['system_admin', 'club_admin', 'tournament_organizer'].includes(role),
    
    // Additional permissions...
  };
};
```

## Application-level Access Control

Components use the permission hooks to determine what actions to enable:

```tsx
// src/features/tournaments/components/TournamentActions.tsx
const TournamentActions = ({ tournamentId }: Props) => {
  const permissions = useTournamentPermissions(tournamentId);
  
  return (
    <div className="tournament-actions">
      {permissions.canEditTournament && (
        <Button variant="primary" onClick={() => navigate(`/tournaments/${tournamentId}/edit`)}>
          Edit Tournament
        </Button>
      )}
      
      {permissions.canManageParticipants && (
        <Button variant="secondary" onClick={() => navigate(`/tournaments/${tournamentId}/participants`)}>
          Manage Participants
        </Button>
      )}
      
      {permissions.canSubmitScores && (
        <Button variant="secondary" onClick={() => navigate(`/tournaments/${tournamentId}/scores`)}>
          Submit Scores
        </Button>
      )}
      
      {/* Additional action buttons based on permissions */}
    </div>
  );
};
```

## JWT Token Claims for Performance

To optimize permission checks, relevant role information is stored in JWT claims:

```typescript
// src/lib/supabase.ts
export const refreshSessionWithRoles = async () => {
  const { data: { session } } = await supabase.auth.getSession();
  
  if (!session) return null;
  
  // Fetch user roles for active tournaments
  const { data: userRoles } = await supabase
    .from('user_tournament_roles')
    .select('tournament_id, role')
    .eq('user_id', session.user.id);
  
  // Update JWT with role information
  await supabase.auth.refreshSession({
    refresh_token: session.refresh_token,
  });
  
  return session;
};
```

## Access Control in Azure Functions

Azure Functions use the same permission model via JWT validation:

```csharp
// src/api/GolfApp.Api/Auth/TournamentPermissionAttribute.cs
[AttributeUsage(AttributeTargets.Method)]
public class TournamentPermissionAttribute : Attribute, IAsyncFunctionInvocationFilter
{
    private readonly string[] _allowedRoles;
    private readonly string _tournamentIdParameter;

    public TournamentPermissionAttribute(
        string tournamentIdParameter, 
        params string[] allowedRoles)
    {
        _tournamentIdParameter = tournamentIdParameter;
        _allowedRoles = allowedRoles;
    }

    public async Task OnExecutingAsync(FunctionExecutingContext executingContext, 
                                      CancellationToken cancellationToken)
    {
        // Extract JWT claims from request
        var principal = executingContext.GetHttpContext().User;
        var tournamentId = executingContext.Arguments.SingleOrDefault(a => 
                          a.Name == _tournamentIdParameter)?.Value?.ToString();

        if (string.IsNullOrEmpty(tournamentId))
        {
            throw new UnauthorizedAccessException("Tournament ID not provided");
        }

        // Check if user has system admin role
        if (principal.HasClaim(c => c.Type == "app_role" && c.Value == "system_admin"))
        {
            return; // System admins can access everything
        }

        // Check user's role for this specific tournament
        var tournamentRoleClaim = principal.Claims.FirstOrDefault(c => 
            c.Type == "tournament_roles" && c.Value.StartsWith($"{tournamentId}:"));

        if (tournamentRoleClaim == null || 
            !_allowedRoles.Contains(tournamentRoleClaim.Value.Split(':')[1]))
        {
            throw new UnauthorizedAccessException("Insufficient permissions for this tournament");
        }
    }

    public Task OnExecutedAsync(FunctionExecutedContext executedContext, 
                               CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

Usage in Function:

```csharp
[Function(nameof(UpdateTournamentDetails))]
[TournamentPermission("tournamentId", "tournament_organizer", "club_admin", "system_admin")]
public async Task<HttpResponseData> UpdateTournamentDetails(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tournaments/{tournamentId}")] 
    HttpRequestData req,
    string tournamentId)
{
    // Function implementation
    // Permission check is handled by the attribute
}
```

## Handling Complex Permissions and Edge Cases

### Time-limited Permissions

Some permissions are time-limited, particularly for tournaments:

```sql
-- Players can only submit scores during active rounds
CREATE POLICY "Players can submit scores during active rounds" ON hole_scores
  FOR INSERT
  WITH CHECK (
    auth.uid() = player_id AND
    EXISTS (
      SELECT 1 FROM tournament_rounds
      WHERE tournament_id = (SELECT tournament_id FROM scores WHERE id = hole_scores.score_id)
      AND start_time <= NOW()
      AND end_time >= NOW()
    )
  );
```

### Delegate Permissions

Tournament organizers can delegate specific permissions to assistants:

```typescript
// src/features/tournaments/components/OrganizersManagement.tsx
const updateOrganizer = async (organizerId: string, permissions: OrganizerPermissions) => {
  await supabase
    .from('tournament_organizers')
    .update({
      can_manage_players: permissions.canManagePlayers,
      can_manage_scores: permissions.canManageScores,
      can_verify_scores: permissions.canVerifyScores,
      can_manage_schedule: permissions.canManageSchedule,
      updated_at: new Date().toISOString()
    })
    .eq('id', organizerId);
};
```

### Tiered Access to Data

Different roles see different levels of detail:

```typescript
// src/features/scoring/hooks/useScoreboardData.ts
export const useScoreboardData = (tournamentId: string) => {
  const { data: rawData } = useSupabaseQuery(/* ... */);
  const permissions = useTournamentPermissions(tournamentId);
  
  // Filter and transform data based on permissions
  return {
    scores: rawData?.map(score => ({
      ...score,
      // Only show detailed hole-by-hole data to authorized roles
      holeDetails: permissions.canViewDetailedScores ? score.holeDetails : null,
      // Only show notes/comments to tournament staff
      notes: permissions.canViewScoringNotes ? score.notes : null,
    })),
    // Include additional metadata only for organizers
    metadata: permissions.canEditTournament ? {
      submissionStats: { /* ... */ },
      verificationStatus: { /* ... */ },
    } : null
  };
};
```

## Security Considerations

1. **Double Verification**: Critical actions require both RLS and application-level checks
2. **Principle of Least Privilege**: RLS policies grant minimum required access
3. **No Trust in Client**: All client-submitted data is re-validated on the server
4. **Fine-grained Permissions**: Authorization is specific to actions, not just broad roles
5. **Audit Logging**: Permission-related actions are logged for security review

## Testing Access Control

We use a comprehensive testing strategy to validate our access control implementation:

1. **Unit Tests**: For permission calculation logic
2. **Integration Tests**: For RLS policy enforcement
3. **End-to-End Tests**: For complete user journeys with different roles
4. **Penetration Tests**: Regular security testing to identify privilege escalation risks

Example test:

```typescript
// tests/access-control/tournament-permissions.test.ts
describe('Tournament Access Control', () => {
  test('Players cannot modify tournament details', async () => {
    // Setup player role
    await setupUserRole(testUser.id, testTournament.id, 'player');
    
    // Attempt to modify tournament
    const { error } = await supabase
      .from('tournaments')
      .update({ name: 'Changed Name' })
      .eq('id', testTournament.id);
    
    expect(error).not.toBeNull();
    expect(error!.code).toBe('42501'); // PostgreSQL permission denied code
  });
  
  // Additional tests...
});
```

## Role Transition Workflows

The application supports smooth transitions between roles:

1. **Promoting Players to Assistants**: Tournament organizers can promote players to assistant roles
2. **Transferring Ownership**: Transferring primary organizer role to another user
3. **Club Administrator Delegation**: Club admins can assign tournament management permissions

## Conclusion

This multi-layered access control implementation ensures that users can only perform actions appropriate to their role within each tournament context. By combining Supabase RLS with application-level permission checks, we create a robust security model that protects data integrity while providing appropriate access to tournament information.
