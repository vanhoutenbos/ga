// Cache module for Supabase client
// This module extends the Supabase client with caching capabilities

import { createClient } from '@supabase/supabase-js';
import * as localforage from 'localforage';

/**
 * CacheableSupabase extends the standard Supabase client with caching capabilities
 */
export class CacheableSupabase {
  supabaseClient;
  cache;

  /**
   * Constructor for the CacheableSupabase client
   * @param supabaseUrl The URL of your Supabase project
   * @param supabaseKey The anon key of your Supabase project
   */
  constructor(supabaseUrl, supabaseKey) {
    this.supabaseClient = createClient(supabaseUrl, supabaseKey);
    
    // Initialize localforage for cache storage
    this.cache = localforage.createInstance({
      name: 'golfapp-cache',
      version: 1.0,
      storeName: 'supabase_cache',
      description: 'Cache for Supabase data'
    });
  }

  /**
   * Get the underlying Supabase client
   */
  get supabase() {
    return this.supabaseClient;
  }

  /**
   * Perform a cached query
   * @param table The table to query
   * @param query The query function to execute on the table
   * @param cacheKey The unique cache key
   * @param ttlSeconds Time to live in seconds
   * @param forceRefresh Whether to force a refresh of the cached data
   */
  async cachedQuery(table, query, cacheKey, ttlSeconds = 300, forceRefresh = false) {
    if (!navigator.onLine) {
      // Offline mode - try to return cached data
      const cachedData = await this.getFromCache(cacheKey);
      if (cachedData) {
        return { 
          data: cachedData.data, 
          error: null, 
          source: 'cache',
          timestamp: cachedData.timestamp 
        };
      }
      return { data: null, error: new Error('No cached data available and offline'), source: 'none' };
    }

    if (!forceRefresh) {
      // Check cache first if not forcing refresh
      const cachedData = await this.getFromCache(cacheKey);
      if (cachedData && this.isCacheValid(cachedData, ttlSeconds)) {
        return { 
          data: cachedData.data, 
          error: null, 
          source: 'cache',
          timestamp: cachedData.timestamp 
        };
      }
    }

    // Get fresh data from Supabase
    try {
      const { data, error } = await query(this.supabaseClient.from(table));
      
      if (error) {
        throw error;
      }
      
      // Cache the result
      await this.saveToCache(cacheKey, data);
      
      return { data, error: null, source: 'network', timestamp: Date.now() };
    } catch (error) {
      // On error, try to return cached data even if expired
      const cachedData = await this.getFromCache(cacheKey);
      if (cachedData) {
        return { 
          data: cachedData.data, 
          error: new Error(`Network error, using stale cache: ${error.message}`), 
          source: 'stale-cache',
          timestamp: cachedData.timestamp 
        };
      }
      return { data: null, error, source: 'error' };
    }
  }

  /**
   * Get data for a tournament leaderboard with caching
   * @param tournamentId The tournament ID
   * @param format The tournament format ('stroke', 'stableford', or 'match')
   * @param ttlSeconds Cache TTL in seconds (defaults to 30 seconds for leaderboard)
   */
  async getTournamentLeaderboard(tournamentId, format, ttlSeconds = 30) {
    const cacheKey = `leaderboard_${format}_${tournamentId}`;
    let viewName;
    
    switch (format) {
      case 'stroke':
        viewName = 'stroke_leaderboard';
        break;
      case 'stableford':
        viewName = 'stableford_leaderboard';
        break;
      case 'match':
        viewName = 'match_leaderboard';
        break;
      default:
        throw new Error(`Unsupported tournament format: ${format}`);
    }
    
    return this.cachedQuery(
      viewName,
      (query) => query.eq('tournament_id', tournamentId),
      cacheKey,
      ttlSeconds
    );
  }

  /**
   * Get cached tournament data
   * @param tournamentId The tournament ID
   * @param ttlSeconds Cache TTL in seconds (defaults to 300 seconds = 5 minutes)
   */
  async getTournament(tournamentId, ttlSeconds = 300) {
    const cacheKey = `tournament_${tournamentId}`;
    
    return this.cachedQuery(
      'tournaments',
      (query) => query.select(`
        *,
        course:courses(*),
        tournament_players(*),
        flights(*)
      `).eq('id', tournamentId).single(),
      cacheKey,
      ttlSeconds
    );
  }

  /**
   * Save data to the cache
   * @param key The cache key
   * @param data The data to cache
   */
  async saveToCache(key, data) {
    await this.cache.setItem(key, {
      data,
      timestamp: Date.now()
    });
  }

  /**
   * Get data from the cache
   * @param key The cache key
   */
  async getFromCache(key) {
    return await this.cache.getItem(key);
  }

  /**
   * Check if cached data is still valid
   * @param cachedData The cached data object
   * @param ttlSeconds Time to live in seconds
   */
  isCacheValid(cachedData, ttlSeconds) {
    const now = Date.now();
    const expiryTime = cachedData.timestamp + (ttlSeconds * 1000);
    return now < expiryTime;
  }

  /**
   * Manually invalidate a cache entry
   * @param key The cache key to invalidate
   */
  async invalidateCache(key) {
    await this.cache.removeItem(key);
  }

  /**
   * Clear all cached data
   */
  async clearCache() {
    await this.cache.clear();
  }

  /**
   * Initialize offline data for a tournament
   * This method prefetches and caches tournament data for offline use
   * @param tournamentId The tournament ID
   */
  async initOfflineData(tournamentId) {
    try {
      // Get tournament details
      await this.getTournament(tournamentId, 3600); // 1 hour cache
      
      // Get tournament format
      const { data: tournament } = await this.supabaseClient
        .from('tournaments')
        .select('format')
        .eq('id', tournamentId)
        .single();
      
      if (!tournament) {
        throw new Error('Tournament not found');
      }
      
      // Get leaderboard
      await this.getTournamentLeaderboard(tournamentId, tournament.format, 3600);
      
      // Get player scores
      const { data: players } = await this.supabaseClient
        .from('tournament_players')
        .select('id')
        .eq('tournament_id', tournamentId);
      
      if (players && players.length > 0) {
        for (const player of players) {
          const cacheKey = `scores_${tournamentId}_${player.id}`;
          
          await this.cachedQuery(
            'scores',
            (query) => query
              .select('*')
              .eq('tournament_id', tournamentId)
              .eq('player_id', player.id),
            cacheKey,
            3600 // 1 hour cache
          );
        }
      }
      
      return { success: true, message: 'Offline data cached successfully' };
    } catch (error) {
      console.error('Error caching offline data:', error);
      return { success: false, error: error.message };
    }
  }
}
