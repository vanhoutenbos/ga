# Real-Time Scaling Strategy

## Overview

This document outlines the strategy for scaling real-time functionality in the Golf Tournament Organizer application as user concurrency increases. Real-time updates are a critical feature for our application, particularly during active tournaments when players, spectators, and organizers are simultaneously viewing and updating tournament data.

## Current Real-Time Architecture

The Golf Tournament Organizer currently uses Supabase's real-time functionality, which is built on PostgreSQL's logical replication feature. This system:

1. Listens for changes to the PostgreSQL database using the Write-Ahead Log (WAL)
2. Broadcasts those changes to connected clients using WebSockets
3. Updates client state in response to these broadcasts

While this architecture works well for small to medium-sized tournaments, it has limitations as we scale to larger concurrent user counts.

## Scaling Challenges

### Supabase Real-Time Limits

1. **Connection Limits**:
   - Free tier: 200 concurrent connections
   - Pro tier: 500 concurrent connections
   - Team tier: 3,000 concurrent connections

2. **Payload Size Limitations**:
   - Maximum message size: 8KB
   - Maximum channel subscriptions per socket: 100

3. **Client State Synchronization**:
   - Each client needs to maintain subscription state
   - Large tournaments could require many distinct subscriptions

### Tournament Scale Scenarios

| Tournament Size | Estimated Concurrent Users | Realtime Requirements |
|----------------|----------------------------|----------------------|
| Small (20-50 players) | 100-250 | Current Supabase Free tier sufficient |
| Medium (50-100 players) | 250-500 | Requires Supabase Pro tier |
| Large (100-200 players) | 500-1,000 | Near limits of Pro tier |
| Major (200+ players) | 1,000-5,000+ | Exceeds Pro tier, approaches Team tier limits |

## Scaling Strategies

### 1. Optimized Subscription Management

**Implementation Strategy:**

1. **Targeted Subscriptions**:
   - Subscribe only to specific holes/groups rather than entire tournaments
   - Implement dynamic subscription management based on user's current view
   - Unsubscribe from inactive views automatically

2. **Data Chunking**:
   - Break large updates into smaller chunks to avoid payload size limitations
   - Implement client-side assembly of chunked updates
   - Prioritize critical updates (e.g., leaderboard changes) over non-critical ones

3. **Throttling and Debouncing**:
   - Implement server-side throttling for high-frequency updates
   - Use client-side debouncing to prevent UI thrashing
   - Add configurable update frequency based on tournament size

**Implementation Example:**

```typescript
// Dynamic subscription management
function subscribeToRelevantData(user, view) {
  // Unsubscribe from previous views
  unsubscribeFromIrrelevantChannels();
  
  // Subscribe based on current view
  if (view === 'leaderboard') {
    subscribeToLeaderboardUpdates(tournamentId);
  } else if (view === 'hole') {
    subscribeToHoleUpdates(tournamentId, holeId);
  } else if (view === 'playerCard') {
    subscribeToPlayerUpdates(tournamentId, playerId);
  }
  
  // Set up auto-unsubscribe timer for inactive views
  setupInactivityTimer();
}
```

### 2. Hierarchical Real-Time Architecture

As we scale beyond Supabase's limits, we'll implement a hierarchical architecture:

1. **Primary/Secondary Pattern**:
   - Primary Supabase instance handles write operations and critical updates
   - Secondary instances or services handle read-heavy operations

2. **Event Aggregation**:
   - Aggregate multiple small updates into periodic batched updates
   - Implement different update frequencies for different data types

3. **Fan-out Architecture**:
   - Implement intermediary servers to handle the fan-out of updates
   - Each intermediary server handles a subset of total connections

**Implementation Diagram:**

```
┌────────────┐                ┌───────────────┐
│            │                │               │
│ PostgreSQL │───────────────▶│ Primary       │
│ Database   │                │ Realtime      │
│            │                │ Service       │
└────────────┘                └───────┬───────┘
                                      │
                                      ▼
                 ┌──────────┬─────────┴───────┬──────────┐
                 │          │                 │          │
         ┌───────▼──┐ ┌─────▼─────┐   ┌───────▼──┐ ┌─────▼─────┐
         │          │ │           │   │          │ │           │
         │ Fan-out  │ │ Fan-out   │   │ Fan-out  │ │ Fan-out   │
         │ Server 1 │ │ Server 2  │   │ Server 3 │ │ Server N  │
         │          │ │           │   │          │ │           │
         └─────┬────┘ └─────┬─────┘   └─────┬────┘ └─────┬─────┘
               │            │               │            │
         ┌─────▼────┐ ┌─────▼─────┐   ┌─────▼────┐ ┌─────▼─────┐
         │          │ │           │   │          │ │           │
         │ Clients  │ │ Clients   │   │ Clients  │ │ Clients   │
         │ Group 1  │ │ Group 2   │   │ Group 3  │ │ Group N   │
         │          │ │           │   │          │ │           │
         └──────────┘ └───────────┘   └──────────┘ └───────────┘
```

### 3. Hybrid Service Approach

For tournaments exceeding our Supabase tier limits, we'll implement a hybrid approach:

1. **Azure SignalR Integration**:
   - Use Azure SignalR Service for large tournaments requiring high concurrency
   - Scale to tens of thousands of concurrent connections

2. **Service Selection Logic**:
   - Small tournaments (<200 connections): Supabase Real-time
   - Medium tournaments (200-500 connections): Enhanced Supabase with optimization
   - Large tournaments (500+ connections): Azure SignalR Service

3. **Unified Client API**:
   - Abstract the real-time service behind a consistent client API
   - Allow seamless switching between services based on load

**Implementation Example:**

```typescript
// Realtime service factory
class RealtimeServiceFactory {
  static getRealtimeService(tournamentId, expectedUsers) {
    if (expectedUsers < 200) {
      return new SupabaseRealtimeService(tournamentId);
    } else if (expectedUsers < 500) {
      return new OptimizedSupabaseRealtimeService(tournamentId);
    } else {
      return new AzureSignalRService(tournamentId);
    }
  }
}

// Client usage remains consistent
const realtimeService = RealtimeServiceFactory.getRealtimeService(
  tournament.id,
  tournament.expectedConcurrentUsers
);

realtimeService.subscribe('leaderboard', (update) => {
  updateLeaderboardUI(update);
});
```

### 4. Edge Caching for Read-Heavy Data

1. **CDN Integration**:
   - Cache read-heavy but infrequently changing data at the edge
   - Implement cache invalidation strategies for updates

2. **Regional Deployment**:
   - Deploy real-time servers in regions close to tournament locations
   - Route users to the nearest server instance

3. **Hybrid Polling/WebSocket Approach**:
   - Use WebSockets for critical real-time data (scores, leaderboard)
   - Use polling for less time-sensitive information

## Implementation Phases

### Phase 1: Optimized Supabase Usage

**Target Scale**: Up to 500 concurrent users

1. Implement subscription management optimizations
2. Add client-side throttling and debouncing
3. Create monitoring for connection usage and potential bottlenecks

**Technical Implementation:**
- Create a subscription manager service
- Implement view-based subscription logic
- Add connection monitoring and analytics

### Phase 2: Enhanced Architecture

**Target Scale**: Up to 3,000 concurrent users

1. Implement the hierarchical architecture with fan-out servers
2. Develop the service abstraction layer
3. Add automatic scaling based on tournament size

**Technical Implementation:**
- Create intermediate server layer using Azure Functions or App Service
- Implement the replication logic between services
- Develop automatic service selection based on expected load

### Phase 3: Full Hybrid Architecture

**Target Scale**: 3,000+ concurrent users

1. Integrate Azure SignalR Service for major tournaments
2. Implement regional deployments
3. Add sophisticated load balancing and failover

**Technical Implementation:**
- Develop Azure SignalR integration
- Create unified client APIs across services
- Implement regional routing and load balancing

## Monitoring and Performance Metrics

To ensure our real-time scaling strategy is effective, we'll track:

1. **Connection Metrics**:
   - Peak concurrent connections
   - Connection error rates
   - Connection duration distribution

2. **Performance Metrics**:
   - Message delivery latency
   - Message queue backlog
   - Client synchronization delay

3. **Resource Usage**:
   - WebSocket server CPU/memory usage
   - Database replication lag
   - Network bandwidth consumption

**Implementation Example:**

```typescript
// Sample monitoring setup
const monitoring = {
  recordConnectionMetrics: (connectionCount, errorRate) => {
    // Record to monitoring service
  },
  
  measureLatency: (messageId, sendTime) => {
    const deliveryTime = Date.now();
    const latency = deliveryTime - sendTime;
    // Record latency metric
  },
  
  trackResourceUsage: (cpu, memory, networkBandwidth) => {
    // Record resource metrics
  }
};
```

## Testing Strategy

1. **Load Testing**:
   - Simulate various tournament sizes with virtual clients
   - Test gradual scaling and sudden spikes
   - Measure performance degradation under load

2. **Failover Testing**:
   - Test automatic service switching
   - Simulate primary service failures
   - Verify data consistency during failovers

3. **Regional Performance Testing**:
   - Test from different geographic locations
   - Measure latency across regions
   - Verify regional routing effectiveness

## Fallback Mechanisms

In case of real-time service degradation or failure:

1. **Graceful Degradation**:
   - Fall back to polling for updates
   - Reduce update frequency
   - Prioritize critical updates only

2. **User Notifications**:
   - Inform users of reduced real-time functionality
   - Provide manual refresh options
   - Display last update timestamp

3. **Offline Mode**:
   - Switch to offline mode with local data
   - Queue updates for synchronization when service recovers
   - Prioritize tournament continuation over real-time updates

## Cost Analysis

| Scaling Approach | User Scale | Estimated Monthly Cost |
|------------------|------------|------------------------|
| Supabase Free Tier | <200 users | $0 |
| Supabase Pro Tier | <500 users | $25 |
| Optimized Pro Tier | <1,000 users | $25 + optimization development |
| Hybrid Architecture | <3,000 users | $25 (Supabase) + ~$100-$200 (Azure) |
| Full SignalR | 3,000+ users | ~$300-$1,000 depending on scale |

## Conclusion

The real-time scaling strategy outlined here provides a clear path from our current architecture to one that can support major tournaments with thousands of concurrent users. By implementing these strategies in phases, we can grow our real-time capabilities in line with actual usage patterns, ensuring a smooth experience for users while managing costs effectively.

Our hybrid approach allows us to leverage the simplicity of Supabase for smaller tournaments while ensuring we have a scaling path for larger events, providing the best balance of developer experience, user experience, and cost efficiency.
