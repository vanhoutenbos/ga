# Backend Observability Guide

This document describes the observability implementation in the Golf Tournament Organizer's backend services, focusing on tracing, logging, and monitoring.

## Overview

Our backend observability strategy is built around distributed tracing, structured logging, and Application Insights monitoring. This approach helps us identify issues across service boundaries, understand request flows, and monitor application health.

## Key Components

1. **Correlation IDs**: Unique identifiers that follow requests across service boundaries
2. **Structured Logging**: Consistent, machine-parsable log format with context
3. **Distributed Tracing**: End-to-end visibility of request flows
4. **Application Insights**: Centralized monitoring and alerting
5. **Custom Dimensions**: Additional context added to telemetry for debugging

## Correlation IDs

### Implementation

Every request receives a unique correlation ID, either from the incoming request headers or generated if not present:

```csharp
private string GetOrCreateCorrelationId(FunctionContext context)
{
    // Try to get HTTP request data
    var httpRequestData = GetHttpRequestData(context);
    var correlationId = httpRequestData?.Headers.TryGetValues("X-Correlation-ID", out var values) == true
        ? values.First()
        : Guid.NewGuid().ToString();
    
    // Enrich the function context with the correlation ID
    context.Items["CorrelationId"] = correlationId;
    
    return correlationId;
}
```

### Headers

The API adds correlation headers to all responses:

- `X-Correlation-ID`: Unique identifier for the entire request chain
- `X-Request-ID`: Unique identifier for the specific request

These headers are used for tracing requests across multiple services and for correlating client errors with server logs.

### Propagation

Correlation IDs are propagated:

1. From client to server via request headers
2. From server to client via response headers
3. Between services via HTTP headers and messaging properties
4. To Supabase via custom headers in API calls

## Structured Logging

All logs follow a consistent JSON structure with standard fields:

```json
{
  "timestamp": "2023-05-15T13:45:22.123Z",
  "level": "Information",
  "message": "Tournament created successfully",
  "correlationId": "c1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "function": "CreateTournament",
  "userId": "user-123",
  "tenantId": "tenant-456",
  "tournamentId": "tournament-789",
  "elapsedMs": 123,
  "properties": {
    "custom": "value"
  }
}
```

### Log Levels

We use standard log levels consistently across the application:

- **Trace**: Detailed debugging information
- **Debug**: Internal system events, not usually visible in production
- **Information**: Notable events in the application (default level in production)
- **Warning**: Non-critical issues that should be reviewed
- **Error**: Failures that affect the current operation but not the entire application
- **Critical**: Failures that require immediate attention

### Context Enrichment

Logs are automatically enriched with context:

```csharp
// Log with context
_logger.LogInformation("Tournament {TournamentId} created for tenant {TenantId} by user {UserId}",
    tournament.Id, tournament.TenantId, userId);
```

## Distributed Tracing

Azure Functions integrate with Application Insights for distributed tracing:

```csharp
using var operation = new TelemetryClientExtensions(context, _telemetryClient)
    .StartOperation<RequestTelemetry>("HttpRequest", correlationId, requestId);

// Add standard properties to the telemetry
operation.Telemetry.Properties["FunctionName"] = context.FunctionDefinition.Name;
operation.Telemetry.Properties["RequestId"] = requestId;
```

### Dependency Tracking

Dependencies like Supabase API calls are tracked as part of the trace:

```csharp
using (_telemetryClient.StartOperation<DependencyTelemetry>(
    "Supabase", "tournaments", "GET"))
{
    // Supabase API call
}
```

### Custom Operations

Custom operations are tracked for business-level tracing:

```csharp
using var op = _telemetryClient.StartOperation<DependencyTelemetry>(
    "BusinessOperation", "TournamentScoring");
op.Telemetry.Properties["TournamentId"] = tournamentId;

// Business logic execution

// Set success/failure
op.Telemetry.Success = success;
```

## Application Insights Integration

Application Insights provides centralized monitoring and alerting:

### Custom Metrics

Custom metrics track business and technical KPIs:

```csharp
// Track tournament creation as a metric
_telemetryClient.TrackMetric("Tournaments.Created", 1);

// Track API response time
_telemetryClient.TrackMetric("API.ResponseTime", stopwatch.ElapsedMilliseconds);
```

### Exception Tracking

Exceptions are tracked with full context:

```csharp
try
{
    // Business logic
}
catch (Exception ex)
{
    var exceptionTelemetry = new ExceptionTelemetry(ex)
    {
        Properties = 
        {
            ["CorrelationId"] = correlationId,
            ["TournamentId"] = tournamentId,
            ["UserId"] = userId
        }
    };
    _telemetryClient.TrackException(exceptionTelemetry);
    
    throw; // Rethrow or handle
}
```

### Availability Tests

Application Insights availability tests monitor critical API endpoints:

1. **Ping tests**: Basic HTTP endpoint health checks (every 5 minutes)
2. **Function invocation tests**: End-to-end tests that verify business functionality (every 15 minutes)

## Alert Configuration

Alerts are configured for the following conditions:

| Alert | Condition | Severity | Action |
|-------|-----------|----------|--------|
| High Error Rate | Error percentage > 5% over 5 minutes | 1 - Critical | Email, SMS, Teams notification |
| API Response Time | 95th percentile > 1000ms over 10 minutes | 2 - Warning | Email notification |
| Function Failure | Any function failure | 2 - Warning | Email notification |
| Storage Quota | Storage usage > 80% | 3 - Information | Email notification |
| Authentication Failures | > 10 failures in 5 minutes | 2 - Warning | Email notification |

## Dashboards and Monitoring

Custom Application Insights dashboards provide visibility into system health:

1. **Operation Dashboard**: Current performance and error rates
2. **User Experience Dashboard**: Response times and client-side metrics
3. **Business Dashboard**: Tournament creation rates, active users, etc.

## Supabase Integration

Supabase interactions are tracked through dedicated monitoring:

```csharp
public async Task<Tournament> GetTournamentAsync(string tournamentId)
{
    using var operation = _telemetryClient.StartOperation<DependencyTelemetry>(
        "Supabase", "tournaments", "GET");
    operation.Telemetry.Properties["TournamentId"] = tournamentId;
    
    try
    {
        var startTime = Stopwatch.GetTimestamp();
        
        // Supabase API call
        var result = await _supabaseClient
            .From<Tournament>()
            .Where(t => t.Id == tournamentId)
            .Get();
            
        // Track request timing
        var elapsed = Stopwatch.GetElapsedTime(startTime);
        _telemetryClient.TrackMetric("Supabase.QueryDuration", elapsed.TotalMilliseconds);
        
        operation.Telemetry.Success = true;
        return result;
    }
    catch (Exception ex)
    {
        operation.Telemetry.Success = false;
        _telemetryClient.TrackException(ex, new Dictionary<string, string>
        {
            ["TournamentId"] = tournamentId,
            ["Operation"] = "GetTournament"
        });
        throw;
    }
}
```

## Local Development

For local development, we provide:

1. **Local Application Insights**: Using the Application Insights SDK in development mode
2. **Structured Log Console Output**: Formatting similar to production logs
3. **Debug Logs**: More verbose than production

## Implementation in Azure Functions

Azure Functions configuration for observability:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        // Add correlation middleware
        builder.UseMiddleware<CorrelationMiddleware>();
        
        // Configure Application Insights
        builder.ConfigureApplicationInsights();
    })
    .ConfigureServices(services =>
    {
        // Add Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        
        // Add telemetry initializer
        services.AddSingleton<ITelemetryInitializer, EnvironmentTelemetryInitializer>();
    })
    .Build();
```

## Conclusion

This observability implementation ensures we can effectively monitor, debug, and troubleshoot the Golf Tournament Organizer's backend services across distributed components, providing visibility into both technical and business operations.
