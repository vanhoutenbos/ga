# State Management Strategy

This document outlines the state management approach used in the Golf Tournament Organizer application, with particular focus on React state management patterns and how they integrate with Supabase realtime updates.

## State Management Architecture

Our application uses a hybrid state management approach that combines several patterns to effectively handle different types of state:

1. **Local Component State**: For UI-specific and ephemeral state
2. **React Context**: For shared state across component trees
3. **Custom Hooks**: For encapsulating state logic and data fetching
4. **SWR**: For remote data fetching, caching, and synchronization
5. **Supabase Realtime**: For live updates across clients

## State Categories

We categorize our application state into these distinct types:

| State Type | Description | Management Approach | Examples |
|------------|-------------|---------------------|----------|
| **UI State** | Visual state that doesn't affect data | Local state, Context | Modal visibility, form validity, active tabs |
| **Session State** | User session information | Auth Context | Current user, permissions, authentication status |
| **Entity State** | Core business data | SWR + Supabase | Tournaments, scores, player information |
| **Derived State** | Computed from other state | Custom hooks | Leaderboard rankings, player statistics |
| **Application State** | Global app configuration | App Context | Theme, language, notifications |
| **Navigation State** | Current view/routing info | React Router | Current page, URL parameters |
| **Network State** | API and connection status | Network Context | Online/offline status, request states |

## Core State Management Components

### 1. Auth Context

Manages user authentication state and session information:

```typescript
// src/contexts/AuthContext.tsx
export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Initialize auth state from Supabase
    const { data: { subscription } } = supabase.auth.onAuthStateChange(
      (_event, session) => {
        setSession(session);
        setUser(session?.user ?? null);
        setLoading(false);
      }
    );

    return () => subscription.unsubscribe();
  }, []);

  // Expose auth methods and state
  const value = {
    session,
    user,
    loading,
    signIn: (email: string, password: string) => 
      supabase.auth.signInWithPassword({ email, password }),
    signOut: () => supabase.auth.signOut(),
    // Additional auth methods...
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
```

### 2. Tournament Context

Manages active tournament state and realtime updates:

```typescript
// src/contexts/TournamentContext.tsx
export const TournamentContext = createContext<TournamentContextType | undefined>(undefined);

export const TournamentProvider: React.FC<PropsWithChildren<{ tournamentId: string }>> = ({ 
  children, 
  tournamentId 
}) => {
  const [tournament, setTournament] = useState<Tournament | null>(null);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [scores, setScores] = useState<Score[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  // Initial data fetch
  useEffect(() => {
    const fetchTournamentData = async () => {
      try {
        const [tournamentData, participantsData, scoresData] = await Promise.all([
          fetchTournamentDetails(tournamentId),
          fetchTournamentParticipants(tournamentId),
          fetchTournamentScores(tournamentId)
        ]);

        setTournament(tournamentData);
        setParticipants(participantsData);
        setScores(scoresData);
      } catch (err) {
        setError(err as Error);
      } finally {
        setLoading(false);
      }
    };

    fetchTournamentData();
  }, [tournamentId]);

  // Setup realtime subscriptions
  useEffect(() => {
    if (!tournamentId) return;

    // Subscribe to tournament updates
    const tournamentSubscription = supabase
      .channel(`tournament-${tournamentId}`)
      .on('postgres_changes', {
        event: 'UPDATE',
        schema: 'public',
        table: 'tournaments',
        filter: `id=eq.${tournamentId}`
      }, (payload) => {
        setTournament(prev => ({ ...prev, ...payload.new }));
      })
      .subscribe();

    // Subscribe to scores updates
    const scoresSubscription = supabase
      .channel(`tournament-scores-${tournamentId}`)
      .on('postgres_changes', {
        event: '*',
        schema: 'public',
        table: 'scores',
        filter: `tournament_id=eq.${tournamentId}`
      }, (payload) => {
        if (payload.eventType === 'INSERT') {
          setScores(prev => [...prev, payload.new]);
        } else if (payload.eventType === 'UPDATE') {
          setScores(prev => prev.map(score => 
            score.id === payload.new.id ? payload.new : score
          ));
        } else if (payload.eventType === 'DELETE') {
          setScores(prev => prev.filter(score => score.id !== payload.old.id));
        }
      })
      .subscribe();

    return () => {
      tournamentSubscription.unsubscribe();
      scoresSubscription.unsubscribe();
    };
  }, [tournamentId]);

  // Context value
  const value = {
    tournament,
    participants,
    scores,
    loading,
    error,
    // Helper methods for tournament actions
    updateTournament,
    addParticipant,
    updateScore,
    // ...other methods
  };

  return (
    <TournamentContext.Provider value={value}>
      {children}
    </TournamentContext.Provider>
  );
};
```

### 3. Network Context

Manages online/offline status and request handling:

```typescript
// src/contexts/NetworkContext.tsx
export const NetworkContext = createContext<NetworkContextType | undefined>(undefined);

export const NetworkProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [pendingRequests, setPendingRequests] = useState<PendingRequest[]>([]);

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  // Process pending requests when coming back online
  useEffect(() => {
    if (isOnline && pendingRequests.length > 0) {
      processPendingRequests();
    }
  }, [isOnline, pendingRequests]);

  const enqueueRequest = (request: PendingRequest) => {
    setPendingRequests(prev => [...prev, request]);
    // Store in IndexedDB for persistence
    storePendingRequestInDB(request);
  };

  const processPendingRequests = async () => {
    // Process requests in order
    for (const request of pendingRequests) {
      try {
        await executeRequest(request);
        // Remove from queue
        setPendingRequests(prev => 
          prev.filter(r => r.id !== request.id)
        );
        // Remove from IndexedDB
        removePendingRequestFromDB(request.id);
      } catch (error) {
        console.error('Failed to process request:', error);
        // Leave in queue for retry
      }
    }
  };

  const value = {
    isOnline,
    pendingRequests,
    enqueueRequest,
    // Additional network methods...
  };

  return (
    <NetworkContext.Provider value={value}>
      {children}
    </NetworkContext.Provider>
  );
};
```

## Custom Hooks for State Management

We encapsulate state logic in custom hooks to make it reusable and testable:

### Data Fetching with SWR and Supabase

```typescript
// src/hooks/useTournamentData.ts
export const useTournamentData = (tournamentId: string) => {
  const { isOnline } = useNetwork();
  const { user } = useAuth();

  // SWR hook for data fetching, caching, revalidation
  const { data, error, mutate } = useSWR(
    tournamentId ? `tournaments/${tournamentId}` : null,
    () => fetchTournamentDetails(tournamentId),
    {
      // Use stale data when offline
      revalidateIfStale: isOnline,
      revalidateOnFocus: isOnline,
      revalidateOnReconnect: true,
      // Cache configuration
      dedupingInterval: 10000,
    }
  );

  // Set up realtime subscription
  useEffect(() => {
    if (!tournamentId || !isOnline || !user) return;

    const subscription = supabase
      .channel(`tournament-${tournamentId}`)
      .on('postgres_changes', {
        event: 'UPDATE',
        schema: 'public',
        table: 'tournaments',
        filter: `id=eq.${tournamentId}`
      }, (payload) => {
        // Update SWR cache with realtime data
        mutate((currentData) => {
          if (!currentData) return payload.new;
          return { ...currentData, ...payload.new };
        }, false); // Don't revalidate from API after mutation
      })
      .subscribe();

    return () => {
      subscription.unsubscribe();
    };
  }, [tournamentId, isOnline, user, mutate]);

  return {
    tournament: data,
    isLoading: !error && !data,
    isError: !!error,
    mutate,
  };
};
```

### State Synchronization Hook

```typescript
// src/hooks/useRealtimeSync.ts
export const useRealtimeSync = <T extends Record<string, any>>(
  table: string,
  primaryKey: string,
  initialData: T[] = [],
  filter?: { column: string, value: string }
) => {
  const [data, setData] = useState<T[]>(initialData);
  const { isOnline } = useNetwork();

  // Set up realtime subscription
  useEffect(() => {
    if (!isOnline) return;

    let channel = supabase.channel(`${table}-changes`);
    
    // Build filter conditions
    let filterCondition = {};
    if (filter) {
      filterCondition = { filter: `${filter.column}=eq.${filter.value}` };
    }

    // Subscribe to all change types
    channel = channel
      .on('postgres_changes', {
        event: 'INSERT',
        schema: 'public',
        table,
        ...filterCondition
      }, (payload) => {
        setData(prev => [...prev, payload.new as T]);
      })
      .on('postgres_changes', {
        event: 'UPDATE',
        schema: 'public',
        table,
        ...filterCondition
      }, (payload) => {
        setData(prev => prev.map(item => 
          item[primaryKey] === payload.new[primaryKey] ? payload.new : item
        ));
      })
      .on('postgres_changes', {
        event: 'DELETE',
        schema: 'public',
        table,
        ...filterCondition
      }, (payload) => {
        setData(prev => prev.filter(item => 
          item[primaryKey] !== payload.old[primaryKey]
        ));
      })
      .subscribe();

    return () => {
      channel.unsubscribe();
    };
  }, [table, primaryKey, filter, isOnline]);

  return {
    data,
    setData,
    // Utility functions for optimistic updates
    addItem: (newItem: T) => {
      setData(prev => [...prev, newItem]);
    },
    updateItem: (updatedItem: Partial<T> & { [key: string]: any }) => {
      setData(prev => prev.map(item => 
        item[primaryKey] === updatedItem[primaryKey] 
          ? { ...item, ...updatedItem } 
          : item
      ));
    },
    removeItem: (itemId: string | number) => {
      setData(prev => prev.filter(item => 
        item[primaryKey] !== itemId
      ));
    }
  };
};
```

## Implementing Optimistic Updates

For better user experience, we implement optimistic updates with rollback capabilities:

```typescript
// src/hooks/useOptimisticMutation.ts
export const useOptimisticMutation = <T, U = unknown>(
  mutationFn: (data: U) => Promise<T>,
  options: {
    onMutate?: (data: U) => void;
    onSuccess?: (result: T, data: U) => void;
    onError?: (error: Error, data: U) => void;
    onSettled?: (error: Error | null, result: T | null) => void;
  } = {}
) => {
  const [isLoading, setIsLoading] = useState(false);
  const [isError, setIsError] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { enqueueRequest } = useNetwork();

  const mutate = async (data: U) => {
    setIsLoading(true);
    setIsError(false);
    setError(null);
    
    try {
      // Call pre-mutation hook
      if (options.onMutate) {
        options.onMutate(data);
      }

      // Perform the mutation
      const result = await mutationFn(data);
      
      // Call success hook
      if (options.onSuccess) {
        options.onSuccess(result, data);
      }
      
      return result;
    } catch (err) {
      setIsError(true);
      setError(err as Error);
      
      // Handle offline scenario
      const { isOnline } = getNetworkStatus();
      if (!isOnline) {
        // Queue for later execution
        enqueueRequest({
          id: uuidv4(),
          timestamp: new Date().toISOString(),
          type: 'mutation',
          data,
          mutationFn: mutationFn.toString(),
        });
      }
      
      // Call error hook
      if (options.onError) {
        options.onError(err as Error, data);
      }
      
      throw err;
    } finally {
      setIsLoading(false);
      
      // Call settled hook
      if (options.onSettled) {
        options.onSettled(error, error ? null : {} as T);
      }
    }
  };

  return {
    mutate,
    isLoading,
    isError,
    error,
  };
};
```

Example Usage:

```typescript
// src/features/tournaments/hooks/useUpdateScore.ts
export const useUpdateScore = (tournamentId: string) => {
  const { mutate: updateScoreData } = useSWR(`tournaments/${tournamentId}/scores`);
  
  return useOptimisticMutation(
    // Mutation function
    async (scoreData: ScoreUpdate) => {
      const { data, error } = await supabase
        .from('scores')
        .update({
          strokes: scoreData.strokes,
          updated_at: new Date().toISOString(),
        })
        .eq('id', scoreData.id)
        .select()
        .single();
        
      if (error) throw error;
      return data;
    },
    // Options
    {
      // Optimistically update UI
      onMutate: (scoreData) => {
        // Update local state optimistically
        updateScoreData((currentData) => {
          if (!currentData) return currentData;
          return currentData.map(score => 
            score.id === scoreData.id 
              ? { ...score, strokes: scoreData.strokes } 
              : score
          );
        }, false);
      },
      // Handle errors
      onError: (error, scoreData) => {
        console.error('Failed to update score:', error);
        // Revert optimistic update
        updateScoreData(undefined, true);
      }
    }
  );
};
```

## Offline-First State Management

For offline functionality, we integrate IndexedDB for persistent state:

```typescript
// src/hooks/useOfflineStorage.ts
export const useOfflineStorage = <T extends Record<string, any>>(
  storeName: string,
  primaryKey: string = 'id'
) => {
  // Initialize Dexie.js database
  const db = useMemo(() => {
    const database = new Dexie('GolfAppOfflineDB');
    database.version(1).stores({
      [storeName]: primaryKey,
    });
    return database;
  }, [storeName, primaryKey]);

  // Create type-safe table reference
  const table = useMemo(() => {
    return db.table<T>(storeName);
  }, [db, storeName]);

  return {
    // Basic CRUD operations
    getAll: () => table.toArray(),
    getById: (id: string) => table.get(id),
    add: (item: T) => table.add(item),
    update: (id: string, changes: Partial<T>) => table.update(id, changes),
    delete: (id: string) => table.delete(id),
    
    // Bulk operations
    bulkAdd: (items: T[]) => table.bulkAdd(items),
    bulkUpdate: (items: { id: string, changes: Partial<T> }[]) => 
      db.transaction('rw', table, async () => {
        for (const { id, changes } of items) {
          await table.update(id, changes);
        }
      }),
      
    // Sync operations
    syncWithRemote: async (fetchRemoteData: () => Promise<T[]>) => {
      try {
        const remoteData = await fetchRemoteData();
        await table.clear();
        await table.bulkAdd(remoteData);
        return remoteData;
      } catch (error) {
        console.error('Failed to sync with remote:', error);
        return table.toArray();
      }
    }
  };
};
```

## State Management for Tournament Leaderboard

For complex derived state like leaderboards, we use specialized hooks:

```typescript
// src/features/leaderboard/hooks/useLeaderboardData.ts
export const useLeaderboardData = (tournamentId: string) => {
  // Fetch base data
  const { data: tournament } = useTournamentData(tournamentId);
  const { data: scores, isLoading: scoresLoading } = useScores(tournamentId);
  const { data: players, isLoading: playersLoading } = usePlayers(tournamentId);
  
  // Computed leaderboard state
  const leaderboard = useMemo(() => {
    if (!tournament || !scores || !players) return [];
    
    return calculateLeaderboard(tournament, scores, players);
  }, [tournament, scores, players]);
  
  // Realtime updates for scores
  useEffect(() => {
    if (!tournamentId) return;
    
    const subscription = supabase
      .channel(`leaderboard-${tournamentId}`)
      .on('postgres_changes', {
        event: '*',
        schema: 'public',
        table: 'scores',
        filter: `tournament_id=eq.${tournamentId}`
      }, () => {
        // Simply trigger a revalidation of the scores data
        // This will cascade to recalculation of the leaderboard
        revalidateScores();
      })
      .subscribe();
      
    return () => {
      subscription.unsubscribe();
    };
  }, [tournamentId]);
  
  return {
    leaderboard,
    isLoading: scoresLoading || playersLoading,
    tournament,
  };
};
```

## Complex State Management Example: Score Entry

Score entry combines multiple state types and realtime updates:

```typescript
// src/features/scoring/hooks/useScoreEntry.ts
export const useScoreEntry = (tournamentId: string, playerId: string) => {
  // Current score state
  const [currentScores, setCurrentScores] = useState<HoleScore[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  
  // Fetch existing score data if any
  const { data: existingScore, isLoading } = usePlayerScore(tournamentId, playerId);
  const { data: courseData } = useCourseData(tournamentId);
  
  // Tournament permissions check
  const permissions = useTournamentPermissions(tournamentId);
  
  // Network context for offline awareness
  const { isOnline, enqueueRequest } = useNetwork();
  
  // Initialize from existing data
  useEffect(() => {
    if (existingScore && courseData) {
      setCurrentScores(
        existingScore.holeScores || 
        createEmptyScorecard(courseData.holeCount)
      );
    }
  }, [existingScore, courseData]);

  // Validate scores against course par
  const validateScores = useCallback(() => {
    if (!courseData) return {};
    
    const errors: Record<string, string> = {};
    currentScores.forEach((score, index) => {
      const holeNumber = index + 1;
      const par = courseData.holes.find(h => h.number === holeNumber)?.par || 3;
      
      if (score.strokes && (score.strokes < 1 || score.strokes > par + 5)) {
        errors[`hole_${holeNumber}`] = `Score should be between 1 and ${par + 5}`;
      }
    });
    
    return errors;
  }, [currentScores, courseData]);
  
  // Update hole score
  const updateHoleScore = (holeIndex: number, strokes: number) => {
    setCurrentScores(prev => {
      const newScores = [...prev];
      newScores[holeIndex] = { 
        ...newScores[holeIndex],
        strokes,
        updated_at: new Date().toISOString()
      };
      return newScores;
    });
    
    // Clear validation error if it exists
    if (validationErrors[`hole_${holeIndex + 1}`]) {
      setValidationErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[`hole_${holeIndex + 1}`];
        return newErrors;
      });
    }
  };
  
  // Submit score card
  const submitScoreCard = async () => {
    // Validate first
    const errors = validateScores();
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return { success: false, errors };
    }
    
    setIsSubmitting(true);
    
    try {
      // If offline, queue for later
      if (!isOnline) {
        enqueueRequest({
          id: uuidv4(),
          timestamp: new Date().toISOString(),
          type: 'scoreSubmission',
          data: {
            tournamentId,
            playerId,
            scores: currentScores
          }
        });
        
        return { success: true, queued: true };
      }
      
      // Online submission
      const { data, error } = await supabase
        .from('scores')
        .upsert({
          tournament_id: tournamentId,
          player_id: playerId,
          hole_scores: currentScores,
          total_score: calculateTotalScore(currentScores),
          updated_at: new Date().toISOString(),
          submitted_by: permissions.canSubmitScores ? supabase.auth.getUser().then(u => u.data.user?.id) : playerId
        })
        .select();
        
      if (error) throw error;
      return { success: true, data };
      
    } catch (error) {
      console.error('Failed to submit scorecard:', error);
      return { success: false, error };
    } finally {
      setIsSubmitting(false);
    }
  };
  
  return {
    scores: currentScores,
    isLoading,
    isSubmitting,
    validationErrors,
    updateHoleScore,
    submitScoreCard,
    calculateTotal: () => calculateTotalScore(currentScores),
    // Helper stat calculations
    calculateStats: () => calculatePlayerStats(currentScores, courseData),
  };
};
```

## Supabase Realtime Performance Considerations

For optimal performance with Supabase realtime updates:

1. **Selective Subscriptions**: Subscribe only to relevant tables and filter by specific IDs
2. **Batched Updates**: Group UI updates to prevent excessive re-renders
3. **Presence for Active Users**: Use Supabase Presence for showing active scorers

```typescript
// src/hooks/useActiveTournamentUsers.ts
export const useActiveTournamentUsers = (tournamentId: string) => {
  const [activeUsers, setActiveUsers] = useState<ActiveUser[]>([]);
  const { user } = useAuth();

  useEffect(() => {
    if (!tournamentId || !user) return;

    // Create presence channel
    const channel = supabase.channel(`tournament-presence-${tournamentId}`, {
      config: {
        presence: {
          key: user.id,
        },
      },
    });

    // Handle presence state changes
    channel
      .on('presence', { event: 'sync' }, () => {
        const state = channel.presenceState();
        const presentUsers = Object.entries(state).map(([userId, userStates]) => {
          const userData = userStates[0] as unknown as { username: string; role: string; avatar: string };
          return {
            id: userId,
            username: userData.username,
            role: userData.role,
            avatar: userData.avatar,
            lastSeen: new Date(),
          };
        });

        setActiveUsers(presentUsers);
      })
      .subscribe(async (status) => {
        if (status === 'SUBSCRIBED') {
          // Track user's presence
          await channel.track({
            username: user.user_metadata.username || user.email,
            role: user.app_metadata.role || 'player',
            avatar: user.user_metadata.avatar_url,
          });
        }
      });

    return () => {
      channel.unsubscribe();
    };
  }, [tournamentId, user]);

  return activeUsers;
};
```

## Testing State Management

We use a comprehensive testing approach for our state management:

```typescript
// src/features/tournaments/__tests__/tournamentContext.test.tsx
describe('TournamentContext', () => {
  test('provides tournament data to children', async () => {
    // Mock Supabase responses
    mockSupabase.from.mockImplementation((table) => {
      if (table === 'tournaments') {
        return {
          select: jest.fn().mockReturnValue({
            eq: jest.fn().mockResolvedValue({
              data: mockTournament,
              error: null,
            }),
          }),
        };
      }
      // Add mocks for other tables
      return { /* ... */ };
    });

    // Render with context
    const { result, waitForNextUpdate } = renderHook(() => useTournamentContext(), {
      wrapper: ({ children }) => (
        <TournamentProvider tournamentId="123">
          {children}
        </TournamentProvider>
      ),
    });

    // Initial loading state
    expect(result.current.loading).toBe(true);

    // Wait for data fetching
    await waitForNextUpdate();

    // Verify data
    expect(result.current.loading).toBe(false);
    expect(result.current.tournament).toEqual(mockTournament);
  });

  test('updates state on realtime events', async () => {
    // Setup and initial render
    // ...

    // Trigger a realtime event
    mockSupabaseChannel.emit('postgres_changes', {
      eventType: 'UPDATE',
      new: { ...mockTournament, name: 'Updated Tournament' },
      old: mockTournament,
    });

    // Verify state was updated
    expect(result.current.tournament.name).toBe('Updated Tournament');
  });
});
```

## Performance Optimizations

1. **Selective Rendering with React.memo**:
```tsx
const ScoreCard = React.memo(({ scores }: ScoreCardProps) => {
  // Component implementation
}, (prevProps, nextProps) => {
  // Custom comparison to reduce unnecessary re-renders
  return prevProps.scores.every((score, idx) => 
    score.strokes === nextProps.scores[idx].strokes
  );
});
```

2. **Careful Dependency Management**:
```tsx
// Only recalculate when dependencies actually change
const sortedLeaderboard = useMemo(() => {
  return scores
    .map(calculatePlayerScore)
    .sort((a, b) => a.totalScore - b.totalScore);
}, [scores]); // Only recalculate when scores change
```

3. **Debounced Updates**:
```tsx
// Debounce leaderboard updates to prevent excessive re-renders
const debouncedUpdateLeaderboard = useCallback(
  debounce((newScores: Score[]) => {
    setLeaderboard(calculateLeaderboard(newScores));
  }, 500),
  []
);
```

## Conclusion

Our state management approach combines React's built-in state management with custom hooks, SWR for data fetching, and Supabase's realtime capabilities to create a responsive and offline-capable application. By categorizing different types of state and applying appropriate patterns to each, we maintain a clean architecture that scales with application complexity while keeping performance optimal.
