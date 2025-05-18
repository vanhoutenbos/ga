# Conflict Resolution Testing Strategy

This document outlines our approach to testing the conflict resolution strategy for the Golf Tournament Organizer application, with a focus on ensuring that the system handles data conflicts appropriately across various scenarios.

## Testing Objectives

1. **Validate Core Resolution Logic**
   - Verify that the "last edit wins" approach works as expected in typical scenarios
   - Confirm that timestamp-based resolution consistently selects the correct version
   - Test that metadata (timestamps, client IDs, user roles) is properly captured and used

2. **Verify Edge Case Handling**
   - Test all special conflict resolution scenarios documented in our strategy
   - Ensure that scorer priority rules are correctly applied
   - Validate field-level merging for partial updates

3. **Stress Test Offline Scenarios**
   - Simulate prolonged offline periods with substantial local changes
   - Test synchronization with varying network conditions
   - Verify behavior with large volumes of pending changes

## Testing Approaches

### 1. Unit Testing

Unit tests focus on isolated components of the conflict resolution system:

```typescript
// Example: Testing last-edit-wins resolver
describe('LastEditWinsResolver', () => {
  test('should select newer local record when local is newer', () => {
    const localRecord = createTestRecord({
      id: '123',
      value: 'local value',
      updated_at: new Date(2023, 5, 2, 10, 0, 0).toISOString()
    });
    
    const serverRecord = createTestRecord({
      id: '123',
      value: 'server value',
      updated_at: new Date(2023, 5, 2, 9, 0, 0).toISOString()
    });
    
    const result = lastEditWinsResolver.resolve(localRecord, serverRecord);
    expect(result.value).toBe('local value');
    expect(result.resolution).toBe('LOCAL_NEWER');
  });
  
  test('should select newer server record when server is newer', () => {
    const localRecord = createTestRecord({
      id: '123',
      value: 'local value',
      updated_at: new Date(2023, 5, 2, 9, 0, 0).toISOString()
    });
    
    const serverRecord = createTestRecord({
      id: '123',
      value: 'server value',
      updated_at: new Date(2023, 5, 2, 10, 0, 0).toISOString()
    });
    
    const result = lastEditWinsResolver.resolve(localRecord, serverRecord);
    expect(result.value).toBe('server value');
    expect(result.resolution).toBe('SERVER_NEWER');
  });
  
  test('official scorer changes win regardless of timestamp', () => {
    const localRecord = createTestRecord({
      id: '123',
      value: 'local value',
      updated_at: new Date(2023, 5, 2, 10, 0, 0).toISOString(),
      is_official_score: false
    });
    
    const serverRecord = createTestRecord({
      id: '123',
      value: 'server value',
      updated_at: new Date(2023, 5, 2, 9, 0, 0).toISOString(),
      is_official_score: true
    });
    
    const result = lastEditWinsResolver.resolve(localRecord, serverRecord);
    expect(result.value).toBe('server value');
    expect(result.resolution).toBe('OFFICIAL_SCORER_OVERRIDE');
  });
});
```

### 2. Integration Testing

Integration tests validate the interaction between components:

```typescript
describe('Sync Process Integration', () => {
  let localDb;
  let mockSupabase;
  let syncService;
  
  beforeEach(async () => {
    // Set up test databases and services
    localDb = await setupTestIndexedDb();
    mockSupabase = createMockSupabaseClient();
    syncService = new SyncService(localDb, mockSupabase);
  });
  
  test('should sync local changes to server and apply server changes', async () => {
    // Prepare test data
    const localScores = [
      createTestScore({ id: '1', value: 3, updated_at: new Date(2023, 5, 2, 10, 0).toISOString() }),
      createTestScore({ id: '2', value: 4, updated_at: new Date(2023, 5, 2, 11, 0).toISOString() })
    ];
    
    const serverScores = [
      createTestScore({ id: '1', value: 5, updated_at: new Date(2023, 5, 2, 11, 30).toISOString() }),
      createTestScore({ id: '3', value: 2, updated_at: new Date(2023, 5, 2, 9, 0).toISOString() })
    ];
    
    // Set up test scenario
    await localDb.scores.bulkAdd(localScores);
    mockSupabase.mockServerData('scores', serverScores);
    
    // Execute sync
    await syncService.performSync();
    
    // Verify results
    const finalLocalScores = await localDb.scores.toArray();
    expect(finalLocalScores).toHaveLength(3); // IDs 1, 2, and 3
    
    // ID 1: Server should win (newer)
    const score1 = finalLocalScores.find(s => s.id === '1');
    expect(score1.value).toBe(5);
    
    // ID 2: Local should win (server didn't have it)
    const score2 = finalLocalScores.find(s => s.id === '2');
    expect(score2.value).toBe(4);
    
    // ID 3: Server should be added to local
    const score3 = finalLocalScores.find(s => s.id === '3');
    expect(score3.value).toBe(2);
    
    // Verify server calls
    expect(mockSupabase.upsertCalls).toHaveLength(1);
    expect(mockSupabase.upsertCalls[0].records).toContainEqual(expect.objectContaining({ id: '2', value: 4 }));
  });
});
```

### 3. End-to-End Testing

End-to-end tests simulate real-world scenarios in a complete environment:

```typescript
describe('Offline Sync E2E', () => {
  test('should handle offline scoring and reconnection correctly', async () => {
    // Load test tournament
    await page.goto('/tournaments/test-tournament');
    await page.waitForSelector('.loaded-tournament');
    
    // Put app in offline mode
    await page.evaluate(() => {
      window.goOffline(); // Mock function to simulate offline mode
    });
    
    // Make offline changes
    await page.click('.player-score-card');
    await page.fill('input[name="hole3Score"]', '4');
    await page.click('.save-score-button');
    
    // Verify local save indicator
    await expect(page.locator('.offline-save-indicator')).toBeVisible();
    
    // Return to online mode
    await page.evaluate(() => {
      window.goOnline(); // Mock function to simulate returning online
    });
    
    // Trigger sync by navigating
    await page.click('.nav-home');
    await page.click('.nav-tournaments');
    
    // Wait for sync to complete
    await expect(page.locator('.sync-complete-indicator')).toBeVisible({ timeout: 5000 });
    
    // Verify data on server
    const serverData = await getServerData('scores', { tournament_id: 'test-tournament' });
    const hole3Score = serverData.find(s => s.hole_number === 3);
    expect(hole3Score.value).toBe(4);
  });
});
```

## Specialized Test Scenarios

### 1. Conflict Scenario Testing

Create reproducible conflict scenarios to validate resolution behavior:

```typescript
describe('Conflict Scenarios', () => {
  test('should handle concurrent edits with field-level merging', async () => {
    // Set up test scenario with different fields edited on "server" and "local"
    const originalRecord = {
      id: 'player123',
      name: 'John Doe',
      email: 'john@example.com',
      handicap: 12,
      notes: 'Original notes',
      updated_at: new Date(2023, 5, 1).toISOString()
    };
    
    // Local changes only name and handicap
    const localRecord = {
      ...originalRecord,
      name: 'Jonathan Doe', // Changed
      handicap: 14, // Changed
      updated_at: new Date(2023, 5, 2).toISOString()
    };
    
    // Server changes only email and notes
    const serverRecord = {
      ...originalRecord,
      email: 'jonathan@example.com', // Changed
      notes: 'Updated server notes', // Changed
      updated_at: new Date(2023, 5, 3).toISOString() // Newer!
    };
    
    // Execute merge resolution
    const resolvedRecord = await conflictResolver.resolveWithFieldMerging(localRecord, serverRecord);
    
    // Verify field-level merging occurred correctly
    expect(resolvedRecord.name).toBe('Jonathan Doe'); // Local change preserved
    expect(resolvedRecord.handicap).toBe(14); // Local change preserved
    expect(resolvedRecord.email).toBe('jonathan@example.com'); // Server change preserved
    expect(resolvedRecord.notes).toBe('Updated server notes'); // Server change preserved
    expect(resolvedRecord.updated_at).toBe(serverRecord.updated_at); // Server timestamp (newer) used
  });
});
```

### 2. Load and Performance Testing

Test with large volumes of data and concurrent operations:

```typescript
test('should handle large batch of offline changes efficiently', async () => {
  // Generate large test dataset
  const scoreCount = 500; // 500 score changes
  const testScores = generateTestScores(scoreCount);
  
  // Record performance metrics
  const startTime = performance.now();
  
  // Process the sync operation
  await syncService.processOfflineChanges(testScores);
  
  const endTime = performance.now();
  const syncDuration = endTime - startTime;
  
  // Assert on performance expectations
  expect(syncDuration).toBeLessThan(5000); // Should sync 500 scores in under 5 seconds
  
  // Verify correctness
  const syncedCount = await mockSupabase.getUpsertedCount();
  expect(syncedCount).toBe(scoreCount);
});
```

### 3. Network Condition Testing

Test sync behavior under varying network conditions:

```typescript
test('should handle intermittent network conditions during sync', async () => {
  // Set up test data
  const offlineChanges = generateTestChanges(50);
  await localDb.pendingChanges.bulkAdd(offlineChanges);
  
  // Configure network simulation
  const networkSimulator = new NetworkConditionSimulator();
  networkSimulator.setCondition({
    disconnectProbability: 0.3, // 30% chance of disconnect during request
    reconnectDelay: 1000, // 1 second to reconnect
    latencyRange: [200, 1500] // Variable latency
  });
  
  // Configure sync service to use simulated network
  syncService.setNetworkHandler(networkSimulator.getHandler());
  
  // Execute sync with poor network conditions
  const result = await syncService.syncPendingChanges();
  
  // Verify all changes eventually synced
  expect(result.completed).toBe(true);
  expect(result.retryCount).toBeGreaterThan(0); // Should have had to retry some
  expect(result.failedRecords).toBe(0); // But all should eventually succeed
  
  // Verify empty pending queue
  const remainingChanges = await localDb.pendingChanges.count();
  expect(remainingChanges).toBe(0);
});
```

## Mock Implementation Examples

### 1. Mock IndexedDB Implementation

```typescript
class MockIndexedDB {
  constructor() {
    this.stores = {
      scores: new Map(),
      players: new Map(),
      pendingChanges: new Map(),
      syncMetadata: new Map()
    };
  }
  
  // Implement Dexie-like API for testing
  async getTable(tableName) {
    return {
      toArray: async () => Array.from(this.stores[tableName].values()),
      get: async (id) => this.stores[tableName].get(id),
      put: async (record) => {
        this.stores[tableName].set(record.id, record);
        return record.id;
      },
      bulkPut: async (records) => {
        for (const record of records) {
          this.stores[tableName].set(record.id, record);
        }
        return records.length;
      },
      delete: async (id) => {
        this.stores[tableName].delete(id);
      },
      clear: async () => {
        this.stores[tableName].clear();
      }
    };
  }
}
```

### 2. Mock Supabase Implementation

```typescript
class MockSupabaseClient {
  constructor() {
    this.serverData = {
      scores: new Map(),
      players: new Map(),
      tournaments: new Map()
    };
    this.upsertCalls = [];
    this.selectCalls = [];
  }
  
  mockServerData(table, records) {
    for (const record of records) {
      this.serverData[table].set(record.id, record);
    }
  }
  
  from(table) {
    return {
      select: (columns = '*') => {
        this.selectCalls.push({ table, columns });
        return this;
      },
      eq: (field, value) => {
        this.lastFilter = { field, value };
        return this;
      },
      upsert: (records, options = {}) => {
        this.upsertCalls.push({ table, records, options });
        
        // Process the upsert based on our mock database
        const processedRecords = Array.isArray(records) ? records : [records];
        
        for (const record of processedRecords) {
          const existingRecord = this.serverData[table].get(record.id);
          
          if (existingRecord) {
            // Apply conflict resolution logic if needed
            if (options.onConflict === 'id') {
              if (new Date(record.updated_at) > new Date(existingRecord.updated_at)) {
                this.serverData[table].set(record.id, record);
              }
            } else {
              this.serverData[table].set(record.id, record);
            }
          } else {
            this.serverData[table].set(record.id, record);
          }
        }
        
        return { 
          data: processedRecords.map(r => ({ ...r })),
          error: null
        };
      },
      single: () => {
        const result = this.lastFilter 
          ? Array.from(this.serverData[this.lastTable].values())
              .find(r => r[this.lastFilter.field] === this.lastFilter.value)
          : null;
          
        return { 
          data: result ? { ...result } : null, 
          error: null 
        };
      }
    };
  }
  
  getUpsertedCount() {
    return this.upsertCalls.reduce((total, call) => {
      const recordCount = Array.isArray(call.records) ? call.records.length : 1;
      return total + recordCount;
    }, 0);
  }
}
```

## Testing Environment Setup

### 1. Isolated Test Environment

```typescript
// Set up isolated test environment for conflict resolution tests
async function setupConflictTestEnvironment() {
  // Create isolated database instances
  const localDb = new LocalDatabase('test-local-db');
  await localDb.initialize();
  
  // Reset database state
  await localDb.scores.clear();
  await localDb.players.clear();
  await localDb.pendingChanges.clear();
  
  // Create mock server environment
  const mockServer = new MockSupabaseClient();
  
  // Set up sync service with test hooks
  const syncService = new SyncService(localDb, mockServer);
  
  // Return test environment
  return {
    localDb,
    mockServer,
    syncService,
    async cleanup() {
      await localDb.close();
    }
  };
}
```

### 2. Network Condition Simulator

```typescript
class NetworkConditionSimulator {
  constructor() {
    this.conditions = {
      disconnectProbability: 0,
      reconnectDelay: 0,
      latencyRange: [0, 0]
    };
  }
  
  setCondition(condition) {
    this.conditions = { ...this.conditions, ...condition };
  }
  
  getHandler() {
    return {
      fetch: async (url, options) => {
        // Simulate network latency
        const latency = this._getRandomLatency();
        await new Promise(resolve => setTimeout(resolve, latency));
        
        // Simulate potential disconnection
        if (Math.random() < this.conditions.disconnectProbability) {
          // Simulate reconnection after delay
          await new Promise(resolve => setTimeout(resolve, this.conditions.reconnectDelay));
          
          // Retry request after "reconnection"
          return fetch(url, options);
        }
        
        // Normal request
        return fetch(url, options);
      }
    };
  }
  
  _getRandomLatency() {
    const [min, max] = this.conditions.latencyRange;
    return min + Math.random() * (max - min);
  }
}
```

## Testing Tournament Rules Validation

```typescript
describe('Tournament Rules Validation in Conflict Resolution', () => {
  test('should prioritize valid scores over invalid scores', async () => {
    // Set up tournament rules
    const tournamentRules = {
      maxStrokesPerHole: 10,
      enforceMaximumScore: true
    };
    
    // Set up conflict scenario
    const localScore = createTestScore({ 
      id: 'hole7player3', 
      value: 12, // Invalid: exceeds maximum
      updated_at: new Date(2023, 5, 2, 10, 0).toISOString()
    });
    
    const serverScore = createTestScore({ 
      id: 'hole7player3', 
      value: 8, // Valid
      updated_at: new Date(2023, 5, 2, 9, 0).toISOString() // Older!
    });
    
    // Create validator
    const scoreValidator = new ScoreValidator(tournamentRules);
    
    // Create resolver with validation
    const validationAwareResolver = new ValidationAwareResolver(scoreValidator);
    
    // Resolve conflict
    const resolvedScore = await validationAwareResolver.resolve(localScore, serverScore);
    
    // Server score should win despite being older, because local is invalid
    expect(resolvedScore.value).toBe(8);
    expect(resolvedScore.resolution).toBe('SERVER_VALID_LOCAL_INVALID');
  });
});
```

## Automated Testing Schedule

To ensure comprehensive testing of our conflict resolution strategy, we follow this testing schedule:

1. **Continuous Testing**
   - Unit tests run on every commit
   - Integration tests run on every PR
   - End-to-end tests run nightly

2. **Pre-Release Testing**
   - Full conflict resolution test suite
   - Extended offline scenario testing
   - Performance testing with large datasets
   - Network resilience testing

3. **Production Monitoring**
   - Sync success rate tracking
   - Conflict resolution metrics
   - Error tracking for sync failures
   - Performance monitoring for synchronization operations

## Conclusion

This comprehensive testing strategy ensures that our conflict resolution approach works reliably across various scenarios, handling offline editing, synchronization challenges, and data conflicts appropriately. Regular execution of these tests maintains the integrity of our core conflict resolution mechanisms and helps identify potential issues before they affect users.

By consistently testing both common and edge cases, we can be confident that the Golf Tournament Organizer application will provide a seamless experience for users, even in challenging connectivity environments.
