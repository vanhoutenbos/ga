# Supabase Real-Time Testing Implementation Guide

This document provides practical implementation guidance for testing Supabase real-time features in our Golf Tournament application, particularly focusing on strategies applicable to the free tier.

## Local Development Testing

### Mock Implementation

```typescript
// src/mocks/supabaseRealtimeMock.ts
export class SupabaseRealtimeMock {
  private listeners: Map<string, Function[]> = new Map();
  
  /**
   * Mock subscription to a table
   */
  public from(table: string) {
    return {
      on: (event: string, callback: Function) => {
        const key = `${table}:${event}`;
        if (!this.listeners.has(key)) {
          this.listeners.set(key, []);
        }
        this.listeners.get(key)?.push(callback);
        
        // Return an object with a subscription handle
        return {
          subscribe: () => ({
            unsubscribe: () => {
              const callbacks = this.listeners.get(key) || [];
              const index = callbacks.indexOf(callback);
              if (index > -1) {
                callbacks.splice(index, 1);
              }
            }
          })
        };
      }
    };
  }
  
  /**
   * Simulate an event occurring
   */
  public simulateEvent(table: string, event: string, payload: any) {
    const key = `${table}:${event}`;
    const callbacks = this.listeners.get(key) || [];
    callbacks.forEach(callback => callback(payload));
  }
}
```

### Example Unit Test

```typescript
// src/features/leaderboard/LeaderboardSubscription.test.ts
import { SupabaseRealtimeMock } from '../../mocks/supabaseRealtimeMock';
import { LeaderboardService } from './LeaderboardService';

describe('LeaderboardService', () => {
  let realtimeMock: SupabaseRealtimeMock;
  let leaderboardService: LeaderboardService;
  
  beforeEach(() => {
    realtimeMock = new SupabaseRealtimeMock();
    leaderboardService = new LeaderboardService(realtimeMock as any);
  });
  
  test('should update leaderboard when scores change', () => {
    // Setup
    const tournamentId = '123';
    const mockUpdateHandler = jest.fn();
    
    // Subscribe to leaderboard updates
    leaderboardService.subscribeToLeaderboard(tournamentId, mockUpdateHandler);
    
    // Simulate a score update
    const mockPayload = {
      new: {
        tournament_id: tournamentId,
        player_id: '456',
        total_score: 72,
        position: 1
      },
      old: {
        tournament_id: tournamentId,
        player_id: '456',
        total_score: 74,
        position: 3
      }
    };
    
    realtimeMock.simulateEvent('leaderboard', 'UPDATE', mockPayload);
    
    // Verify the update handler was called with the new data
    expect(mockUpdateHandler).toHaveBeenCalledWith(expect.objectContaining({
      playerId: '456',
      totalScore: 72,
      position: 1
    }));
  });
  
  test('should handle subscription errors gracefully', () => {
    // Setup error handler spy
    const errorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    const fallbackMock = jest.fn();
    
    // Create service with error simulation
    const errorMock = {
      from: () => ({
        on: () => {
          throw new Error('Subscription failed');
        }
      })
    };
    
    const serviceWithError = new LeaderboardService(
      errorMock as any,
      fallbackMock
    );
    
    // Try to subscribe
    serviceWithError.subscribeToLeaderboard('123', jest.fn());
    
    // Verify error was logged and fallback was called
    expect(errorSpy).toHaveBeenCalled();
    expect(fallbackMock).toHaveBeenCalledWith('123');
  });
});
```

## Fallback Mechanism Implementation

```typescript
// src/services/supabase/LeaderboardService.ts
export class LeaderboardService {
  private subscriptions: Map<string, { unsubscribe: () => void }> = new Map();
  private pollingIntervals: Map<string, NodeJS.Timeout> = new Map();
  
  constructor(
    private supabase: SupabaseClient,
    private fallbackPollingInterval: number = 15000
  ) {}
  
  /**
   * Subscribe to leaderboard updates for a tournament
   */
  public subscribeToLeaderboard(
    tournamentId: string,
    onUpdate: (data: LeaderboardEntry) => void
  ): void {
    try {
      // Try to use realtime subscriptions
      const subscription = this.supabase
        .from(`tournament_leaderboard:tournament_id=eq.${tournamentId}`)
        .on('*', (payload) => {
          onUpdate(this.mapToLeaderboardEntry(payload.new));
        })
        .subscribe();
        
      this.subscriptions.set(tournamentId, subscription);
    } catch (error) {
      console.error('Failed to create realtime subscription', error);
      // Fall back to polling
      this.setupPollingFallback(tournamentId, onUpdate);
    }
  }
  
  /**
   * Unsubscribe from leaderboard updates
   */
  public unsubscribeFromLeaderboard(tournamentId: string): void {
    // Clean up realtime subscription if exists
    const subscription = this.subscriptions.get(tournamentId);
    if (subscription) {
      subscription.unsubscribe();
      this.subscriptions.delete(tournamentId);
    }
    
    // Clean up polling interval if exists
    const pollingInterval = this.pollingIntervals.get(tournamentId);
    if (pollingInterval) {
      clearInterval(pollingInterval);
      this.pollingIntervals.delete(tournamentId);
    }
  }
  
  /**
   * Set up polling as a fallback when realtime fails
   */
  private setupPollingFallback(
    tournamentId: string,
    onUpdate: (data: LeaderboardEntry) => void
  ): void {
    // Initial fetch
    this.fetchLeaderboardData(tournamentId, onUpdate);
    
    // Set up interval for polling
    const interval = setInterval(() => {
      this.fetchLeaderboardData(tournamentId, onUpdate);
    }, this.fallbackPollingInterval);
    
    this.pollingIntervals.set(tournamentId, interval);
  }
  
  /**
   * Fetch leaderboard data via REST API
   */
  private async fetchLeaderboardData(
    tournamentId: string,
    onUpdate: (data: LeaderboardEntry) => void
  ): Promise<void> {
    try {
      const { data, error } = await this.supabase
        .from('tournament_leaderboard')
        .select('*')
        .eq('tournament_id', tournamentId);
        
      if (error) throw error;
      
      if (data) {
        data.forEach(entry => onUpdate(this.mapToLeaderboardEntry(entry)));
      }
    } catch (error) {
      console.error('Failed to fetch leaderboard data', error);
    }
  }
  
  /**
   * Map database entry to application model
   */
  private mapToLeaderboardEntry(data: any): LeaderboardEntry {
    return {
      playerId: data.player_id,
      playerName: data.player_name,
      totalScore: data.total_score,
      position: data.position,
      // other fields...
    };
  }
}
```

## Integration Tests Against Supabase Development Instance

### Setup Test Environment

```typescript
// src/tests/integration/setupSupabaseTestEnv.ts
import { createClient } from '@supabase/supabase-js';
import { execSync } from 'child_process';
import { promises as fs } from 'fs';
import path from 'path';

/**
 * Sets up a test environment with a dedicated Supabase project
 */
export async function setupSupabaseTestEnv() {
  const supabase = createClient(
    process.env.SUPABASE_TEST_URL!,
    process.env.SUPABASE_TEST_ANON_KEY!
  );
  
  // Reset database to known state
  try {
    // Apply schema from SQL files
    const schemaSql = await fs.readFile(
      path.join(__dirname, '../../db/schema.sql'),
      'utf-8'
    );
    
    await supabase.rpc('exec_sql', { sql: schemaSql });
    
    // Apply seed data
    const seedSql = await fs.readFile(
      path.join(__dirname, '../../db/seed-test-data.sql'),
      'utf-8'
    );
    
    await supabase.rpc('exec_sql', { sql: seedSql });
    
    return supabase;
  } catch (error) {
    console.error('Failed to setup test environment', error);
    throw error;
  }
}
```

### Real-Time Integration Test

```typescript
// src/tests/integration/leaderboard/leaderboardRealtime.test.ts
import { setupSupabaseTestEnv } from '../setupSupabaseTestEnv';
import { LeaderboardService } from '../../../services/LeaderboardService';

describe('Leaderboard Realtime Integration', () => {
  let supabase;
  let leaderboardService;
  
  beforeAll(async () => {
    supabase = await setupSupabaseTestEnv();
    leaderboardService = new LeaderboardService(supabase);
  });
  
  test('should receive updates when scores change', async () => {
    // Setup
    const tournamentId = 'test-tournament-1';
    const updates = [];
    
    // Create a promise that resolves when update is received
    const updatePromise = new Promise(resolve => {
      // Subscribe to leaderboard updates
      leaderboardService.subscribeToLeaderboard(tournamentId, (data) => {
        updates.push(data);
        if (updates.length >= 1) {
          resolve(updates);
        }
      });
    });
    
    // Update a score in the database
    await supabase
      .from('scores')
      .update({ strokes: 4 })
      .eq('tournament_id', tournamentId)
      .eq('player_id', 'test-player-1')
      .eq('hole_number', 1);
    
    // Wait for the update to be received via realtime
    const receivedUpdates = await updatePromise;
    
    // Cleanup
    leaderboardService.unsubscribeFromLeaderboard(tournamentId);
    
    // Verify the update was received
    expect(receivedUpdates.length).toBeGreaterThan(0);
    const update = receivedUpdates[0];
    expect(update.playerId).toBe('test-player-1');
  }, 10000); // Longer timeout for realtime test
  
  test('should fall back to polling when realtime fails', async () => {
    // Setup - create service with mock that fails realtime
    const mockSupabase = {
      from: () => ({
        on: () => {
          throw new Error('Simulated realtime failure');
        },
        select: () => ({
          eq: () => Promise.resolve({
            data: [
              {
                player_id: 'test-player-1',
                player_name: 'Test Player',
                total_score: 72,
                position: 1
              }
            ],
            error: null
          })
        })
      })
    };
    
    const pollingService = new LeaderboardService(mockSupabase, 100); // 100ms polling for tests
    const updates = [];
    
    // Start subscription (should fail and fall back to polling)
    pollingService.subscribeToLeaderboard('test-tournament-1', (data) => {
      updates.push(data);
    });
    
    // Wait for polling to occur
    await new Promise(resolve => setTimeout(resolve, 200));
    
    // Cleanup
    pollingService.unsubscribeFromLeaderboard('test-tournament-1');
    
    // Verify data was received via polling
    expect(updates.length).toBeGreaterThan(0);
    expect(updates[0].playerId).toBe('test-player-1');
  });
});
```

## Monitoring Implementation

```typescript
// src/services/monitoring/RealtimeMonitor.ts
export class RealtimeMonitor {
  private connectionState: 'connected' | 'disconnected' | 'connecting' = 'disconnected';
  private subscriptionCounts: Map<string, number> = new Map();
  private eventDeliveryTimes: number[] = [];
  private fallbackActivations: number = 0;
  
  constructor(
    private supabase: SupabaseClient,
    private telemetry: TelemetryService
  ) {
    // Monitor connection state
    this.monitorConnection();
  }
  
  /**
   * Track when a subscription is created
   */
  public trackSubscription(channel: string): void {
    const current = this.subscriptionCounts.get(channel) || 0;
    this.subscriptionCounts.set(channel, current + 1);
    
    // Report metrics
    this.telemetry.trackMetric('supabase.realtime.subscriptions', 
      Array.from(this.subscriptionCounts.values()).reduce((a, b) => a + b, 0));
  }
  
  /**
   * Track when a subscription is closed
   */
  public trackUnsubscribe(channel: string): void {
    const current = this.subscriptionCounts.get(channel) || 0;
    if (current > 0) {
      this.subscriptionCounts.set(channel, current - 1);
    }
  }
  
  /**
   * Track when an event is received
   */
  public trackEventReceived(eventId: string, sentAt: Date): void {
    const now = new Date();
    const latencyMs = now.getTime() - sentAt.getTime();
    
    this.eventDeliveryTimes.push(latencyMs);
    
    // Keep only the last 100 measurements
    if (this.eventDeliveryTimes.length > 100) {
      this.eventDeliveryTimes.shift();
    }
    
    // Track latency
    this.telemetry.trackMetric('supabase.realtime.latency', latencyMs);
  }
  
  /**
   * Track when fallback mechanism is activated
   */
  public trackFallbackActivated(reason: string): void {
    this.fallbackActivations++;
    this.telemetry.trackEvent('supabase.realtime.fallback', { reason });
    this.telemetry.trackMetric('supabase.realtime.fallback.count', this.fallbackActivations);
  }
  
  /**
   * Get average event delivery latency
   */
  public getAverageLatency(): number {
    if (this.eventDeliveryTimes.length === 0) return 0;
    
    const sum = this.eventDeliveryTimes.reduce((a, b) => a + b, 0);
    return sum / this.eventDeliveryTimes.length;
  }
  
  /**
   * Monitor connection state
   */
  private monitorConnection(): void {
    // Track connection state changes
    this.supabase.realtime.onConnected(() => {
      this.connectionState = 'connected';
      this.telemetry.trackEvent('supabase.realtime.connected');
    });
    
    this.supabase.realtime.onConnecting(() => {
      this.connectionState = 'connecting';
      this.telemetry.trackEvent('supabase.realtime.connecting');
    });
    
    this.supabase.realtime.onDisconnected(() => {
      this.connectionState = 'disconnected';
      this.telemetry.trackEvent('supabase.realtime.disconnected');
    });
  }
}
```

## Free Tier Specific Testing Approaches

### Service Throttling Simulation

```typescript
// src/services/testing/FreeThrottlingTest.ts
export async function simulateFreeThrottling(tournamentId: string): Promise<void> {
  // Setup test data
  const supabase = createClient(
    process.env.SUPABASE_URL!,
    process.env.SUPABASE_ANON_KEY!
  );
  
  const MAX_CONNECTIONS = 50; // Free tier limit
  const connections = [];
  
  console.log('Starting free tier throttling test...');
  
  // Create monitoring
  const monitor = new RealtimeMonitor(supabase);
  
  // Function to create a subscription
  const createSubscription = (index: number) => {
    console.log(`Creating subscription ${index}...`);
    
    const subscription = supabase
      .from(`tournament_leaderboard:tournament_id=eq.${tournamentId}`)
      .on('*', (payload) => {
        console.log(`Subscription ${index} received update:`, payload);
        monitor.trackEventReceived(payload.commit_timestamp, new Date(payload.commit_timestamp));
      })
      .subscribe();
      
    connections.push(subscription);
    return subscription;
  };
  
  // Test creating connections up to and beyond free tier limit
  try {
    // Create connections up to limit
    for (let i = 0; i < MAX_CONNECTIONS; i++) {
      createSubscription(i);
      
      // Small delay to avoid overwhelming the service
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    
    console.log(`Created ${MAX_CONNECTIONS} connections. Testing throttling...`);
    
    // Try to create a few more connections (should fail or be throttled)
    for (let i = 0; i < 5; i++) {
      try {
        createSubscription(MAX_CONNECTIONS + i);
      } catch (error) {
        console.log(`Error creating connection ${MAX_CONNECTIONS + i}:`, error);
      }
    }
    
    // Make a change that should trigger realtime updates
    console.log('Making test update to trigger events...');
    await supabase
      .from('scores')
      .update({ strokes: 5 })
      .eq('tournament_id', tournamentId)
      .eq('hole_number', 1)
      .eq('player_id', 'test-player-1');
      
    // Wait to see which subscriptions receive updates
    await new Promise(resolve => setTimeout(resolve, 5000));
    
    // Check metrics
    console.log('Average event latency:', monitor.getAverageLatency());
    
  } finally {
    // Cleanup all connections
    console.log('Cleaning up test connections...');
    connections.forEach(subscription => {
      if (subscription && subscription.unsubscribe) {
        subscription.unsubscribe();
      }
    });
  }
  
  console.log('Free tier throttling test completed.');
}
```

### Usage Limit Monitoring

```typescript
// src/services/monitoring/SupabaseUsageMonitor.ts
export class SupabaseUsageMonitor {
  // Counter for database operations
  private dbOperations: number = 0;
  
  // Counter for realtime connections
  private activeConnections: number = 0;
  
  // Free tier limits
  private readonly FREE_TIER_DB_SIZE_LIMIT_MB = 500;
  private readonly FREE_TIER_CONNECTIONS_LIMIT = 50;
  
  constructor(
    private supabase: SupabaseClient,
    private telemetry: TelemetryService,
    private notificationService?: NotificationService
  ) {}
  
  /**
   * Track database operation
   */
  public trackDbOperation(): void {
    this.dbOperations++;
    
    // Track metrics every 100 operations
    if (this.dbOperations % 100 === 0) {
      this.checkDatabaseSize();
    }
  }
  
  /**
   * Track realtime connection
   */
  public trackConnection(isConnecting: boolean): void {
    if (isConnecting) {
      this.activeConnections++;
    } else {
      this.activeConnections = Math.max(0, this.activeConnections - 1);
    }
    
    this.telemetry.trackMetric('supabase.connections', this.activeConnections);
    
    // Check if approaching limit
    const usagePercentage = (this.activeConnections / this.FREE_TIER_CONNECTIONS_LIMIT) * 100;
    
    if (usagePercentage > 80) {
      this.telemetry.trackEvent('supabase.connections.approaching_limit', {
        percentage: usagePercentage
      });
      
      // Notify if needed
      if (usagePercentage > 90 && this.notificationService) {
        this.notificationService.notify(
          'Approaching Supabase Free Tier Connection Limit',
          `Currently at ${usagePercentage.toFixed(1)}% of free tier connection limit.`
        );
      }
    }
  }
  
  /**
   * Check database size and usage
   */
  private async checkDatabaseSize(): Promise<void> {
    try {
      // This would need to be implemented using a custom function in Supabase
      const { data } = await this.supabase.rpc('get_db_size_mb');
      
      if (data) {
        const sizeInMb = data.size_mb;
        this.telemetry.trackMetric('supabase.database.size_mb', sizeInMb);
        
        // Check if approaching limit
        const usagePercentage = (sizeInMb / this.FREE_TIER_DB_SIZE_LIMIT_MB) * 100;
        
        if (usagePercentage > 80) {
          this.telemetry.trackEvent('supabase.database.approaching_limit', {
            percentage: usagePercentage
          });
          
          // Notify if needed
          if (usagePercentage > 90 && this.notificationService) {
            this.notificationService.notify(
              'Approaching Supabase Free Tier Database Size Limit',
              `Currently at ${usagePercentage.toFixed(1)}% of free tier database size limit.`
            );
          }
        }
      }
    } catch (error) {
      console.error('Failed to check database size', error);
    }
  }
}
```

## Sample SQL Stored Function for Database Size Monitoring

```sql
-- Function to check database size
-- Create this in your Supabase SQL editor
CREATE OR REPLACE FUNCTION public.get_db_size_mb()
RETURNS TABLE (size_mb FLOAT) SECURITY DEFINER
AS $$
BEGIN
  RETURN QUERY
  SELECT
    pg_database_size(current_database()) / (1024 * 1024)::float AS size_mb;
END;
$$ LANGUAGE plpgsql;

-- Grant access to the function
GRANT EXECUTE ON FUNCTION public.get_db_size_mb() TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_db_size_mb() TO anon;
```

This implementation guide provides practical code examples for testing Supabase real-time features, implementing fallback mechanisms, and monitoring usage - all critical considerations when using the free tier of Supabase.
