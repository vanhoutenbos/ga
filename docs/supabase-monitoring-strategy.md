# Supabase Monitoring Strategy

This document outlines our comprehensive strategy for monitoring Supabase performance, health, and usage in the Golf Tournament Organizer application. It covers technical implementation, key metrics to track, alert thresholds, and integration with monitoring platforms.

## Table of Contents

1. [Overview](#overview)
2. [Key Metrics](#key-metrics)
3. [Implementation](#implementation)
   - [Client-Side Monitoring](#client-side-monitoring)
   - [Server-Side Monitoring](#server-side-monitoring)
   - [Azure Function Integration](#azure-function-integration)
4. [Dashboard Setup](#dashboard-setup)
5. [Alert Configuration](#alert-configuration)
6. [Free Tier Considerations](#free-tier-considerations)
7. [Periodic Health Checks](#periodic-health-checks)
8. [Load Testing Strategy](#load-testing-strategy)
   - [Test Environment Setup](#test-environment-setup)
   - [Virtual User Simulation](#virtual-user-simulation)
   - [Metrics Collection](#metrics-collection)
   - [Load Test Orchestrator](#load-test-orchestrator)
   - [Test Scenarios](#test-scenarios)
   - [Optimization Strategies](#optimization-strategies-based-on-test-results)
   - [Interpreting Test Results](#interpreting-test-results)

## Overview

Effectively monitoring our Supabase instance is critical for ensuring consistent performance, identifying issues before they affect users, and planning for scaling needs. Our monitoring strategy combines:

1. **Client-side telemetry**: Collected directly from our application
2. **Server-side monitoring**: Via Supabase logs and dashboard metrics
3. **Custom health checks**: Active probing for availability and performance
4. **Usage threshold alerts**: Particularly important for free tier limitations
5. **Error tracking**: Aggregation and analysis of errors

## Key Metrics

We focus on the following key metrics to maintain optimal Supabase performance:

### Database Performance

| Metric | Description | Alert Threshold | Critical Threshold |
|--------|-------------|------------------|-------------------|
| Query Duration | Time to execute database queries | > 500ms avg | > 1000ms avg |
| Database Size | Total size of the PostgreSQL database | > 400MB (80% of free tier) | > 450MB (90% of free tier) |
| Active Connections | Current number of database connections | > 40 (80% of free tier) | > 45 (90% of free tier) |
| RLS Policy Performance | Time spent evaluating RLS policies | > 200ms avg | > 500ms avg |
| Cache Hit Ratio | Percentage of queries served from cache | < 70% | < 50% |

### Realtime Performance

| Metric | Description | Alert Threshold | Critical Threshold |
|--------|-------------|------------------|-------------------|
| Connection Count | Number of active realtime connections | > 40 (80% of free tier) | > 45 (90% of free tier) |
| Event Delivery Latency | Time from database change to client notification | > 500ms avg | > 1500ms avg |
| Connection Stability | Connection drops per hour | > 5 per hour | > 15 per hour |
| Channel Subscription Count | Number of channels being subscribed to | > 80 | > 100 |

### API Performance

| Metric | Description | Alert Threshold | Critical Threshold |
|--------|-------------|------------------|-------------------|
| Request Duration | Time to complete API requests | > 300ms avg | > 800ms avg |
| Error Rate | Percentage of requests resulting in errors | > 1% | > 5% |
| Rate Limit Hits | Number of rate limit errors | > 0 | > 5 per hour |
| Authentication Latency | Time to authenticate users | > 500ms avg | > 1000ms avg |

### Storage Performance

| Metric | Description | Alert Threshold | Critical Threshold |
|--------|-------------|------------------|-------------------|
| Storage Size | Total size of storage buckets | > 800MB (80% of free tier) | > 900MB (90% of free tier) |
| Upload Duration | Time to complete file uploads | > 2s avg for 1MB | > 5s avg for 1MB |
| Download Duration | Time to complete file downloads | > 1s avg for 1MB | > 3s avg for 1MB |

## Implementation

### Client-Side Monitoring

We implement monitoring within our application using the following services:

```typescript
// src/services/monitoring/SupabaseMonitor.ts
import { SupabaseClient } from '@supabase/supabase-js';
import { TelemetryService } from './TelemetryService';
import { ErrorReporter } from '../error/ErrorReporter';

export class SupabaseMonitor {
  private queryTimings: Map<string, number[]> = new Map();
  private realtimeMetrics: {
    connectionCount: number;
    channelCount: number;
    eventLatencies: number[];
    connectionDrops: number;
    lastConnectionDropTime: number;
  } = {
    connectionCount: 0,
    channelCount: 0,
    eventLatencies: [],
    connectionDrops: 0,
    lastConnectionDropTime: 0
  };
  private storageMetrics: {
    uploadDurations: Map<string, number[]>;
    downloadDurations: Map<string, number[]>;
  } = {
    uploadDurations: new Map(),
    downloadDurations: new Map()
  };
  
  constructor(
    private supabase: SupabaseClient,
    private telemetry: TelemetryService,
    private errorReporter: ErrorReporter,
    private options: {
      flushInterval?: number;
      samplingRate?: number;
      maxStoredTimings?: number;
    } = {}
  ) {
    // Set defaults
    this.options.flushInterval = this.options.flushInterval || 60000; // 1 minute
    this.options.samplingRate = this.options.samplingRate || 0.1; // 10% sampling
    this.options.maxStoredTimings = this.options.maxStoredTimings || 100;
    
    // Initialize monitoring
    this.initializeMonitoring();
  }
  
  /**
   * Set up realtime connection monitoring
   */
  private initializeMonitoring(): void {
    // Setup periodic flushing of metrics
    setInterval(() => this.flushMetrics(), this.options.flushInterval);
    
    // Monitor realtime connections
    this.monitorRealtimeConnections();
    
    // Set up interceptors for database queries
    this.setupQueryInterceptors();
  }
  
  /**
   * Track the performance of a database query
   * @param queryName Identifier for the query type
   * @param durationMs Time taken to execute the query in milliseconds
   */
  public trackQuery(queryName: string, durationMs: number): void {
    // Apply sampling to reduce telemetry volume
    if (Math.random() > this.options.samplingRate) {
      return;
    }
    
    if (!this.queryTimings.has(queryName)) {
      this.queryTimings.set(queryName, []);
    }
    
    const timings = this.queryTimings.get(queryName)!;
    timings.push(durationMs);
    
    // Keep array size under control
    if (timings.length > this.options.maxStoredTimings!) {
      timings.shift();
    }
    
    // Report immediately if it's a slow query
    if (durationMs > 1000) {
      this.telemetry.trackEvent('supabase.query.slow', {
        queryName,
        durationMs
      });
      
      this.errorReporter.captureMessage('Slow Supabase query detected', {
        queryName,
        durationMs,
        context: 'Database performance'
      });
    }
  }
  
  /**
   * Wrap a Supabase query with performance tracking
   * @param queryName Identifier for the query type
   * @param queryFn Function that performs the Supabase query
   */
  public async measureQuery<T>(
    queryName: string,
    queryFn: () => Promise<T>
  ): Promise<T> {
    const startTime = performance.now();
    try {
      const result = await queryFn();
      const duration = performance.now() - startTime;
      this.trackQuery(queryName, duration);
      return result;
    } catch (error) {
      const duration = performance.now() - startTime;
      this.trackQuery(`${queryName}:error`, duration);
      this.errorReporter.captureError(error, {
        context: 'Database query',
        queryName
      });
      throw error;
    }
  }
  
  /**
   * Track a realtime event's delivery latency
   * @param serverTimestamp Server timestamp when the event was generated
   */
  public trackRealtimeEventLatency(serverTimestamp: string): void {
    const serverTime = new Date(serverTimestamp).getTime();
    const clientTime = Date.now();
    const latencyMs = Math.max(0, clientTime - serverTime);
    
    this.realtimeMetrics.eventLatencies.push(latencyMs);
    
    // Keep array size under control
    if (this.realtimeMetrics.eventLatencies.length > this.options.maxStoredTimings!) {
      this.realtimeMetrics.eventLatencies.shift();
    }
    
    // Report immediately if it's a high latency event
    if (latencyMs > 1500) {
      this.telemetry.trackEvent('supabase.realtime.high_latency', {
        latencyMs
      });
    }
  }
  
  /**
   * Track realtime connection changes
   */
  private monitorRealtimeConnections(): void {
    if (this.supabase.realtime) {
      // Track connection state
      this.supabase.realtime.onOpen(() => {
        this.telemetry.trackEvent('supabase.realtime.connected');
      });
      
      this.supabase.realtime.onClose(() => {
        const now = Date.now();
        this.realtimeMetrics.connectionDrops++;
        
        // Only count as a drop if it's been at least 5 seconds since the last drop
        // This prevents counting rapid reconnection attempts as separate drops
        if (now - this.realtimeMetrics.lastConnectionDropTime > 5000) {
          this.realtimeMetrics.connectionDrops++;
          this.realtimeMetrics.lastConnectionDropTime = now;
          
          this.telemetry.trackEvent('supabase.realtime.disconnected');
        }
      });
      
      this.supabase.realtime.onError((error: any) => {
        this.errorReporter.captureError(error, {
          context: 'Realtime connection'
        });
        this.telemetry.trackEvent('supabase.realtime.error');
      });
    }
  }
  
  /**
   * Track a new channel subscription
   */
  public trackChannelSubscribed(channelName: string): void {
    this.realtimeMetrics.channelCount++;
    this.telemetry.trackEvent('supabase.realtime.channel_subscribed', {
      channelName
    });
  }
  
  /**
   * Track a channel unsubscription
   */
  public trackChannelUnsubscribed(channelName: string): void {
    this.realtimeMetrics.channelCount = Math.max(0, this.realtimeMetrics.channelCount - 1);
    this.telemetry.trackEvent('supabase.realtime.channel_unsubscribed', {
      channelName
    });
  }
  
  /**
   * Track storage upload performance
   */
  public trackStorageUpload(bucketName: string, fileSizeBytes: number, durationMs: number): void {
    const key = `${bucketName}:upload`;
    if (!this.storageMetrics.uploadDurations.has(key)) {
      this.storageMetrics.uploadDurations.set(key, []);
    }
    
    const normalizedDuration = durationMs / (fileSizeBytes / (1024 * 1024)); // Duration per MB
    this.storageMetrics.uploadDurations.get(key)!.push(normalizedDuration);
    
    // Keep array size under control
    if (this.storageMetrics.uploadDurations.get(key)!.length > this.options.maxStoredTimings!) {
      this.storageMetrics.uploadDurations.get(key)!.shift();
    }
    
    this.telemetry.trackMetric('supabase.storage.upload_duration_per_mb', normalizedDuration, {
      bucketName
    });
  }
  
  /**
   * Track storage download performance
   */
  public trackStorageDownload(bucketName: string, fileSizeBytes: number, durationMs: number): void {
    const key = `${bucketName}:download`;
    if (!this.storageMetrics.downloadDurations.has(key)) {
      this.storageMetrics.downloadDurations.set(key, []);
    }
    
    const normalizedDuration = durationMs / (fileSizeBytes / (1024 * 1024)); // Duration per MB
    this.storageMetrics.downloadDurations.get(key)!.push(normalizedDuration);
    
    // Keep array size under control
    if (this.storageMetrics.downloadDurations.get(key)!.length > this.options.maxStoredTimings!) {
      this.storageMetrics.downloadDurations.get(key)!.shift();
    }
    
    this.telemetry.trackMetric('supabase.storage.download_duration_per_mb', normalizedDuration, {
      bucketName
    });
  }
  
  /**
   * Set up query interceptors (if possible with your Supabase client version)
   */
  private setupQueryInterceptors(): void {
    // This is a simplified example - actual implementation would depend on
    // the specific Supabase client version and available hooks
    try {
      // Example of how you might hook into requests if available
      const originalFrom = this.supabase.from;
      this.supabase.from = (table: string) => {
        const result = originalFrom.call(this.supabase, table);
        
        // Wrap methods that perform database operations
        const wrapMethod = (obj: any, method: string) => {
          const original = obj[method];
          if (typeof original === 'function') {
            obj[method] = async (...args: any[]) => {
              return this.measureQuery(`${table}.${method}`, () => original.apply(obj, args));
            };
          }
        };
        
        // Wrap common query methods
        ['select', 'insert', 'update', 'delete', 'upsert'].forEach(method => {
          wrapMethod(result, method);
        });
        
        return result;
      };
    } catch (error) {
      console.warn('Failed to set up Supabase query interceptors:', error);
    }
  }
  
  /**
   * Flush collected metrics to telemetry service
   */
  private flushMetrics(): void {
    // Process query timings
    for (const [queryName, timings] of this.queryTimings.entries()) {
      if (timings.length > 0) {
        const avg = timings.reduce((sum, value) => sum + value, 0) / timings.length;
        const max = Math.max(...timings);
        
        this.telemetry.trackMetric(`supabase.query.duration.avg`, avg, { queryName });
        this.telemetry.trackMetric(`supabase.query.duration.max`, max, { queryName });
      }
    }
    
    // Process realtime metrics
    if (this.realtimeMetrics.eventLatencies.length > 0) {
      const avgLatency = this.realtimeMetrics.eventLatencies.reduce((sum, value) => sum + value, 0) / 
        this.realtimeMetrics.eventLatencies.length;
      
      this.telemetry.trackMetric('supabase.realtime.latency.avg', avgLatency);
      this.telemetry.trackMetric('supabase.realtime.connection_drops', this.realtimeMetrics.connectionDrops);
      this.telemetry.trackMetric('supabase.realtime.channel_count', this.realtimeMetrics.channelCount);
    }
    
    // Process storage metrics
    for (const [key, durations] of this.storageMetrics.uploadDurations.entries()) {
      if (durations.length > 0) {
        const avg = durations.reduce((sum, value) => sum + value, 0) / durations.length;
        this.telemetry.trackMetric(`supabase.storage.upload_duration_per_mb.avg`, avg, { bucket: key.split(':')[0] });
      }
    }
    
    for (const [key, durations] of this.storageMetrics.downloadDurations.entries()) {
      if (durations.length > 0) {
        const avg = durations.reduce((sum, value) => sum + value, 0) / durations.length;
        this.telemetry.trackMetric(`supabase.storage.download_duration_per_mb.avg`, avg, { bucket: key.split(':')[0] });
      }
    }
    
    // Clear stored metrics after flushing (or keep a small buffer)
    this.queryTimings.clear();
    this.realtimeMetrics.eventLatencies = [];
    this.realtimeMetrics.connectionDrops = 0;
    
    for (const key of this.storageMetrics.uploadDurations.keys()) {
      this.storageMetrics.uploadDurations.set(key, []);
    }
    
    for (const key of this.storageMetrics.downloadDurations.keys()) {
      this.storageMetrics.downloadDurations.set(key, []);
    }
  }
  
  /**
   * Check database size and connection limits
   */
  public async checkResourceUsage(): Promise<void> {
    try {
      // This requires a custom RPC function to be defined in Supabase
      const { data, error } = await this.supabase.rpc('get_db_resource_usage');
      
      if (error) throw error;
      
      if (data) {
        const { size_mb, connection_count } = data;
        
        // Track metrics
        this.telemetry.trackMetric('supabase.database.size_mb', size_mb);
        this.telemetry.trackMetric('supabase.database.connection_count', connection_count);
        
        // Check against thresholds
        const FREE_TIER_DB_SIZE_LIMIT_MB = 500;
        const FREE_TIER_CONNECTIONS_LIMIT = 50;
        
        const sizePercentage = (size_mb / FREE_TIER_DB_SIZE_LIMIT_MB) * 100;
        const connectionsPercentage = (connection_count / FREE_TIER_CONNECTIONS_LIMIT) * 100;
        
        if (sizePercentage > 80) {
          this.telemetry.trackEvent('supabase.database.size_warning', {
            size_mb,
            percentage: sizePercentage
          });
        }
        
        if (connectionsPercentage > 80) {
          this.telemetry.trackEvent('supabase.database.connections_warning', {
            connection_count,
            percentage: connectionsPercentage
          });
        }
      }
    } catch (error) {
      this.errorReporter.captureError(error, {
        context: 'Database resource usage check'
      });
    }
  }
}
```

### Telemetry Service Implementation

```typescript
// src/services/monitoring/TelemetryService.ts
export interface MetricOptions {
  [key: string]: string | number | boolean;
}

export class TelemetryService {
  constructor(
    private analyticsProvider: any, // Could be Application Insights, Google Analytics, etc.
    private options: {
      appVersion: string;
      environment: 'development' | 'staging' | 'production';
      enableDebugLogging?: boolean;
    }
  ) {}
  
  /**
   * Track a numeric metric
   */
  public trackMetric(
    name: string,
    value: number,
    properties: MetricOptions = {}
  ): void {
    // Add standard properties
    const enrichedProperties = {
      ...properties,
      appVersion: this.options.appVersion,
      environment: this.options.environment
    };
    
    // Log in development mode
    if (this.options.enableDebugLogging) {
      console.log(`[Metric] ${name}: ${value}`, enrichedProperties);
    }
    
    // Send to analytics provider
    try {
      this.analyticsProvider.trackMetric({
        name,
        value,
        properties: enrichedProperties
      });
    } catch (error) {
      console.error('Failed to track metric:', error);
    }
  }
  
  /**
   * Track an event
   */
  public trackEvent(
    name: string,
    properties: MetricOptions = {}
  ): void {
    // Add standard properties
    const enrichedProperties = {
      ...properties,
      appVersion: this.options.appVersion,
      environment: this.options.environment,
      timestamp: new Date().toISOString()
    };
    
    // Log in development mode
    if (this.options.enableDebugLogging) {
      console.log(`[Event] ${name}`, enrichedProperties);
    }
    
    // Send to analytics provider
    try {
      this.analyticsProvider.trackEvent({
        name,
        properties: enrichedProperties
      });
    } catch (error) {
      console.error('Failed to track event:', error);
    }
  }
}
```

### Database Monitoring Helper Functions for Supabase

We'll need to create these functions in Supabase SQL editor:

```sql
-- Function to get database resource usage (size and connections)
CREATE OR REPLACE FUNCTION public.get_db_resource_usage()
RETURNS TABLE (
  size_mb FLOAT,
  connection_count INT
) SECURITY DEFINER
AS $$
BEGIN
  RETURN QUERY
  WITH db_size AS (
    SELECT 
      pg_database_size(current_database()) / (1024 * 1024)::float AS size_mb
  ),
  connections AS (
    SELECT 
      COUNT(*) AS conn_count
    FROM 
      pg_stat_activity
    WHERE 
      datname = current_database()
  )
  SELECT 
    db_size.size_mb,
    connections.conn_count
  FROM 
    db_size, connections;
END;
$$ LANGUAGE plpgsql;

-- Function to get query performance statistics
CREATE OR REPLACE FUNCTION public.get_query_stats()
RETURNS TABLE (
  query_type TEXT,
  avg_exec_time FLOAT,
  calls INT,
  rows_per_call FLOAT
) SECURITY DEFINER
AS $$
BEGIN
  RETURN QUERY
  SELECT
    left(query, 50) AS query_type,
    mean_exec_time AS avg_exec_time,
    calls,
    rows_per_call
  FROM
    pg_stat_statements
  ORDER BY
    mean_exec_time DESC
  LIMIT 20;
END;
$$ LANGUAGE plpgsql;

-- Grant access to the functions
GRANT EXECUTE ON FUNCTION public.get_db_resource_usage() TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_db_resource_usage() TO service_role;
GRANT EXECUTE ON FUNCTION public.get_query_stats() TO service_role;
```

### Server-Side Monitoring

For monitoring on the server side, we'll use Azure Functions to periodically check Supabase health and performance:

```typescript
// Azure Function for periodic health checks
// /src/azure-functions/SupabaseHealthCheck/index.ts
import { AzureFunction, Context } from "@azure/functions";
import { createClient } from "@supabase/supabase-js";
import * as appInsights from "applicationinsights";

// Initialize Application Insights
appInsights.setup(process.env.APPLICATIONINSIGHTS_CONNECTION_STRING)
  .setAutoDependencyCorrelation(true)
  .setAutoCollectRequests(true)
  .setAutoCollectPerformance(true)
  .setAutoCollectExceptions(true)
  .setAutoCollectDependencies(true)
  .setAutoCollectConsole(true)
  .start();

const client = appInsights.defaultClient;

const timerTrigger: AzureFunction = async function (context: Context, myTimer: any): Promise<void> {
  const timeStamp = new Date().toISOString();
  
  context.log('Supabase health check function started at:', timeStamp);
  
  // Create Supabase client
  const supabase = createClient(
    process.env.SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_KEY!
  );
  
  try {
    // Measure database size and connection count
    const startTime = performance.now();
    const { data: resourceData, error: resourceError } = await supabase.rpc('get_db_resource_usage');
    const duration = performance.now() - startTime;
    
    if (resourceError) throw resourceError;
    
    // Track resource metrics
    if (resourceData) {
      client.trackMetric({ name: "supabase.database.size_mb", value: resourceData.size_mb });
      client.trackMetric({ name: "supabase.database.connection_count", value: resourceData.connection_count });
      client.trackMetric({ name: "supabase.database.query_response_time", value: duration });
      
      // Check thresholds
      const FREE_TIER_DB_SIZE_LIMIT_MB = 500;
      if (resourceData.size_mb > FREE_TIER_DB_SIZE_LIMIT_MB * 0.8) {
        context.log.warn(`Database size (${resourceData.size_mb}MB) approaching free tier limit (${FREE_TIER_DB_SIZE_LIMIT_MB}MB)`);
        
        client.trackEvent({
          name: "supabase.database.size_warning",
          properties: {
            size_mb: resourceData.size_mb,
            limit_mb: FREE_TIER_DB_SIZE_LIMIT_MB,
            percentage: (resourceData.size_mb / FREE_TIER_DB_SIZE_LIMIT_MB) * 100
          }
        });
      }
    }
    
    // Test database query performance
    await testQueryPerformance(supabase, context);
    
    // Check storage buckets
    await checkStorageBuckets(supabase, context);
    
    // Check authentication service
    await checkAuthService(supabase, context);
    
    context.log('Supabase health check completed successfully');
    
  } catch (error) {
    context.log.error('Supabase health check failed:', error);
    
    client.trackException({
      exception: error instanceof Error ? error : new Error(String(error)),
      properties: { source: "SupabaseHealthCheck" }
    });
  }
};

/**
 * Test database query performance
 */
async function testQueryPerformance(supabase: any, context: Context): Promise<void> {
  // Run a simple query and measure its performance
  const startTime = performance.now();
  
  const { data, error } = await supabase
    .from('courses')
    .select('id, name')
    .limit(10);
    
  const duration = performance.now() - startTime;
  
  if (error) {
    context.log.error('Query test failed:', error);
    throw error;
  }
  
  client.trackMetric({ name: "supabase.test_query.duration", value: duration });
  
  // Check if query is slow
  if (duration > 500) {
    context.log.warn(`Test query is slow: ${duration}ms`);
    
    client.trackEvent({
      name: "supabase.test_query.slow",
      properties: {
        duration_ms: duration
      }
    });
  }
  
  // Get query statistics if available
  try {
    const { data: statsData, error: statsError } = await supabase.rpc('get_query_stats');
    
    if (statsError) throw statsError;
    
    if (statsData && Array.isArray(statsData)) {
      statsData.forEach(stat => {
        client.trackMetric({
          name: "supabase.query_stats.avg_exec_time",
          value: stat.avg_exec_time,
          properties: {
            query_type: stat.query_type
          }
        });
      });
    }
  } catch (statsError) {
    context.log.error('Failed to get query statistics:', statsError);
  }
}

/**
 * Check storage buckets usage
 */
async function checkStorageBuckets(supabase: any, context: Context): Promise<void> {
  try {
    // List all buckets
    const { data: buckets, error: bucketsError } = await supabase.storage.listBuckets();
    
    if (bucketsError) throw bucketsError;
    
    // Track bucket count
    client.trackMetric({ name: "supabase.storage.bucket_count", value: buckets?.length || 0 });
    
    if (buckets && buckets.length > 0) {
      // Check each bucket
      for (const bucket of buckets) {
        const startTime = performance.now();
        
        // List files to check response time
        const { data: files, error: filesError } = await supabase.storage.from(bucket.name).list();
        
        const duration = performance.now() - startTime;
        
        if (filesError) {
          context.log.error(`Error listing files in bucket ${bucket.name}:`, filesError);
          continue;
        }
        
        const fileCount = files?.length || 0;
        
        client.trackMetric({
          name: "supabase.storage.bucket_file_count",
          value: fileCount,
          properties: { bucket: bucket.name }
        });
        
        client.trackMetric({
          name: "supabase.storage.list_response_time",
          value: duration,
          properties: { bucket: bucket.name }
        });
      }
    }
  } catch (error) {
    context.log.error('Storage bucket check failed:', error);
    throw error;
  }
}

/**
 * Check authentication service
 */
async function checkAuthService(supabase: any, context: Context): Promise<void> {
  try {
    // Test auth service response time (just check if it's available)
    const startTime = performance.now();
    
    const { data, error } = await supabase.auth.getSession();
    
    const duration = performance.now() - startTime;
    
    client.trackMetric({ name: "supabase.auth.response_time", value: duration });
    
    if (error) {
      context.log.error('Auth service check failed:', error);
      
      client.trackEvent({
        name: "supabase.auth.error",
        properties: {
          error_message: error.message
        }
      });
      
      throw error;
    }
    
    context.log('Auth service check completed successfully');
    
  } catch (error) {
    context.log.error('Auth service check failed:', error);
    throw error;
  }
}

export default timerTrigger;
```

### Azure Function Integration

For deployment, you'll need to set up a `function.json` file for the Azure Function:

```json
{
  "bindings": [
    {
      "name": "myTimer",
      "type": "timerTrigger",
      "direction": "in",
      "schedule": "0 */5 * * * *" // Run every 5 minutes
    }
  ],
  "scriptFile": "../dist/SupabaseHealthCheck/index.js"
}
```

## Dashboard Setup

For monitoring dashboards, we'll use Application Insights (since you're already using Azure):

1. **Real-time Overview Dashboard**: Core metrics with minute-by-minute updates
2. **Performance Dashboard**: Detailed query and API performance metrics
3. **Resource Usage Dashboard**: Database and storage size monitoring, connection counts
4. **Error Dashboard**: Aggregated errors with context and frequency

### Example Application Insights Dashboard Setup (Azure Portal)

1. In the Azure Portal, navigate to your Application Insights resource
2. Select "Dashboards" and create a new dashboard
3. Add the following tiles to your dashboard:

**Query Performance Tile:**
```kusto
customMetrics
| where name startswith "supabase.query.duration"
| summarize avg(value) by name, queryName=tostring(customDimensions.queryName)
| extend queryType = iif(name endswith ".avg", "Average", "Maximum")
| project queryName, queryType, duration=avg_value
| sort by duration desc
```

**Database Size Tile:**
```kusto
customMetrics
| where name == "supabase.database.size_mb"
| summarize avg(value) by bin(timestamp, 1h)
| render timechart
```

**Connection Count Tile:**
```kusto
customMetrics
| where name == "supabase.database.connection_count"
| summarize avg(value) by bin(timestamp, 5m)
| render timechart
```

**Error Rate Tile:**
```kusto
exceptions
| where customDimensions.source == "SupabaseHealthCheck"
| summarize count() by bin(timestamp, 1h)
| render columnchart
```

**Realtime Latency Tile:**
```kusto
customMetrics
| where name == "supabase.realtime.latency.avg"
| summarize avg(value) by bin(timestamp, 5m)
| render timechart
```

## Alert Configuration

Set up the following alerts to proactively monitor your Supabase instance:

### Database Size Alert

```kusto
customMetrics
| where name == "supabase.database.size_mb"
| summarize avg(value) by bin(timestamp, 5m)
| where avg_value > 400 // 80% of free tier
```

### Query Performance Alert

```kusto
customMetrics
| where name startswith "supabase.query.duration.avg"
| summarize avg(value) by bin(timestamp, 5m)
| where avg_value > 500 // 500ms threshold
```

### Connection Count Alert

```kusto
customMetrics
| where name == "supabase.database.connection_count"
| summarize avg(value) by bin(timestamp, 5m)
| where avg_value > 40 // 80% of free tier
```

### Error Rate Alert

```kusto
exceptions
| where timestamp > ago(1h)
| where customDimensions.source == "SupabaseHealthCheck"
| summarize count() by bin(timestamp, 5m)
| where count_ > 5
```

## Free Tier Considerations

Special monitoring considerations for the Supabase free tier:

1. **Database Size**: Free tier is limited to 500 MB. Set alerts at 400 MB (80%).
2. **Connection Limits**: Free tier is limited to 50 concurrent connections. Monitor this closely.
3. **Storage**: Free tier has 1 GB storage limit. Track usage to avoid unexpected limitations.
4. **Row Limits**: No explicit row limits, but query performance will degrade with large tables.

### Optimizing Free Tier Usage

```typescript
// src/services/monitoring/FreeTierOptimizer.ts
export class FreeTierOptimizer {
  private USAGE_CHECK_INTERVAL_MS = 3600000; // 1 hour
  
  constructor(
    private supabase: SupabaseClient,
    private telemetry: TelemetryService,
    private errorReporter: ErrorReporter
  ) {
    // Start periodic checks
    this.startPeriodicChecks();
  }
  
  private startPeriodicChecks(): void {
    setInterval(() => this.checkResourceUsage(), this.USAGE_CHECK_INTERVAL_MS);
  }
  
  public async checkResourceUsage(): Promise<void> {
    try {
      const { data, error } = await this.supabase.rpc('get_db_resource_usage');
      
      if (error) throw error;
      
      if (data) {
        // Check database size
        const FREE_TIER_DB_SIZE_LIMIT_MB = 500;
        const sizePercentage = (data.size_mb / FREE_TIER_DB_SIZE_LIMIT_MB) * 100;
        
        if (sizePercentage > 90) {
          // Critical - need to take action
          this.telemetry.trackEvent('supabase.free_tier.db_size_critical', {
            size_mb: data.size_mb,
            percentage: sizePercentage
          });
          
          // Trigger cleanup suggestions
          this.suggestCleanupActions();
        } else if (sizePercentage > 80) {
          // Warning level
          this.telemetry.trackEvent('supabase.free_tier.db_size_warning', {
            size_mb: data.size_mb,
            percentage: sizePercentage
          });
        }
        
        // Check connection count
        const FREE_TIER_CONNECTIONS_LIMIT = 50;
        const connectionsPercentage = (data.connection_count / FREE_TIER_CONNECTIONS_LIMIT) * 100;
        
        if (connectionsPercentage > 80) {
          this.telemetry.trackEvent('supabase.free_tier.connections_warning', {
            connections: data.connection_count,
            percentage: connectionsPercentage
          });
        }
      }
    } catch (error) {
      this.errorReporter.captureError(error, {
        context: 'Free tier resource check'
      });
    }
  }
  
  private async suggestCleanupActions(): Promise<void> {
    try {
      // Find largest tables
      const { data: tableStats } = await this.supabase.rpc('get_table_sizes');
      
      if (tableStats && Array.isArray(tableStats)) {
        // Report largest tables
        this.telemetry.trackEvent('supabase.free_tier.largest_tables', {
          tables: tableStats.slice(0, 3).map(t => `${t.table_name}: ${t.size_mb}MB`).join(', ')
        });
        
        // Find tables with old data that could be archived
        const { data: oldData } = await this.supabase.rpc('get_old_data_stats');
        
        if (oldData) {
          this.telemetry.trackEvent('supabase.free_tier.archivable_data', {
            data: JSON.stringify(oldData)
          });
        }
      }
    } catch (error) {
      this.errorReporter.captureError(error, {
        context: 'Free tier cleanup suggestions'
      });
    }
  }
}
```

### Additional SQL functions for free tier optimization

```sql
-- Function to get table sizes
CREATE OR REPLACE FUNCTION public.get_table_sizes()
RETURNS TABLE (
  table_name TEXT,
  size_mb FLOAT,
  row_count BIGINT
) SECURITY DEFINER
AS $$
BEGIN
  RETURN QUERY
  SELECT
    c.relname::TEXT AS table_name,
    pg_total_relation_size(c.oid) / (1024 * 1024)::FLOAT AS size_mb,
    c.reltuples::BIGINT AS row_count
  FROM
    pg_class c
  JOIN
    pg_namespace n ON n.oid = c.relnamespace
  WHERE
    n.nspname = 'public'
    AND c.relkind = 'r'
  ORDER BY
    pg_total_relation_size(c.oid) DESC;
END;
$$ LANGUAGE plpgsql;

-- Function to find old data that could be archived
CREATE OR REPLACE FUNCTION public.get_old_data_stats()
RETURNS TABLE (
  table_name TEXT,
  old_rows BIGINT,
  percentage FLOAT
) SECURITY DEFINER
AS $$
BEGIN
  -- This is an example that would need to be customized for your schema
  -- It assumes tables have a created_at or updated_at timestamp column
  RETURN QUERY
  
  WITH old_data AS (
    -- Example for scores table - older than 60 days
    SELECT
      'scores' AS table_name,
      COUNT(*) AS old_count,
      (SELECT COUNT(*) FROM scores) AS total_count
    FROM
      scores
    WHERE
      created_at < (CURRENT_DATE - INTERVAL '60 days')
    
    UNION ALL
    
    -- Example for tournaments table - older than 90 days
    SELECT
      'tournaments' AS table_name,
      COUNT(*) AS old_count,
      (SELECT COUNT(*) FROM tournaments) AS total_count
    FROM
      tournaments
    WHERE
      created_at < (CURRENT_DATE - INTERVAL '90 days')
  )
  
  SELECT
    table_name,
    old_count AS old_rows,
    CASE 
      WHEN total_count > 0 THEN (old_count::FLOAT / total_count::FLOAT) * 100
      ELSE 0
    END AS percentage
  FROM
    old_data
  WHERE
    old_count > 0
  ORDER BY
    old_count DESC;
END;
$$ LANGUAGE plpgsql;

-- Grant access to the functions
GRANT EXECUTE ON FUNCTION public.get_table_sizes() TO service_role;
GRANT EXECUTE ON FUNCTION public.get_old_data_stats() TO service_role;
```

## Periodic Health Checks

In addition to automated monitoring, perform these periodic manual health checks:

### Weekly Health Check

1. **Database Performance Review**:
   - Review slow queries logged in the past week
   - Check for tables that need optimization or indexing
   - Verify connection usage patterns and identify potential pooling issues

2. **Storage Analysis**:
   - Review storage growth trends
   - Check for large or unused files
   - Verify bucket permissions are correct

3. **Error Analysis**:
   - Review aggregated errors
   - Look for patterns in error types or user reports
   - Prioritize fixes based on impact

### Monthly Usage Review

1. **Growth Projections**:
   - Analyze database size growth rate
   - Project when you might hit free tier limits
   - Plan for potential upgrades or optimizations

2. **Performance Optimization**:
   - Review most expensive queries
   - Identify opportunities for caching
   - Check for unnecessary real-time subscriptions

3. **Security Audit**:
   - Review RLS policies
   - Check for any abnormal access patterns
   - Verify authentication settings

This comprehensive monitoring strategy ensures that we maintain optimal performance for our Supabase instance while staying within the free tier limits. It provides early warning for potential issues and helps us plan appropriately for growth.

## Load Testing Strategy

Testing our real-time features under load is essential to ensure the application can handle concurrent users while staying within Supabase's free tier constraints. This section outlines our approach to load testing.

### Test Environment Setup

```typescript
// src/tests/loadTests/SupabaseLoadTestEnvironment.ts
import { createClient } from '@supabase/supabase-js';
import { v4 as uuidv4 } from 'uuid';

export class SupabaseLoadTestEnvironment {
  private supabaseUrl: string;
  private supabaseAnonKey: string;
  private testUsers: Array<{email: string, password: string, id: string}> = [];
  
  constructor(
    supabaseUrl: string,
    supabaseAnonKey: string,
    private options: {
      testUserPrefix?: string;
      numberOfTestUsers?: number;
    } = {}
  ) {
    this.supabaseUrl = supabaseUrl;
    this.supabaseAnonKey = supabaseAnonKey;
    
    // Set defaults
    this.options.testUserPrefix = this.options.testUserPrefix || 'loadtest_user';
    this.options.numberOfTestUsers = this.options.numberOfTestUsers || 50;
  }
  
  /**
   * Create test users for load testing
   */
  public async setupTestUsers(): Promise<void> {
    const supabase = createClient(this.supabaseUrl, this.supabaseAnonKey);
    
    console.log(`Creating ${this.options.numberOfTestUsers} test users...`);
    
    // Create test users in batches to avoid rate limits
    const batchSize = 5;
    for (let i = 0; i < this.options.numberOfTestUsers!; i += batchSize) {
      const batch = [];
      
      for (let j = 0; j < batchSize && (i + j) < this.options.numberOfTestUsers!; j++) {
        const userIndex = i + j;
        const email = `${this.options.testUserPrefix}${userIndex}@example.com`;
        const password = `LoadTest123!${uuidv4().substring(0, 8)}`;
        
        batch.push(this.createTestUser(supabase, email, password));
      }
      
      // Wait for all users in the batch to be created
      const results = await Promise.allSettled(batch);
      
      // Process results
      results.forEach((result, index) => {
        if (result.status === 'fulfilled' && result.value) {
          this.testUsers.push(result.value);
        }
      });
      
      // Small delay between batches to avoid rate limits
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    
    console.log(`Successfully created ${this.testUsers.length} test users`);
  }
  
  /**
   * Create a single test user
   */
  private async createTestUser(supabase: any, email: string, password: string): Promise<{email: string, password: string, id: string} | null> {
    try {
      const { data, error } = await supabase.auth.signUp({
        email,
        password,
      });
      
      if (error) throw error;
      
      return {
        email,
        password,
        id: data.user.id
      };
    } catch (error) {
      console.error(`Error creating test user ${email}:`, error);
      return null;
    }
  }
  
  /**
   * Setup test tournament for load testing
   */
  public async setupTestTournament(): Promise<string> {
    const supabase = createClient(this.supabaseUrl, this.supabaseAnonKey);
    
    // Create a test tournament
    const tournamentId = uuidv4();
    const tournamentName = `Load Test Tournament ${new Date().toISOString()}`;
    
    try {
      // Sign in as the first test user (organizer)
      if (this.testUsers.length === 0) {
        throw new Error('No test users available. Call setupTestUsers first.');
      }
      
      const organizer = this.testUsers[0];
      const { error: signInError } = await supabase.auth.signInWithPassword({
        email: organizer.email,
        password: organizer.password
      });
      
      if (signInError) throw signInError;
      
      // Create a test tournament
      const { error: tournamentError } = await supabase
        .from('tournaments')
        .insert({
          id: tournamentId,
          name: tournamentName,
          start_date: new Date().toISOString(),
          end_date: new Date(Date.now() + 86400000).toISOString(), // Tomorrow
          created_by: organizer.id
        });
      
      if (tournamentError) throw tournamentError;
      
      return tournamentId;
    } catch (error) {
      console.error('Error setting up test tournament:', error);
      throw error;
    }
  }
  
  /**
   * Clean up test resources
   */
  public async cleanup(): Promise<void> {
    console.log('Cleaning up test resources');
    // Implementation would depend on your cleanup needs
  }
}
```

### Virtual User Simulation

```typescript
// src/tests/loadTests/VirtualUser.ts
import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { LoadTestMetricsCollector } from './LoadTestMetricsCollector';

export class VirtualUser {
  private supabase: SupabaseClient;
  private isAuthenticated: boolean = false;
  private activeSubscriptions: any[] = [];
  private metricsCollector: LoadTestMetricsCollector;
  
  constructor(
    private supabaseUrl: string,
    private supabaseAnonKey: string,
    private credentials: {email: string, password: string},
    metricsCollector: LoadTestMetricsCollector
  ) {
    this.supabase = createClient(supabaseUrl, supabaseAnonKey);
    this.metricsCollector = metricsCollector;
  }
  
  /**
   * Authenticate the virtual user
   */
  public async authenticate(): Promise<boolean> {
    const startTime = performance.now();
    
    try {
      const { data, error } = await this.supabase.auth.signInWithPassword({
        email: this.credentials.email,
        password: this.credentials.password
      });
      
      const duration = performance.now() - startTime;
      this.metricsCollector.recordMetric('auth_duration_ms', duration);
      
      if (error) {
        this.metricsCollector.recordError('auth_error', error);
        return false;
      }
      
      this.isAuthenticated = true;
      return true;
    } catch (error) {
      const duration = performance.now() - startTime;
      this.metricsCollector.recordMetric('auth_duration_ms', duration);
      this.metricsCollector.recordError('auth_error', error);
      return false;
    }
  }
  
  /**
   * Subscribe to a tournament's leaderboard updates
   */
  public async subscribeToLeaderboard(tournamentId: string): Promise<void> {
    if (!this.isAuthenticated) {
      throw new Error('User must be authenticated before subscribing');
    }
    
    const startTime = performance.now();
    
    try {
      // Subscribe to the leaderboard channel
      const subscription = this.supabase
        .channel(`leaderboard:${tournamentId}`)
        .on('postgres_changes', {
          event: '*',
          schema: 'public',
          table: 'leaderboard',
          filter: `tournament_id=eq.${tournamentId}`
        }, (payload) => {
          // Record event latency
          const serverTime = new Date(payload.commit_timestamp).getTime();
          const clientTime = Date.now();
          const latency = Math.max(0, clientTime - serverTime);
          
          this.metricsCollector.recordMetric('realtime_latency_ms', latency);
        })
        .subscribe((status) => {
          const subscriptionTime = performance.now() - startTime;
          this.metricsCollector.recordMetric('subscription_setup_ms', subscriptionTime);
          
          if (status === 'SUBSCRIBED') {
            this.metricsCollector.recordEvent('subscription_success');
          } else {
            this.metricsCollector.recordEvent('subscription_failure', { status });
          }
        });
      
      this.activeSubscriptions.push(subscription);
    } catch (error) {
      this.metricsCollector.recordError('subscription_error', error);
      throw error;
    }
  }
  
  /**
   * Simulate a user viewing multiple pages/features
   */
  public async simulateUserJourney(tournamentId: string): Promise<void> {
    try {
      // 1. Authenticate
      const authSuccess = await this.authenticate();
      if (!authSuccess) {
        throw new Error('Authentication failed');
      }
      
      // 2. Subscribe to leaderboard
      await this.subscribeToLeaderboard(tournamentId);
      
      // 3. Simulate browsing behavior
      await this.simulateBrowsing(tournamentId);
      
      // 4. Simulate occasional data updates
      if (Math.random() < 0.3) { // 30% chance to update data
        await this.simulateDataUpdate(tournamentId);
      }
    } catch (error) {
      this.metricsCollector.recordError('user_journey_error', error);
    }
  }
  
  /**
   * Simulate user browsing behavior
   */
  private async simulateBrowsing(tournamentId: string): Promise<void> {
    // Simulate viewing different pages/data
    const actions = [
      () => this.fetchTournamentDetails(tournamentId),
      () => this.fetchLeaderboard(tournamentId),
      () => this.fetchPlayerDetails(tournamentId),
    ];
    
    // Execute 1-3 random actions
    const numActions = 1 + Math.floor(Math.random() * 3);
    for (let i = 0; i < numActions; i++) {
      const randomAction = actions[Math.floor(Math.random() * actions.length)];
      await randomAction();
      
      // Simulate user think time between actions (1-5 seconds)
      const thinkTime = 1000 + Math.floor(Math.random() * 4000);
      await new Promise(resolve => setTimeout(resolve, thinkTime));
    }
  }
  
  /**
   * Fetch tournament details
   */
  private async fetchTournamentDetails(tournamentId: string): Promise<void> {
    const startTime = performance.now();
    
    try {
      const { data, error } = await this.supabase
        .from('tournaments')
        .select('*')
        .eq('id', tournamentId)
        .single();
      
      const duration = performance.now() - startTime;
      this.metricsCollector.recordMetric('tournament_details_query_ms', duration);
      
      if (error) {
        this.metricsCollector.recordError('tournament_details_error', error);
      }
    } catch (error) {
      this.metricsCollector.recordError('tournament_details_error', error);
    }
  }
  
  /**
   * Fetch leaderboard data
   */
  private async fetchLeaderboard(tournamentId: string): Promise<void> {
    const startTime = performance.now();
    
    try {
      const { data, error } = await this.supabase
        .from('leaderboard')
        .select('*')
        .eq('tournament_id', tournamentId)
        .order('position', { ascending: true });
      
      const duration = performance.now() - startTime;
      this.metricsCollector.recordMetric('leaderboard_query_ms', duration);
      
      if (error) {
        this.metricsCollector.recordError('leaderboard_query_error', error);
      }
    } catch (error) {
      this.metricsCollector.recordError('leaderboard_query_error', error);
    }
  }
  
  /**
   * Fetch player details
   */
  private async fetchPlayerDetails(tournamentId: string): Promise<void> {
    // Implementation details omitted for brevity
  }
  
  /**
   * Simulate a data update (e.g., submitting a score)
   */
  private async simulateDataUpdate(tournamentId: string): Promise<void> {
    const startTime = performance.now();
    
    try {
      // Implementation omitted for brevity
      // Would generate and submit random scoring data
      
      const duration = performance.now() - startTime;
      this.metricsCollector.recordMetric('score_update_ms', duration);
    } catch (error) {
      this.metricsCollector.recordError('score_update_error', error);
    }
  }
  
  /**
   * Clean up subscriptions
   */
  public cleanup(): void {
    // Unsubscribe from all active subscriptions
    this.activeSubscriptions.forEach(subscription => {
      try {
        if (subscription && typeof subscription.unsubscribe === 'function') {
          subscription.unsubscribe();
        }
      } catch (error) {
        this.metricsCollector.recordError('unsubscribe_error', error);
      }
    });
    
    this.activeSubscriptions = [];
    
    // Sign out
    try {
      this.supabase.auth.signOut();
    } catch (error) {
      this.metricsCollector.recordError('signout_error', error);
    }
  }
}
```

### Metrics Collection

```typescript
// src/tests/loadTests/LoadTestMetricsCollector.ts
export class LoadTestMetricsCollector {
  private metrics: Map<string, number[]> = new Map();
  private events: Map<string, {count: number, details: any[]}> = new Map();
  private errors: Map<string, {count: number, details: any[]}> = new Map();
  private testStart: number = 0;
  private testEnd: number = 0;
  
  /**
   * Start the load test timer
   */
  public startTest(): void {
    this.testStart = Date.now();
  }
  
  /**
   * Record the end of the test
   */
  public endTest(): void {
    this.testEnd = Date.now();
  }
  
  /**
   * Record a numeric metric
   */
  public recordMetric(name: string, value: number): void {
    if (!this.metrics.has(name)) {
      this.metrics.set(name, []);
    }
    
    this.metrics.get(name)!.push(value);
  }
  
  /**
   * Record an event occurrence
   */
  public recordEvent(name: string, details: any = {}): void {
    if (!this.events.has(name)) {
      this.events.set(name, {count: 0, details: []});
    }
    
    const event = this.events.get(name)!;
    event.count++;
    event.details.push({
      timestamp: Date.now(),
      ...details
    });
  }
  
  /**
   * Record an error
   */
  public recordError(category: string, error: any): void {
    if (!this.errors.has(category)) {
      this.errors.set(category, {count: 0, details: []});
    }
    
    const errorRecord = this.errors.get(category)!;
    errorRecord.count++;
    errorRecord.details.push({
      timestamp: Date.now(),
      message: error.message || String(error),
      stack: error.stack,
      details: error
    });
  }
  
  /**
   * Generate report
   */
  public generateReport(): object {
    return {
      testDuration: this.getTestDuration(),
      timestamp: new Date().toISOString(),
      metrics: this.getMetricsSummary(),
      events: this.getEventsSummary(),
      errors: this.getErrorsSummary()
    };
  }
  
  // Other helper methods omitted for brevity
}
```

### Load Test Orchestrator

```typescript
// src/tests/loadTests/SupabaseLoadTester.ts
export class SupabaseLoadTester {
  private metricsCollector: LoadTestMetricsCollector;
  private users: VirtualUser[] = [];
  private testEnvironment: SupabaseLoadTestEnvironment;
  private testTournamentId: string = '';
  
  constructor(
    private supabaseUrl: string,
    private supabaseAnonKey: string,
    private options: {
      concurrentUsers?: number;
      rampUpPeriodSeconds?: number;
      testDurationSeconds?: number;
      userActionsPerSecond?: number;
    } = {}
  ) {
    // Set defaults
    this.options.concurrentUsers = this.options.concurrentUsers || 50;
    this.options.rampUpPeriodSeconds = this.options.rampUpPeriodSeconds || 30;
    this.options.testDurationSeconds = this.options.testDurationSeconds || 300; // 5 minutes
    this.options.userActionsPerSecond = this.options.userActionsPerSecond || 5;
    
    this.metricsCollector = new LoadTestMetricsCollector();
    this.testEnvironment = new SupabaseLoadTestEnvironment(
      supabaseUrl,
      supabaseAnonKey,
      {
        numberOfTestUsers: this.options.concurrentUsers
      }
    );
  }
  
  /**
   * Setup the load test environment
   */
  public async setup(): Promise<void> {
    console.log('Setting up load test environment...');
    
    // Create test users
    await this.testEnvironment.setupTestUsers();
    
    // Create test tournament
    this.testTournamentId = await this.testEnvironment.setupTestTournament();
    
    // Create virtual users
    const testUsers = this.testEnvironment.getTestUsers();
    
    for (let i = 0; i < Math.min(this.options.concurrentUsers!, testUsers.length); i++) {
      this.users.push(new VirtualUser(
        this.supabaseUrl,
        this.supabaseAnonKey,
        testUsers[i],
        this.metricsCollector
      ));
    }
  }
  
  /**
   * Run the load test
   */
  public async runTest(): Promise<object> {
    console.log('Starting load test...');
    this.metricsCollector.startTest();
    
    // Ramp up users
    const userRampUpDelay = (this.options.rampUpPeriodSeconds! * 1000) / this.users.length;
    
    // Start each user with a delay
    for (let i = 0; i < this.users.length; i++) {
      setTimeout(() => {
        this.users[i].authenticate()
          .then(() => this.users[i].subscribeToLeaderboard(this.testTournamentId))
          .catch(error => console.error(`Failed to initialize user ${i}:`, error));
      }, i * userRampUpDelay);
    }
    
    // Run test actions at the specified rate
    const actionInterval = 1000 / this.options.userActionsPerSecond!;
    let keepRunning = true;
    
    setTimeout(() => {
      keepRunning = false;
    }, this.options.testDurationSeconds! * 1000);
    
    // Run user actions until test duration is complete
    while (keepRunning) {
      // Select random user
      const randomUserIndex = Math.floor(Math.random() * this.users.length);
      
      // Run action
      this.users[randomUserIndex].simulateUserJourney(this.testTournamentId)
        .catch(error => console.error(`Action failed for user ${randomUserIndex}:`, error));
      
      // Wait for next action
      await new Promise(resolve => setTimeout(resolve, actionInterval));
    }
    
    // Give time for final operations to complete
    await new Promise(resolve => setTimeout(resolve, 5000));
    
    this.metricsCollector.endTest();
    
    // Generate report
    return this.metricsCollector.generateReport();
  }
  
  /**
   * Clean up after the test
   */
  public async cleanup(): Promise<void> {
    // Clean up virtual users
    for (const user of this.users) {
      user.cleanup();
    }
    
    // Clean up test environment
    await this.testEnvironment.cleanup();
  }
}
```

### Test Scenarios

We'll run the following test scenarios to validate our real-time features:

#### 1. Baseline Performance

Objective: Establish baseline performance metrics with a small number of users.

Parameters:
- 10-20 concurrent users
- 2-minute test duration
- Focus on latency and error rates

#### 2. Tournament Scoring Simulation

Objective: Test performance during active tournament scoring.

Parameters:
- 20-30 concurrent users
- 50% of users only viewing (subscribed to events)
- 50% of users submitting scores regularly
- 5-minute test duration

#### 3. Free Tier Connection Limit Test

Objective: Verify behavior near the Supabase free tier connection limit.

Parameters:
- Gradually increase users to 45-50 (near the 50 connection limit)
- Monitor connection errors and service degradation
- Focus on connection recovery behavior

#### 4. Bursting Traffic Pattern

Objective: Test application performance during sudden traffic spikes.

Parameters:
- Start with 10 users
- Spike to 40 users within 30 seconds
- Maintain for 2 minutes
- Gradually decrease back to 10 users

#### 5. Long-Running Tournament Simulation

Objective: Test performance and stability during extended tournaments.

Parameters:
- 25 concurrent users (mixed roles)
- 30-minute test duration
- Simulated connectivity issues (random disconnects)
- Focus on subscription recovery and data consistency

#### 6. Multi-Tournament Concurrent Testing 

Objective: Test system behavior with users spread across multiple tournaments.

Parameters:
- 40 total concurrent users
- 4 separate tournaments with 10 users each
- Mixed user behaviors (viewing, scoring, admin)
- Focus on channel isolation and cross-tournament performance impacts

#### 7. Subscription Latency Under Load

Objective: Measure real-time subscription setup time and event delivery latency under various loads.

Parameters:
- Progressive load from 5 to 45 users
- Each user rapidly subscribing/unsubscribing to channels
- Measure event propagation time at each load level
- Identify latency inflection points

### Integration with CI/CD Pipeline

We've integrated our load testing strategy with our CI/CD pipeline to ensure performance regressions are caught early:

```yaml
# .github/workflows/load-test.yml
name: Supabase Load Testing

on:
  workflow_dispatch:  # Manual trigger
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sundays at midnight UTC

jobs:
  load-test:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'
          
      - name: Install dependencies
        run: npm ci
        
      - name: Run load tests
        run: npm run load-test
        env:
          SUPABASE_URL: ${{ secrets.SUPABASE_URL }}
          SUPABASE_ANON_KEY: ${{ secrets.SUPABASE_ANON_KEY }}
          
      - name: Upload test results
        uses: actions/upload-artifact@v3
        with:
          name: load-test-results
          path: load-test-results.json
          
      - name: Check performance thresholds
        run: npm run verify-load-test
```

The `verify-load-test` script checks performance metrics against our thresholds:

```typescript
// scripts/verify-load-test.js
const fs = require('fs');

// Load test results
const results = JSON.parse(fs.readFileSync('./load-test-results.json'));

// Define thresholds
const thresholds = {
  realtime_latency_ms: {
    p95: 800,  // 95th percentile should be under 800ms
    max: 2000  // Maximum allowed latency
  },
  connection_drops: {
    total: 5   // No more than 5 connection drops during test
  },
  auth_duration_ms: {
    avg: 1000  // Average auth time under 1 second
  }
};

// Check if thresholds are exceeded
let failed = false;

// Check latency
if (results.metrics.realtime_latency_ms.p95 > thresholds.realtime_latency_ms.p95) {
  console.error(`FAILED: 95th percentile latency (${results.metrics.realtime_latency_ms.p95}ms) exceeds threshold (${thresholds.realtime_latency_ms.p95}ms)`);
  failed = true;
}

// Other checks...

process.exit(failed ? 1 : 0);
```

### Optimization Strategies Based on Test Results

After conducting load tests, we'll implement the following optimizations if needed:

#### 1. Connection Pooling

To manage the 50-connection free tier limit:

```typescript
// src/services/SupabaseConnectionManager.ts
export class SupabaseConnectionManager {
  private static instance: SupabaseConnectionManager;
  private channelSubscriptions: Map<string, {
    channel: any,
    subscribers: Set<string>,
    lastActivity: number
  }> = new Map();
  
  private constructor() {}
  
  /**
   * Get singleton instance
   */
  public static getInstance(): SupabaseConnectionManager {
    if (!SupabaseConnectionManager.instance) {
      SupabaseConnectionManager.instance = new SupabaseConnectionManager();
    }
    return SupabaseConnectionManager.instance;
  }
  
  /**
   * Subscribe to a channel with connection sharing
   */
  public subscribeToChannel(
    supabase: any,
    channelKey: string,
    eventConfig: any,
    callback: (payload: any) => void
  ): string {
    // Generate unique subscriber ID
    const subscriberId = `sub_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    
    // Check if channel already exists
    if (!this.channelSubscriptions.has(channelKey)) {
      // Create new channel
      const channel = supabase
        .channel(channelKey)
        .on('postgres_changes', eventConfig, (payload: any) => {
          // Get all subscribers for this channel
          const subscription = this.channelSubscriptions.get(channelKey);
          if (!subscription) return;
          
          // Update last activity
          subscription.lastActivity = Date.now();
          
          // Notify all subscribers
          for (const subscriberCallback of this.getSubscriberCallbacks(channelKey)) {
            try {
              subscriberCallback(payload);
            } catch (error) {
              console.error('Error in subscriber callback:', error);
            }
          }
        })
        .subscribe();
      
      // Store the channel
      this.channelSubscriptions.set(channelKey, {
        channel,
        subscribers: new Set([subscriberId]),
        lastActivity: Date.now()
      });
    } else {
      // Add subscriber to existing channel
      const subscription = this.channelSubscriptions.get(channelKey)!;
      subscription.subscribers.add(subscriberId);
      subscription.lastActivity = Date.now();
    }
    
    // Store the callback
    this.setSubscriberCallback(channelKey, subscriberId, callback);
    
    return subscriberId;
  }
  
  /**
   * Unsubscribe a specific subscriber
   */
  public unsubscribe(channelKey: string, subscriberId: string): void {
    if (!this.channelSubscriptions.has(channelKey)) return;
    
    const subscription = this.channelSubscriptions.get(channelKey)!;
    
    // Remove subscriber
    subscription.subscribers.delete(subscriberId);
    this.removeSubscriberCallback(channelKey, subscriberId);
    
    // If no subscribers left, unsubscribe and remove channel
    if (subscription.subscribers.size === 0) {
      try {
        subscription.channel.unsubscribe();
      } catch (error) {
        console.error('Error unsubscribing from channel:', error);
      }
      
      this.channelSubscriptions.delete(channelKey);
    }
  }
  
  // Helper methods omitted for brevity
}
```

#### 2. Selective Realtime Subscriptions

To reduce the number of active realtime channels:

```typescript
// src/services/LeaderboardService.ts
export class LeaderboardService {
  private connectionManager = SupabaseConnectionManager.getInstance();
  private subscriberId: string | null = null;
  private currentFilter: string | null = null;
  
  constructor(private supabase: SupabaseClient) {}
  
  /**
   * Subscribe to leaderboard updates with filtering
   */
  public subscribeToLeaderboard(
    tournamentId: string,
    options: {
      // Limit updates to specific data
      playerIds?: string[]; // Only get updates for these players
      topRanksOnly?: number; // Only get updates for top N players
      updateCallback: (data: any) => void;
    }
  ): void {
    // Unsubscribe from previous subscription if any
    this.unsubscribe();
    
    // Create filter conditions
    let filterCondition = `tournament_id=eq.${tournamentId}`;
    
    if (options.playerIds && options.playerIds.length > 0) {
      // Only subscribe to specific players
      const playerList = options.playerIds.map(id => `'${id}'`).join(',');
      filterCondition += ` AND player_id=in.(${playerList})`;
    } else if (options.topRanksOnly) {
      // Only subscribe to top N ranks
      filterCondition += ` AND position<=.${options.topRanksOnly}`;
    }
    
    this.currentFilter = filterCondition;
    
    // Subscribe using the connection manager to share connections
    this.subscriberId = this.connectionManager.subscribeToChannel(
      this.supabase,
      `leaderboard:${tournamentId}:${filterCondition}`,
      {
        event: '*',
        schema: 'public',
        table: 'leaderboard',
        filter: filterCondition
      },
      options.updateCallback
    );
  }
  
  /**
   * Unsubscribe from leaderboard updates
   */
  public unsubscribe(): void {
    if (this.subscriberId && this.currentFilter) {
      this.connectionManager.unsubscribe(
        `leaderboard:${this.currentFilter}`,
        this.subscriberId
      );
      
      this.subscriberId = null;
      this.currentFilter = null;
    }
  }
}
```

#### 3. Intelligent Offline Caching

For users with spotty connectivity:

```typescript
// src/services/OfflineLeaderboardCache.ts
export class OfflineLeaderboardCache {
  private static CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes
  
  /**
   * Cache leaderboard data for offline use
   */
  public static cacheLeaderboardData(tournamentId: string, leaderboardData: any[]): void {
    try {
      const cacheData = {
        timestamp: Date.now(),
        data: leaderboardData
      };
      
      localStorage.setItem(`leaderboard:${tournamentId}`, JSON.stringify(cacheData));
    } catch (error) {
      console.error('Failed to cache leaderboard data:', error);
    }
  }
  
  /**
   * Get cached leaderboard data
   */
  public static getCachedLeaderboardData(tournamentId: string): any[] | null {
    try {
      const cacheJson = localStorage.getItem(`leaderboard:${tournamentId}`);
      
      if (!cacheJson) return null;
      
      const cache = JSON.parse(cacheJson);
      
      // Check if cache is still valid
      if (Date.now() - cache.timestamp > this.CACHE_TTL_MS) {
        // Cache expired
        localStorage.removeItem(`leaderboard:${tournamentId}`);
        return null;
      }
      
      return cache.data;
    } catch (error) {
      console.error('Failed to retrieve cached leaderboard data:', error);
      return null;
    }
  }
}
```

#### 4. Adaptive Subscription Backoff

To handle reconnection attempts during service disruptions or poor connectivity:

```typescript
// src/services/RealtimeReconnectionManager.ts
export class RealtimeReconnectionManager {
  private static instance: RealtimeReconnectionManager;
  private connectionAttempts: Map<string, number> = new Map();
  private maxRetries: number = 10;
  private baseDelayMs: number = 1000; // Start with 1 second
  
  private constructor() {}
  
  public static getInstance(): RealtimeReconnectionManager {
    if (!RealtimeReconnectionManager.instance) {
      RealtimeReconnectionManager.instance = new RealtimeReconnectionManager();
    }
    return RealtimeReconnectionManager.instance;
  }
  
  /**
   * Handle channel reconnection with exponential backoff
   */
  public async reconnect(channelKey: string, reconnectFn: () => Promise<boolean>): Promise<boolean> {
    // Get current attempt count
    const attempts = this.connectionAttempts.get(channelKey) || 0;
    
    if (attempts >= this.maxRetries) {
      console.warn(`Max reconnection attempts (${this.maxRetries}) exceeded for channel ${channelKey}`);
      this.connectionAttempts.delete(channelKey); // Reset for next time
      return false;
    }
    
    // Calculate delay with exponential backoff and jitter
    const exponentialDelay = this.baseDelayMs * Math.pow(2, attempts);
    const jitter = Math.random() * 0.3 * exponentialDelay; // 0-30% jitter
    const delay = exponentialDelay + jitter;
    
    // Log reconnection attempt
    console.info(`Reconnection attempt ${attempts + 1} for channel ${channelKey} in ${Math.round(delay)}ms`);
    
    // Wait for calculated delay
    await new Promise(resolve => setTimeout(resolve, delay));
    
    // Increment attempt counter
    this.connectionAttempts.set(channelKey, attempts + 1);
    
    try {
      // Attempt reconnection
      const success = await reconnectFn();
      
      if (success) {
        // Reset counter on success
        this.connectionAttempts.delete(channelKey);
        return true;
      }
      
      // Try again if unsuccessful
      return this.reconnect(channelKey, reconnectFn);
    } catch (error) {
      console.error(`Error during reconnection attempt for ${channelKey}:`, error);
      // Try again despite error
      return this.reconnect(channelKey, reconnectFn);
    }
  }
  
  /**
   * Reset reconnection attempts for a channel
   */
  public resetReconnectionAttempts(channelKey: string): void {
    this.connectionAttempts.delete(channelKey);
  }
}
```

#### 5. Batch Update Throttling

To prevent overwhelming the realtime service with frequent updates:

```typescript
// src/services/BatchUpdateService.ts
export class BatchUpdateService {
  private updateQueue: Map<string, any[]> = new Map();
  private flushInterval: NodeJS.Timeout | null = null;
  private flushIntervalMs: number = 2000; // 2 seconds
  private supabase: any;
  
  constructor(supabase: any) {
    this.supabase = supabase;
  }
  
  /**
   * Start the batch processing service
   */
  public start(): void {
    if (this.flushInterval) {
      clearInterval(this.flushInterval);
    }
    
    this.flushInterval = setInterval(() => {
      this.flushQueue();
    }, this.flushIntervalMs);
  }
  
  /**
   * Stop the batch processing service
   */
  public stop(): void {
    if (this.flushInterval) {
      clearInterval(this.flushInterval);
      this.flushInterval = null;
    }
  }
  
  /**
   * Queue a score update for batch processing
   */
  public queueScoreUpdate(tournamentId: string, playerId: string, holeNumber: number, score: number): void {
    const key = `tournament:${tournamentId}`;
    
    if (!this.updateQueue.has(key)) {
      this.updateQueue.set(key, []);
    }
    
    this.updateQueue.get(key)!.push({
      playerId,
      holeNumber,
      score,
      timestamp: Date.now()
    });
  }
  
  /**
   * Process all queued updates
   */
  private async flushQueue(): Promise<void> {
    for (const [tournamentId, updates] of this.updateQueue.entries()) {
      if (updates.length === 0) continue;
      
      // Copy the updates and clear the queue before processing
      const updatesToProcess = [...updates];
      this.updateQueue.set(tournamentId, []);
      
      try {
        // Group updates by player for more efficient processing
        const playerUpdates = this.groupUpdatesByPlayer(updatesToProcess);
        
        // Process each player's updates
        for (const [playerId, playerScores] of Object.entries(playerUpdates)) {
          await this.processPlayerUpdates(tournamentId.replace('tournament:', ''), playerId, playerScores);
        }
      } catch (error) {
        console.error(`Error processing batch updates for ${tournamentId}:`, error);
        // Could implement a retry mechanism here
      }
    }
  }
  
  /**
   * Group updates by player ID
   */
  private groupUpdatesByPlayer(updates: any[]): Record<string, any[]> {
    return updates.reduce((grouped, update) => {
      if (!grouped[update.playerId]) {
        grouped[update.playerId] = [];
      }
      grouped[update.playerId].push(update);
      return grouped;
    }, {} as Record<string, any[]>);
  }
  
  /**
   * Process all updates for a single player
   */
  private async processPlayerUpdates(tournamentId: string, playerId: string, updates: any[]): Promise<void> {
    // Implementation would depend on your specific database structure
    // This is a simplified example
    const { error } = await this.supabase
      .from('scores')
      .upsert(
        updates.map(update => ({
          tournament_id: tournamentId,
          player_id: playerId,
          hole_number: update.holeNumber,
          score: update.score,
          updated_at: new Date().toISOString()
        }))
      );
    
    if (error) {
      console.error(`Error updating scores for player ${playerId}:`, error);
      throw error;
    }
  }
}
```

### Interpreting Test Results

When analyzing load test results, we focus on these key metrics:

1. **Realtime Event Latency**: The time between database changes and client notifications
   - **Good**: < 300ms average
   - **Acceptable**: 300-800ms average
   - **Poor**: > 800ms average

2. **Connection Stability**: Number of unexpected connection drops
   - **Good**: < 1 drop per user per hour
   - **Acceptable**: 1-3 drops per user per hour
   - **Poor**: > 3 drops per user per hour

3. **Free Tier Resource Utilization**:
   - Connection count (50 limit)
   - Database size (500MB limit)
   - Keep peak usage below 80% of limits for safe operation

4. **Error Patterns**:
   - Authentication failures
   - Rate limiting errors
   - Database constraint violations
   - Timeout errors

### Advanced Analysis Techniques

Beyond basic metrics, apply these analytical approaches to gain deeper insights:

#### 1. Latency Distribution Analysis

```typescript
// src/tests/analysis/LatencyDistributionAnalyzer.ts
export class LatencyDistributionAnalyzer {
  /**
   * Analyze latency distribution to identify patterns
   */
  public static analyzeLatencyDistribution(latencyValues: number[]): object {
    if (latencyValues.length === 0) return {};
    
    // Sort values for percentile calculations
    const sortedValues = [...latencyValues].sort((a, b) => a - b);
    
    // Basic statistics
    const min = sortedValues[0];
    const max = sortedValues[sortedValues.length - 1];
    const p50 = this.getPercentile(sortedValues, 50);
    const p95 = this.getPercentile(sortedValues, 95);
    const p99 = this.getPercentile(sortedValues, 99);
    
    // Calculate standard deviation
    const mean = this.calculateMean(sortedValues);
    const stdDev = this.calculateStdDev(sortedValues, mean);
    
    // Check for multimodal distribution (can indicate different behavior patterns)
    const isMultimodal = this.detectMultimodality(sortedValues);
    
    // Generate histogram data for visualization
    const histogram = this.generateHistogram(sortedValues, 20);
    
    // Check for long tail pattern
    const tailRatio = max / p95;
    const hasLongTail = tailRatio > 3; // If max is more than 3x the p95
    
    return {
      min,
      max,
      mean,
      p50,
      p95,
      p99,
      stdDev,
      isMultimodal,
      histogram,
      tailRatio,
      hasLongTail,
      anomalies: this.detectAnomalies(sortedValues, mean, stdDev)
    };
  }
  
  // Implementation details omitted for brevity
}
```

Multimodal latency distributions often indicate different classes of behavior within your application. For example, you might see one cluster of latencies for cached responses and another for database queries.

#### 2. Correlation Analysis

Look for relationships between different metrics:

```typescript
// src/tests/analysis/CorrelationAnalyzer.ts
export class CorrelationAnalyzer {
  /**
   * Calculate correlation coefficient between two metrics
   */
  public static calculateCorrelation(
    metricsA: Array<{timestamp: number, value: number}>,
    metricsB: Array<{timestamp: number, value: number}>
  ): number {
    // Align timestamps and pair values
    const pairedValues = this.alignMetricsByTimestamp(metricsA, metricsB);
    
    // Extract just the values for correlation calculation
    const valuesA = pairedValues.map(pair => pair.valueA);
    const valuesB = pairedValues.map(pair => pair.valueB);
    
    // Calculate correlation coefficient
    return this.pearsonCorrelation(valuesA, valuesB);
  }
  
  /**
   * Identify key correlations between multiple metrics
   */
  public static findKeyCorrelations(metrics: Record<string, Array<{timestamp: number, value: number}>>): Array<{
    metricA: string;
    metricB: string;
    correlation: number;
    strength: string;
  }> {
    const metricNames = Object.keys(metrics);
    const results = [];
    
    // Compare each pair of metrics
    for (let i = 0; i < metricNames.length; i++) {
      for (let j = i + 1; j < metricNames.length; j++) {
        const metricA = metricNames[i];
        const metricB = metricNames[j];
        
        const correlation = this.calculateCorrelation(
          metrics[metricA],
          metrics[metricB]
        );
        
        // Categorize correlation strength
        let strength = 'weak';
        if (Math.abs(correlation) > 0.7) strength = 'strong';
        else if (Math.abs(correlation) > 0.4) strength = 'moderate';
        
        results.push({
          metricA,
          metricB,
          correlation,
          strength
        });
      }
    }
    
    // Sort by absolute correlation strength (descending)
    return results.sort((a, b) => Math.abs(b.correlation) - Math.abs(a.correlation));
  }
  
  // Implementation details omitted for brevity
}
```

Strong correlations to look for:
- Connection count vs. event latency (indicates scalability limits)
- Subscription setup time vs. error rate (indicates connection rejection)
- Database query time vs. event delivery time (indicates database bottlenecks)

#### 3. Time-Series Pattern Recognition

Identify specific patterns in your time-series metrics:

```typescript
// src/tests/analysis/PatternRecognizer.ts
export class PatternRecognizer {
  /**
   * Identify common patterns in time series data
   */
  public static identifyPatterns(timeSeriesData: Array<{timestamp: number, value: number}>): object {
    // Sort data by timestamp
    const sortedData = [...timeSeriesData].sort((a, b) => a.timestamp - b.timestamp);
    
    return {
      hasSpikes: this.detectSpikes(sortedData),
      hasTrend: this.detectTrend(sortedData),
      hasCycles: this.detectCycles(sortedData),
      hasStepChange: this.detectStepChanges(sortedData)
    };
  }
  
  // Implementation details omitted for brevity
}
```

Important patterns to watch for:
- Spikes in latency when concurrent connections increase
- Step changes that coincide with specific user actions
- Cyclical patterns that suggest resource saturation and recovery

### Case Study: Optimizing Tournament Leaderboard Realtime Performance

Our team encountered performance challenges with the tournament leaderboard feature during preliminary load testing. Here's how we identified and resolved the issues:

#### Initial Findings

Our baseline load test with 20 concurrent users revealed:

1. **High Latency**: 95th percentile event latency of 1,200ms (above our 800ms threshold)
2. **Connection Instability**: ~5 connection drops per 10-minute test
3. **Database Load**: High CPU utilization during the test

#### Root Cause Analysis

1. Analyzed database query performance using `pg_stat_statements`:
   - Discovered unoptimized leaderboard query executing for each subscription
   - Query was not using available indexes effectively

2. Examined realtime subscription patterns:
   - Each user was subscribing to all tournament data
   - No filtering was applied to limit subscription scope

3. Reviewed connection handling:
   - No connection pooling implemented
   - Each user consumed a separate connection

#### Solution Implementation

1. **Query Optimization**:
   - Added composite index on (tournament_id, position)
   - Rewrote leaderboard query to use more efficient joins
   - Added caching layer for leaderboard calculations

```sql
-- Added to database migrations
CREATE INDEX IF NOT EXISTS idx_leaderboard_tournament_position 
ON leaderboard(tournament_id, position);
```

2. **Subscription Optimization**:
   - Implemented selective subscriptions with filtering:

```typescript
// Before: Subscribing to all tournament data
supabase
  .channel('leaderboard')
  .on('postgres_changes', {
    event: '*',
    schema: 'public',
    table: 'leaderboard',
    filter: `tournament_id=eq.${tournamentId}`
  }, callback)
  .subscribe();

// After: Subscribing only to relevant data
supabase
  .channel('leaderboard_top20')
  .on('postgres_changes', {
    event: '*',
    schema: 'public',
    table: 'leaderboard',
    filter: `tournament_id=eq.${tournamentId} AND position<=20`
  }, callback)
  .subscribe();
```

3. **Connection Pooling**:
   - Implemented shared connection manager
   - Grouped subscriptions by tournament

#### Results

After optimizations, our results improved significantly:

1. **Latency**: 95th percentile decreased to 350ms (71% improvement)
2. **Stability**: Connection drops reduced to <1 per hour
3. **Scalability**: Successfully tested with 45 concurrent users
4. **Database Load**: 60% reduction in database CPU utilization

This case study demonstrates how targeted optimizations can dramatically improve realtime performance while staying within the constraints of Supabase's free tier.

By conducting these load tests and implementing optimizations based on the results, we can ensure our real-time features will perform well under expected user loads while staying within the constraints of Supabase's free tier.

### Developer Workflow for Load Testing

Our development team should follow these steps when working with real-time features:

1. **Development Stage Testing**
   - Run the baseline performance test against the local dev environment after implementing any new real-time feature
   - Verify that latency metrics remain within acceptable thresholds
   - Document baseline metrics in the PR description

2. **Pre-Deployment Testing**
   - Run the full suite of load tests against the staging environment before merging to main
   - Compare results with previous test runs to identify any regressions
   - Address any performance issues before proceeding with deployment

3. **Periodic Production Monitoring**
   - Schedule monthly load tests against the production environment during low-usage periods
   - Document trends in key metrics over time
   - Set up automated alerts for significant regressions

**Recommended Development CLI Commands:**

```bash
# Run a quick baseline test during development
npm run load-test:baseline

# Run the full test suite for pre-deployment verification
npm run load-test:full

# Generate a comprehensive report from test results
npm run load-test:report

# Compare current results with previous runs
npm run load-test:compare
```

**Load Test Result Artifacts:**

Each load test generates the following artifacts:
- JSON report with raw metrics
- Performance trend graphs (.png)
- Console log output in text format
- Summary report in Markdown

These artifacts should be attached to PRs and stored in our documentation repository for historical reference.

### Best Practices for Real-Time Feature Development

To ensure our real-time features perform well under load, follow these best practices:

1. **Limit Subscription Scope**
   - Always filter subscriptions to only the data needed by the current view
   - Use RLS policies to enforce access control at the database level
   - Consider implementing a time-to-live for long-running subscriptions

2. **Manage Connection Lifecycle**
   - Explicitly unsubscribe when components unmount
   - Implement the connection pooling pattern for shared data
   - Handle reconnection gracefully with exponential backoff

3. **Optimize Payload Size**
   - Keep subscription data minimal
   - Consider selective column subscriptions
   - Use database views to pre-aggregate data when possible

4. **Monitor Connection Count**
   - Track active connections in your application telemetry
   - Implement graceful degradation when approaching limits
   - Consider a waiting room pattern for high-traffic events

5. **Test Edge Cases**
   - Poor network conditions
   - Sudden connection loss
   - Concurrent updates to the same record
   - Large batch updates

### Real-Time Performance Dashboard

We've created a monitoring dashboard specifically for real-time performance tracking during load tests and regular operation. This dashboard integrates with our monitoring tools and provides real-time visibility into key metrics.

```typescript
// src/monitoring/RealTimePerformanceDashboard.ts
import { MetricsService } from './MetricsService';
import { SupabaseConnectionStats } from '../services/SupabaseConnectionStats';

export class RealTimePerformanceDashboard {
  private metricsService: MetricsService;
  private connectionStats: SupabaseConnectionStats;
  private updateInterval: NodeJS.Timeout | null = null;
  private intervalMs: number = 5000; // Update every 5 seconds
  
  constructor() {
    this.metricsService = new MetricsService();
    this.connectionStats = new SupabaseConnectionStats();
  }
  
  /**
   * Initialize and start the dashboard
   */
  public async start(): Promise<void> {
    // Initial metrics fetch
    await this.updateMetrics();
    
    // Set up interval for continuous updates
    this.updateInterval = setInterval(() => {
      this.updateMetrics().catch(err => {
        console.error('Error updating dashboard metrics:', err);
      });
    }, this.intervalMs);
    
    console.log('Real-time performance dashboard started');
  }
  
  /**
   * Fetch and update all metrics
   */
  private async updateMetrics(): Promise<void> {
    const metrics = await Promise.all([
      this.connectionStats.getCurrentConnectionCount(),
      this.metricsService.getRecentMetrics('realtime_latency_ms', { minutes: 5 }),
      this.metricsService.getRecentMetrics('subscription_setup_ms', { minutes: 5 }),
      this.metricsService.getErrorCount({ minutes: 5 })
    ]);
    
    const [
      connectionCount,
      latencyMetrics,
      setupTimeMetrics,
      errorCount
    ] = metrics;
    
    // Update dashboard UI components
    this.updateConnectionGauge(connectionCount);
    this.updateLatencyChart(latencyMetrics);
    this.updateSetupTimeChart(setupTimeMetrics);
    this.updateErrorCounter(errorCount);
    
    // Check for alert thresholds
    this.checkAlertThresholds({
      connectionCount,
      latencyP95: this.calculatePercentile(latencyMetrics, 95),
      setupTimeAvg: this.calculateAverage(setupTimeMetrics),
      errorCount
    });
  }
  
  /**
   * Calculate percentile value from array of metrics
   */
  private calculatePercentile(values: number[], percentile: number): number {
    if (values.length === 0) return 0;
    
    // Sort values
    const sortedValues = [...values].sort((a, b) => a - b);
    
    // Calculate index
    const index = Math.ceil((percentile / 100) * sortedValues.length) - 1;
    
    return sortedValues[Math.max(0, index)];
  }
  
  /**
   * Calculate average from array of metrics
   */
  private calculateAverage(values: number[]): number {
    if (values.length === 0) return 0;
    
    const sum = values.reduce((acc, val) => acc + val, 0);
    return sum / values.length;
  }
  
  // UI update methods and alert threshold checks omitted for brevity
  
  /**
   * Stop the dashboard updates
   */
  public stop(): void {
    if (this.updateInterval) {
      clearInterval(this.updateInterval);
      this.updateInterval = null;
    }
    
    console.log('Real-time performance dashboard stopped');
  }
}
```

The dashboard displays the following key metrics:

1. **Connection Utilization Gauge**
   - Shows current connection count relative to the free tier limit (50)
   - Color-coded for quick status assessment (green < 60%, yellow 60-80%, red > 80%)

2. **Event Latency Timeline**
   - Real-time graph of event delivery latency over time
   - P50, P95, and P99 percentile lines

3. **Subscription Setup Time**
   - Average time to establish new subscriptions
   - Shows impact of connection pooling optimizations

4. **Error Rate Tracker**
   - Count and categories of real-time related errors
   - Trend line showing error rate over time

5. **Realtime Channel Heat Map**
   - Visual representation of busy channels and subscription distribution
   - Helps identify hotspots that need optimization

### Integration with External Monitoring Tools

Our load testing framework integrates with the following external tools for enhanced visibility:

#### 1. Azure Application Insights Integration

```typescript
// src/monitoring/AzureMonitoringIntegration.ts
import * as appInsights from 'applicationinsights';
import { LoadTestMetricsCollector } from '../tests/loadTests/LoadTestMetricsCollector';

export class AzureMonitoringIntegration {
  private client: appInsights.TelemetryClient;
  
  constructor(private instrumentationKey: string) {
    // Initialize App Insights
    appInsights.setup(instrumentationKey)
      .setAutoDependencyCorrelation(true)
      .setAutoCollectRequests(true)
      .setAutoCollectPerformance(true)
      .setAutoCollectExceptions(true)
      .setAutoCollectDependencies(true)
      .setAutoCollectConsole(true)
      .setUseDiskRetryCaching(true)
      .setSendLiveMetrics(true)
      .start();
    
    this.client = appInsights.defaultClient;
  }
  
  /**
   * Push load test metrics to Azure Monitor
   */
  public pushLoadTestMetrics(metrics: LoadTestMetricsCollector): void {
    const report = metrics.generateReport() as any;
    
    // Track test execution as a custom event
    this.client.trackEvent({
      name: 'SupabaseLoadTest',
      properties: {
        testDuration: report.testDuration,
        timestamp: report.timestamp,
        userCount: report.userCount
      }
    });
    
    // Track each metric category
    Object.entries(report.metrics).forEach(([category, data]: [string, any]) => {
      // Track averages as metrics
      this.client.trackMetric({
        name: `Supabase.${category}.avg`,
        value: data.avg
      });
      
      // Track 95th percentile as metrics
      this.client.trackMetric({
        name: `Supabase.${category}.p95`,
        value: data.p95
      });
    });
    
    // Track errors
    Object.entries(report.errors).forEach(([category, data]: [string, any]) => {
      this.client.trackMetric({
        name: `Supabase.Errors.${category}`,
        value: data.count
      });
    });
      // Flush telemetry immediately
    this.client.flush();
  }
}
```

#### 2. Supabase Observability Integration

Supabase provides built-in monitoring capabilities through PostgreSQL logging and the Supabase dashboard. Our load testing framework extracts and analyzes this data for comprehensive performance evaluation.

```typescript
// src/monitoring/SupabaseObservabilityIntegration.ts
import { createClient, SupabaseClient } from '@supabase/supabase-js';
import fs from 'fs';

export class SupabaseObservabilityIntegration {
  private supabase: SupabaseClient;
  private pgStatStatementsEnabled: boolean = false;
  
  constructor(supabaseUrl: string, serviceRoleKey: string) {
    // Use service role key to access system tables
    this.supabase = createClient(supabaseUrl, serviceRoleKey);
  }
  
  /**
   * Check if pg_stat_statements extension is enabled
   */
  public async checkPgStatStatements(): Promise<boolean> {
    try {
      const { data, error } = await this.supabase.rpc('check_pg_stat_statements');
      
      if (error) throw error;
      
      this.pgStatStatementsEnabled = data === true;
      return this.pgStatStatementsEnabled;
    } catch (error) {
      console.error('Error checking pg_stat_statements:', error);
      return false;
    }
  }
  
  /**
   * Get query performance statistics from pg_stat_statements
   */
  public async getQueryStats(): Promise<any[]> {
    if (!this.pgStatStatementsEnabled) {
      const enabled = await this.checkPgStatStatements();
      if (!enabled) {
        console.warn('pg_stat_statements extension is not enabled');
        return [];
      }
    }
    
    try {
      // Query pg_stat_statements for top queries by total time
      const { data, error } = await this.supabase.rpc('get_query_stats');
      
      if (error) throw error;
      
      return data || [];
    } catch (error) {
      console.error('Error fetching query stats:', error);
      return [];
    }
  }
  
  /**
   * Get realtime connection statistics
   */
  public async getRealtimeStats(): Promise<any> {
    try {
      // Query realtime connection stats
      const { data, error } = await this.supabase.rpc('get_realtime_stats');
      
      if (error) throw error;
      
      return data || {};
    } catch (error) {
      console.error('Error fetching realtime stats:', error);
      return {};
    }
  }
  
  /**
   * Export database logs for analysis
   */
  public async exportDatabaseLogs(outputPath: string): Promise<boolean> {
    try {
      // This function requires appropriate permissions in Supabase
      const { data, error } = await this.supabase.rpc('export_recent_logs', {
        hours_back: 1
      });
      
      if (error) throw error;
      
      if (data && data.logs) {
        // Write logs to file
        fs.writeFileSync(outputPath, data.logs);
        return true;
      }
      
      return false;
    } catch (error) {
      console.error('Error exporting database logs:', error);
      return false;
    }
  }
  
  /**
   * Analyze SQL performance from logs
   */
  public analyzeSqlPerformance(logPath: string): object {
    try {
      const logContent = fs.readFileSync(logPath, 'utf8');
      
      // Parse logs to extract SQL queries and execution times
      // Implementation details omitted for brevity
      
      return {
        // Analysis results
      };
    } catch (error) {
      console.error('Error analyzing SQL performance:', error);
      return {};
    }
  }
}
```

### Test Execution Checklist

Before executing load tests against your Supabase instance, follow this checklist to ensure meaningful results:

#### Pre-Test Preparation:

- [ ] **Environment Selection**:
  - [ ] Use a separate testing environment that mirrors production
  - [ ] Never run extensive load tests against production without proper coordination
  - [ ] Ensure the environment has similar resource allocation as production

- [ ] **Data Preparation**:
  - [ ] Create a representative dataset (tournaments, users, scores)
  - [ ] Scale data to match expected production scenarios
  - [ ] Include edge cases like very large tournaments

- [ ] **Test Account Setup**:
  - [ ] Create dedicated test users with appropriate permissions
  - [ ] Use service role key for metrics collection
  - [ ] Ensure test users don't interfere with real users

- [ ] **Monitoring Setup**:
  - [ ] Configure Azure Application Insights integration
  - [ ] Enable detailed Supabase logging
  - [ ] Set up real-time dashboard for tracking

#### Execution Process:

1. **Baseline Collection**:
   - Run a minimal load test to establish baseline metrics
   - Document baseline results in test log
   - Ensure baseline meets minimum performance thresholds

2. **Incremental Testing**:
   - Start with lower user counts and gradually increase
   - Allow stabilization period between test runs
   - Identify the breaking point where performance degrades

3. **Specialized Scenarios**:
   - Run specific scenario tests (Tournament Scoring, Connection Limit, etc.)
   - Document specific behaviors observed in each scenario
   - Capture screenshots of real-time dashboard

4. **Edge Case Testing**:
   - Execute tests that simulate poor connectivity
   - Test rapid connect/disconnect patterns
   - Simulate concurrent writes to same records

#### Post-Test Analysis:

- [ ] **Metrics Review**:
  - [ ] Compare results with previous test runs
  - [ ] Look for performance regressions
  - [ ] Identify bottlenecks and optimization opportunities

- [ ] **Database Analysis**:
  - [ ] Review pg_stat_statements for slow queries
  - [ ] Analyze execution plans for problematic queries
  - [ ] Check for missing indexes or inefficient RLS policies

- [ ] **Report Generation**:
  - [ ] Generate comprehensive test report
  - [ ] Include visualizations of key metrics
  - [ ] Document all findings and recommendations

- [ ] **Cleanup**:
  - [ ] Remove test data if no longer needed
  - [ ] Reset connection counts and cache
  - [ ] Archive test results for future reference

#### Test Execution Command:

```powershell
# Set environment variables
$env:SUPABASE_URL = "https://your-project.supabase.co"
$env:SUPABASE_ANON_KEY = "your-anon-key"
$env:SUPABASE_SERVICE_KEY = "your-service-key"

# Run the comprehensive load test suite
npm run load-test -- --scenario=all --output=./reports --dashboardEnabled
```
