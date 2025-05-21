// Real-time data handling for tournament leaderboards
import { createClient } from '@supabase/supabase-js';

/**
 * RealtimeLeaderboard class handles live updates for tournament leaderboards
 * using Supabase's realtime functionality
 */
export class RealtimeLeaderboard {
  private supabaseClient;
  private subscriptions = new Map();
  private fallbackEnabled = false;
  private fallbackIntervalMs = 15000; // Default 15 seconds
  private fallbackIntervals = new Map();
  private onUpdateCallbacks = new Map();

  /**
   * Create a new RealtimeLeaderboard instance
   * @param supabaseUrl The URL of your Supabase project
   * @param supabaseKey The anon key of your Supabase project
   * @param config Optional configuration
   */
  constructor(
    supabaseUrl: string, 
    supabaseKey: string,
    config?: {
      enableFallback?: boolean;
      fallbackIntervalMs?: number;
    }
  ) {
    this.supabaseClient = createClient(supabaseUrl, supabaseKey);
    
    if (config?.enableFallback !== undefined) {
      this.fallbackEnabled = config.enableFallback;
    }
    
    if (config?.fallbackIntervalMs !== undefined) {
      this.fallbackIntervalMs = config.fallbackIntervalMs;
    }

    // Listen for online/offline events to handle connection changes
    if (typeof window !== 'undefined') {
      window.addEventListener('online', () => this.handleOnline());
      window.addEventListener('offline', () => this.handleOffline());
    }
  }

  /**
   * Subscribe to real-time leaderboard updates
   * @param tournamentId The tournament ID
   * @param format The tournament format ('stroke', 'stableford', or 'match')
   * @param callback Function to call when the leaderboard updates
   * @returns A subscription ID that can be used to unsubscribe
   */
  subscribeToLeaderboard(
    tournamentId: string, 
    format: 'stroke' | 'stableford' | 'match', 
    callback: (data: any) => void
  ): string {
    // Generate a unique subscription ID
    const subscriptionId = `leaderboard_${format}_${tournamentId}_${Date.now()}`;
    
    // Store the callback
    this.onUpdateCallbacks.set(subscriptionId, callback);
    
    // Determine which view to subscribe to based on the format
    const viewName = `${format}_leaderboard`;
    
    try {
      // Try to establish a real-time subscription
      const channel = this.supabaseClient.channel(`leaderboard-${tournamentId}`)
        .on(
          'postgres_changes',
          {
            event: '*',
            schema: 'public',
            table: viewName,
            filter: `tournament_id=eq.${tournamentId}`
          },
          async (payload) => {
            // When we get a real-time update, fetch the complete leaderboard
            // This ensures we have the full sorted data rather than just the changed record
            await this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
          }
        )
        .on(
          'postgres_changes',
          {
            event: '*',
            schema: 'public',
            table: 'scores',
            filter: `tournament_id=eq.${tournamentId}`
          },
          async () => {
            // When scores change, fetch the updated leaderboard
            await this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
          }
        )
        .subscribe();
      
      // Store the subscription
      this.subscriptions.set(subscriptionId, {
        channel,
        tournamentId,
        format,
        type: 'realtime'
      });
      
      // Immediately fetch the current leaderboard
      this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
    } catch (error) {
      console.warn('Failed to establish real-time connection, falling back to polling', error);
      
      // Fall back to polling if real-time subscription fails
      if (this.fallbackEnabled) {
        this.setupPolling(subscriptionId, tournamentId, format);
      }
    }
    
    return subscriptionId;
  }

  /**
   * Unsubscribe from leaderboard updates
   * @param subscriptionId The subscription ID returned by subscribeToLeaderboard
   */
  unsubscribe(subscriptionId: string): void {
    // Clean up the real-time subscription if it exists
    const subscription = this.subscriptions.get(subscriptionId);
    if (subscription) {
      if (subscription.type === 'realtime') {
        this.supabaseClient.removeChannel(subscription.channel);
      } else if (subscription.type === 'polling') {
        // Clean up polling interval
        const interval = this.fallbackIntervals.get(subscriptionId);
        if (interval) {
          clearInterval(interval);
          this.fallbackIntervals.delete(subscriptionId);
        }
      }
      
      this.subscriptions.delete(subscriptionId);
    }
    
    // Remove the callback
    this.onUpdateCallbacks.delete(subscriptionId);
  }

  /**
   * Manually trigger a leaderboard refresh
   * @param tournamentId The tournament ID
   * @param format The tournament format
   */
  async refreshLeaderboard(tournamentId: string, format: 'stroke' | 'stableford' | 'match'): Promise<void> {
    // Find all subscriptions for this tournament and format
    for (const [subscriptionId, subscription] of this.subscriptions.entries()) {
      if (subscription.tournamentId === tournamentId && subscription.format === format) {
        await this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
      }
    }
  }

  /**
   * Set up polling as a fallback mechanism
   * @param subscriptionId The subscription ID
   * @param tournamentId The tournament ID
   * @param format The tournament format
   */
  private setupPolling(
    subscriptionId: string, 
    tournamentId: string, 
    format: 'stroke' | 'stableford' | 'match'
  ): void {
    // Store subscription info
    this.subscriptions.set(subscriptionId, {
      tournamentId,
      format,
      type: 'polling'
    });
    
    // Set up interval for polling
    const interval = setInterval(
      () => this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId),
      this.fallbackIntervalMs
    );
    
    // Store the interval for cleanup later
    this.fallbackIntervals.set(subscriptionId, interval);
    
    // Immediately fetch the current leaderboard
    this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
  }

  /**
   * Fetch the latest leaderboard data and call the update callback
   * @param tournamentId The tournament ID
   * @param format The tournament format
   * @param subscriptionId The subscription ID
   */
  private async fetchAndUpdateLeaderboard(
    tournamentId: string, 
    format: 'stroke' | 'stableford' | 'match', 
    subscriptionId: string
  ): Promise<void> {
    try {
      const viewName = `${format}_leaderboard`;
      
      const { data, error } = await this.supabaseClient
        .from(viewName)
        .select('*')
        .eq('tournament_id', tournamentId)
        .order(format === 'stableford' ? 'points' : 'total_strokes', { 
          ascending: format !== 'stableford' 
        });
      
      if (error) {
        throw error;
      }
      
      // Call the callback with the updated data
      const callback = this.onUpdateCallbacks.get(subscriptionId);
      if (callback && data) {
        callback(data);
      }
    } catch (error) {
      console.error('Error fetching leaderboard:', error);
    }
  }

  /**
   * Handle device reconnecting to the network
   */
  private async handleOnline(): Promise<void> {
    console.log('Network connection restored, reconnecting real-time subscriptions');
    
    // Convert polling subscriptions back to real-time when we go online
    for (const [subscriptionId, subscription] of this.subscriptions.entries()) {
      if (subscription.type === 'polling') {
        // Clear the polling interval
        const interval = this.fallbackIntervals.get(subscriptionId);
        if (interval) {
          clearInterval(interval);
          this.fallbackIntervals.delete(subscriptionId);
        }
        
        // Try to establish a real-time subscription
        try {
          const { tournamentId, format } = subscription;
          const viewName = `${format}_leaderboard`;
          
          const channel = this.supabaseClient.channel(`leaderboard-${tournamentId}`)
            .on(
              'postgres_changes',
              {
                event: '*',
                schema: 'public',
                table: viewName,
                filter: `tournament_id=eq.${tournamentId}`
              },
              async () => {
                await this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
              }
            )
            .on(
              'postgres_changes',
              {
                event: '*',
                schema: 'public',
                table: 'scores',
                filter: `tournament_id=eq.${tournamentId}`
              },
              async () => {
                await this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
              }
            )
            .subscribe();
          
          // Update the subscription type
          this.subscriptions.set(subscriptionId, {
            channel,
            tournamentId,
            format,
            type: 'realtime'
          });
          
          // Fetch the latest data
          await this.fetchAndUpdateLeaderboard(tournamentId, format, subscriptionId);
        } catch (error) {
          console.warn('Failed to reestablish real-time connection, continuing with polling', error);
          // Recreate the polling subscription
          this.setupPolling(subscriptionId, subscription.tournamentId, subscription.format);
        }
      }
    }
  }

  /**
   * Handle device going offline
   */
  private handleOffline(): void {
    console.log('Network connection lost, switching to polling mode');
    
    // Convert real-time subscriptions to polling when we go offline
    for (const [subscriptionId, subscription] of this.subscriptions.entries()) {
      if (subscription.type === 'realtime') {
        // Remove the real-time subscription
        this.supabaseClient.removeChannel(subscription.channel);
        
        // Set up polling if fallback is enabled
        if (this.fallbackEnabled) {
          this.setupPolling(subscriptionId, subscription.tournamentId, subscription.format);
        }
      }
    }
  }
}
