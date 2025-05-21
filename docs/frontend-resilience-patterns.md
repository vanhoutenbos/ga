# Frontend Resilience Patterns

This document describes the resilience patterns implemented in the Golf Tournament Organizer frontend application to handle transient errors, network issues, and API failures gracefully.

## Overview

The frontend implements several resilience patterns to ensure a smooth user experience even when faced with network issues or backend service disruptions:

1. **Automatic Retry with Exponential Backoff**: Automatically retries failed API calls with increasing delays
2. **Circuit Breaker Pattern**: Prevents cascading failures by temporarily stopping calls to failing endpoints
3. **Offline Mode**: Queues mutations when offline for later synchronization
4. **Graceful Degradation**: Shows appropriate UI feedback during service disruptions
5. **Rate Limit Handling**: Properly respects rate limit headers and retries accordingly

## Implementation

### Retry with Exponential Backoff

The `useApi` hook implements automatic retry for transient errors (408, 429, 5xx) with an exponential backoff strategy:

```typescript
// Excerpt from useApi hook
if (shouldRetry(error, retryCount - attempt + 1)) {
  const calculatedDelay = retryDelay * Math.pow(backoffFactor, attempt - 1);
  console.warn(`Request failed. Retrying (${attempt}/${retryCount}) after ${calculatedDelay}ms...`);
  await new Promise(resolve => setTimeout(resolve, calculatedDelay));
  return executeRequest();
}
```

Configuration options:
- `retryCount`: Maximum number of retry attempts (default: 3)
- `retryDelay`: Initial delay in milliseconds (default: 1000)
- `backoffFactor`: Multiplier for each subsequent retry (default: 2)
- `statusCodesToRetry`: HTTP status codes to trigger retries (default: [408, 429, 500, 502, 503, 504])

### Circuit Breaker Pattern

The circuit breaker prevents repeated calls to failing endpoints, allowing them time to recover:

```typescript
// Circuit states
type CircuitState = 'CLOSED' | 'OPEN' | 'HALF_OPEN';

// Check if circuit is open for this endpoint
if (isCircuitOpen(endpoint)) {
  throw new Error(`Circuit is open for ${endpoint}. Too many failures.`);
}
```

- **CLOSED**: Normal operation, requests flow through
- **OPEN**: Too many failures, requests are rejected without attempting to call the API
- **HALF_OPEN**: After a timeout period, allows one test request to check if the service has recovered

Configuration options:
- `circuitBreakerThreshold`: Number of failures before opening the circuit (default: 5)
- `circuitBreakerTimeout`: Milliseconds before transitioning to HALF_OPEN (default: 30000)

### Offline Mode

For offline support, the hook queues mutation operations (POST, PUT, PATCH, DELETE) and executes them when the connection is restored:

```typescript
// Handle offline mode for mutations
if (!isOnline && ['post', 'put', 'patch', 'delete'].includes(method.toLowerCase())) {
  // Queue the request for later if it's a mutation
  queueRequest(method, url, data, config);
  throw new Error(`You are offline. The request has been queued for when you're back online.`);
}
```

This works in conjunction with the `OfflineContext` provider, which tracks connectivity status and manages synchronization.

### Graceful UI Degradation

The application components use the state from the `useApi` hook to show appropriate loading states, error messages, and fallbacks:

```tsx
function TournamentList() {
  const { get, isLoading, error } = useApi();
  const [tournaments, setTournaments] = useState([]);

  useEffect(() => {
    const fetchTournaments = async () => {
      try {
        const response = await get('/tournaments');
        setTournaments(response.data);
      } catch (err) {
        // Error is already handled by the hook
      }
    };
    
    fetchTournaments();
  }, [get]);

  if (isLoading) return <LoadingSpinner />;
  
  if (error) {
    return (
      <ErrorPanel 
        title="Unable to load tournaments" 
        message={error.message}
        retry={() => fetchTournaments()}
      />
    );
  }
  
  // Fallback for empty state
  if (tournaments.length === 0) {
    return <EmptyState message="No tournaments found" />;
  }
  
  // Render tournaments
  return (
    <div className="tournament-list">
      {tournaments.map(tournament => (
        <TournamentCard key={tournament.id} tournament={tournament} />
      ))}
    </div>
  );
}
```

### Rate Limit Handling

The hook specifically handles 429 Too Many Requests responses by parsing the Retry-After header:

```typescript
// Handle 429 rate limit with proper backoff using the Retry-After header
if (error.response?.status === 429) {
  const retryAfter = error.response.headers['retry-after'];
  const retryDelayMs = retryAfter ? parseInt(retryAfter) * 1000 : retryDelay * Math.pow(backoffFactor, attempt - 1);
  
  if (shouldRetry(error, retryCount - attempt + 1)) {
    console.warn(`Rate limited. Retrying after ${retryDelayMs}ms...`);
    await new Promise(resolve => setTimeout(resolve, retryDelayMs));
    return executeRequest();
  }
}
```

This ensures we respect the server's rate limiting policy while providing the best possible user experience.

## Usage

To use the resilient API client in your components:

```tsx
import { useApi } from '../../hooks/useApi';

function MyComponent() {
  const { get, post, isLoading, error } = useApi({
    // Optional custom configuration
    retryCount: 5,
    circuitBreakerTimeout: 60000, // 1 minute
  });
  
  const handleSubmit = async (data) => {
    try {
      const response = await post('/api/endpoint', data);
      // Handle success
    } catch (err) {
      // Handle specific error cases not covered by the hook
    }
  };
  
  // Component rendering with isLoading and error states
}
```

## Testing Resilience Patterns

The resilience patterns can be tested with the following approaches:

1. **Mock Service Worker**: Simulate API responses, including errors and delays
2. **Network Throttling**: Use browser dev tools to simulate slow connections
3. **Chaos Testing**: Randomly inject failures into the API layer during testing
4. **Offline Testing**: Toggle device offline mode to test queuing and sync

Example test case:

```typescript
test('should retry on 503 error', async () => {
  // Mock server that returns 503 twice then succeeds
  server.use(
    rest.get('/api/tournaments', (req, res, ctx) => {
      if (requestCount++ < 2) {
        return res(ctx.status(503));
      }
      return res(ctx.json({ data: mockTournaments }));
    })
  );
  
  // Render component
  render(<TournamentList />);
  
  // Initially shows loading
  expect(screen.getByTestId('loading-spinner')).toBeInTheDocument();
  
  // Eventually shows the data
  await waitFor(() => {
    expect(screen.getByText('Tournament 1')).toBeInTheDocument();
  });
});
```

## Monitoring and Observability

The resilience patterns include logging and telemetry for monitoring:

1. **Failed Request Logging**: Failed requests are logged with context
2. **Retry Attempts**: Each retry is logged with attempt count and delay
3. **Circuit Breaker Events**: Circuit open/close events are tracked
4. **Offline Queue Size**: Size of offline queue is monitored

These metrics are sent to Application Insights for monitoring and alerting.

## Conclusion

These resilience patterns ensure that the Golf Tournament Organizer frontend can gracefully handle transient errors, network issues, and service disruptions, providing a reliable user experience even in challenging conditions.
