# Azure Functions for Conflict Resolution

This document details how to optimize Azure Functions for handling data synchronization and conflict resolution in the Golf Tournament Organizer application. It builds upon our existing conflict resolution strategy and Azure infrastructure to provide specific guidance on implementing efficient, scalable synchronization endpoints.

## Table of Contents
1. [Synchronization Architecture](#synchronization-architecture)
2. [Optimized Function Structure](#optimized-function-structure)
3. [Performance Considerations](#performance-considerations)
4. [Error Handling and Resilience](#error-handling-and-resilience)
5. [Monitoring and Diagnostics](#monitoring-and-diagnostics)
6. [Implementation Examples](#implementation-examples)
7. [Testing Strategy](#testing-strategy)

## Synchronization Architecture

The synchronization process leverages Azure Functions as the API layer between the client-side offline storage (IndexedDB) and the server-side database (Supabase PostgreSQL):

```
┌────────────────┐       ┌────────────────────┐       ┌─────────────────┐
│                │       │                    │       │                 │
│  React PWA     │◄─────►│  Azure Functions   │◄─────►│  Supabase       │
│  (IndexedDB)   │       │  (.NET 8)          │       │  (PostgreSQL)   │
│                │       │                    │       │                 │
└────────────────┘       └────────────────────┘       └─────────────────┘
       │                          ▲                           │
       │                          │                           │
       │                          │                           │
       ▼                          │                           ▼
┌────────────────┐                │                 ┌─────────────────┐
│                │                │                 │                 │
│  Service       │                │                 │  Row-Level      │
│  Worker        │────────────────┘                 │  Security       │
│  (Background   │                                  │  Policies       │
│  Sync)         │                                  │                 │
└────────────────┘                                  └─────────────────┘
```

### Key Components

1. **Sync Endpoint Functions**:
   - Batch Processing Function: Handles bulk synchronization requests
   - Entity-Specific Sync Functions: Specialized handling for different entity types
   - Conflict Detection Function: Identifies potential data conflicts
   - Conflict Resolution Function: Applies resolution strategies

2. **Data Flow**:
   - Client collects changes made offline
   - Changes are sent to Azure Functions in batched requests
   - Functions validate and process changes with Supabase
   - Responses include resolved conflicts and server updates

## Optimized Function Structure

To optimize the sync process, our Azure Functions follow these patterns:

### 1. Batch Processing Pattern

```csharp
[Function("SyncBatch")]
public async Task<HttpResponseData> SyncBatch(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/batch")] HttpRequestData req)
{
    // Static clients and connection pooling
    var supabaseClient = _supabaseClientFactory.CreateClient();
    
    // Deserialize batch request
    var syncRequest = await JsonSerializer.DeserializeAsync<SyncBatchRequest>(
        req.Body, _jsonOptions);
    
    // Process in parallel where safe
    var tasks = new List<Task<SyncItemResult>>();
    foreach (var group in syncRequest.Changes.GroupBy(c => c.EntityType))
    {
        // Process each entity type with the appropriate strategy
        tasks.Add(ProcessEntityTypeAsync(group.Key, group.ToList(), supabaseClient));
    }
    
    // Wait for all processing to complete
    var results = await Task.WhenAll(tasks);
    
    // Combine results and return
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new SyncBatchResponse
    {
        Results = results.SelectMany(r => r.Items).ToList(),
        ServerTimestamp = DateTime.UtcNow
    });
    
    return response;
}
```

### 2. Entity-Specific Processing

```csharp
private async Task<SyncItemResult> ProcessEntityTypeAsync(
    string entityType, 
    List<SyncItem> items, 
    SupabaseClient client)
{
    switch (entityType)
    {
        case "player":
            return await _playerSyncProcessor.ProcessAsync(items, client);
        case "score":
            return await _scoreSyncProcessor.ProcessAsync(items, client);
        case "tournament":
            return await _tournamentSyncProcessor.ProcessAsync(items, client);
        // Additional entity types
        default:
            return new SyncItemResult 
            { 
                Items = items.Select(i => new SyncResultItem 
                { 
                    Id = i.Id, 
                    Status = "error",
                    Message = "Unknown entity type" 
                }).ToList()
            };
    }
}
```

### 3. Conflict Resolution Implementation

```csharp
public class ScoreSyncProcessor : IEntitySyncProcessor
{
    public async Task<SyncItemResult> ProcessAsync(
        List<SyncItem> items, 
        SupabaseClient client)
    {
        var results = new List<SyncResultItem>();
        
        foreach (var item in items)
        {
            // Check for conflicts
            var existingItem = await client.From<Score>()
                .Where(s => s.Id == item.Id)
                .Select("id", "updated_at", "last_modified_by", "last_modified_client_id", "is_official_score")
                .Single();
                
            if (existingItem != null)
            {
                // Apply conflict resolution strategy
                var resolution = ResolveConflict(item, existingItem);
                
                if (resolution.Action == "update_server")
                {
                    // Client wins - update server
                    await client.From<Score>().Upsert(item.Data);
                    results.Add(new SyncResultItem 
                    { 
                        Id = item.Id, 
                        Status = "success",
                        Resolution = "client_update_applied" 
                    });
                }
                else
                {
                    // Server wins - return server data
                    var fullServerData = await client.From<Score>()
                        .Where(s => s.Id == item.Id)
                        .Single();
                        
                    results.Add(new SyncResultItem 
                    { 
                        Id = item.Id, 
                        Status = "conflict",
                        Resolution = "server_data_returned",
                        ServerData = fullServerData
                    });
                }
            }
            else
            {
                // No conflict - simply insert
                await client.From<Score>().Insert(item.Data);
                results.Add(new SyncResultItem 
                { 
                    Id = item.Id, 
                    Status = "success",
                    Resolution = "created" 
                });
            }
        }
        
        return new SyncItemResult { Items = results };
    }
    
    private ConflictResolution ResolveConflict(SyncItem clientItem, Score serverItem)
    {
        // Implement timestamp comparison
        var clientTimestamp = DateTime.Parse(clientItem.Data.GetProperty("updated_at").GetString());
        var serverTimestamp = serverItem.UpdatedAt;
        
        // Check if client is official scorer
        bool isClientOfficialScorer = clientItem.Data.TryGetProperty("is_official_score", out var prop) && 
                                     prop.GetBoolean();
        
        // Official scorer always wins
        if (isClientOfficialScorer && !serverItem.IsOfficialScore)
        {
            return new ConflictResolution { Action = "update_server" };
        }
        
        // Apply last-edit-wins
        if (clientTimestamp > serverTimestamp)
        {
            return new ConflictResolution { Action = "update_server" };
        }
        
        return new ConflictResolution { Action = "keep_server" };
    }
}
```

## Performance Considerations

For optimal performance in handling sync requests:

### 1. Memory Optimization

```csharp
// Object pooling for sync processors
private static readonly ObjectPool<ScoreSyncProcessor> _scoreSyncProcessorPool =
    new DefaultObjectPool<ScoreSyncProcessor>(new DefaultPooledObjectPolicy<ScoreSyncProcessor>());

private async Task<SyncItemResult> ProcessScoresAsync(List<SyncItem> items, SupabaseClient client)
{
    var processor = _scoreSyncProcessorPool.Get();
    try
    {
        return await processor.ProcessAsync(items, client);
    }
    finally
    {
        _scoreSyncProcessorPool.Return(processor);
    }
}
```

### 2. Connection Pooling

```csharp
// In Startup.cs
services.AddSingleton<ISupabaseClientFactory>(sp => 
{
    return new SupabaseClientFactory(
        _configuration["Supabase:Url"],
        _configuration["Supabase:Key"],
        new SupabaseOptions 
        { 
            AutoConnectRealtime = false,
            AutoRefreshToken = false 
        });
});

// Factory Implementation
public class SupabaseClientFactory : ISupabaseClientFactory
{
    private readonly string _url;
    private readonly string _key;
    private readonly SupabaseOptions _options;
    private readonly ConcurrentBag<SupabaseClient> _clientPool = new();
    private readonly SemaphoreSlim _poolLock = new(1, 1);
    private const int MaxPoolSize = 10;
    
    public SupabaseClientFactory(string url, string key, SupabaseOptions options)
    {
        _url = url;
        _key = key;
        _options = options;
    }
    
    public SupabaseClient CreateClient()
    {
        if (_clientPool.TryTake(out var client))
        {
            return client;
        }
        
        return new SupabaseClient(_url, _key, _options);
    }
    
    public void ReturnClient(SupabaseClient client)
    {
        if (_clientPool.Count < MaxPoolSize)
        {
            _clientPool.Add(client);
        }
    }
}
```

### 3. Efficient Data Serialization

```csharp
// Shared JSON options to reduce allocations
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// In Function
await response.WriteAsJsonAsync(result, _jsonOptions);
```

## Error Handling and Resilience

The sync endpoints implement robust error handling and resilience patterns:

### 1. Retry Policies

```csharp
// Using Polly for resilient database operations
private static readonly AsyncPolicy _retryPolicy = Policy
    .Handle<SupabaseException>()
    .WaitAndRetryAsync(
        3, 
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (exception, timespan, retryCount, context) =>
        {
            _logger.LogWarning(
                "Retry {RetryCount} after {Timespan}s due to {ExceptionType}: {ExceptionMessage}", 
                retryCount, 
                timespan.TotalSeconds,
                exception.GetType().Name,
                exception.Message);
        });

// In sync processor
public async Task<SyncItemResult> ProcessWithResilienceAsync(
    List<SyncItem> items, 
    SupabaseClient client)
{
    return await _retryPolicy.ExecuteAsync(async () => 
    {
        return await ProcessAsync(items, client);
    });
}
```

### 2. Transaction Management

```csharp
// Ensure atomic batch updates using transactions
public async Task<SyncBatchResponse> ProcessBatchWithTransactionAsync(SyncBatchRequest request)
{
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();
    
    using var transaction = await connection.BeginTransactionAsync();
    
    try
    {
        // Process each entity in the batch
        var results = new List<SyncResultItem>();
        foreach (var item in request.Changes)
        {
            var result = await ProcessItemInTransactionAsync(item, connection, transaction);
            results.Add(result);
        }
        
        // Commit all changes
        await transaction.CommitAsync();
        
        return new SyncBatchResponse
        {
            Results = results,
            ServerTimestamp = DateTime.UtcNow
        };
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error processing sync batch");
        throw;
    }
}
```

### 3. Partial Success Handling

```csharp
[Function("SyncBatch")]
public async Task<HttpResponseData> SyncBatch(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/batch")] HttpRequestData req)
{
    var syncRequest = await JsonSerializer.DeserializeAsync<SyncBatchRequest>(req.Body);
    var results = new List<SyncResultItem>();
    var hasErrors = false;
    
    // Process each change independently to allow partial success
    foreach (var change in syncRequest.Changes)
    {
        try
        {
            var result = await ProcessSyncItemAsync(change);
            results.Add(result);
            
            if (result.Status == "error")
            {
                hasErrors = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sync item {Id}", change.Id);
            results.Add(new SyncResultItem
            {
                Id = change.Id,
                Status = "error",
                Message = "Internal server error"
            });
            hasErrors = true;
        }
    }
    
    var response = req.CreateResponse(hasErrors ? HttpStatusCode.MultiStatus : HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new SyncBatchResponse
    {
        Results = results,
        ServerTimestamp = DateTime.UtcNow
    });
    
    return response;
}
```

## Monitoring and Diagnostics

To effectively monitor sync operations:

### 1. Custom Metrics

```csharp
// In sync processor
private void TrackSyncMetrics(SyncItemResult result, string entityType, long processingTime)
{
    var properties = new Dictionary<string, string>
    {
        ["EntityType"] = entityType
    };
    
    var metrics = new Dictionary<string, double>
    {
        ["ProcessingTimeMs"] = processingTime,
        ["ItemCount"] = result.Items.Count,
        ["SuccessCount"] = result.Items.Count(r => r.Status == "success"),
        ["ConflictCount"] = result.Items.Count(r => r.Status == "conflict"),
        ["ErrorCount"] = result.Items.Count(r => r.Status == "error")
    };
    
    _telemetryClient.TrackEvent("SyncBatchProcessed", properties, metrics);
}
```

### 2. Conflict Logging

```csharp
private void LogConflict(SyncItem clientItem, object serverItem)
{
    _logger.LogInformation(
        "Conflict detected for {EntityType} {Id}. Client timestamp: {ClientTimestamp}, Server timestamp: {ServerTimestamp}",
        clientItem.EntityType,
        clientItem.Id,
        clientItem.Data.GetProperty("updated_at").GetString(),
        ((dynamic)serverItem).UpdatedAt
    );
    
    // Log detailed conflict information for analysis
    _telemetryClient.TrackEvent("SyncConflictDetected", new Dictionary<string, string>
    {
        ["EntityType"] = clientItem.EntityType,
        ["EntityId"] = clientItem.Id,
        ["ClientModifiedBy"] = clientItem.Data.TryGetProperty("last_modified_by", out var prop) ? prop.GetString() : "unknown",
        ["Resolution"] = clientItem.Data.GetProperty("updated_at").GetDateTimeOffset() >
                         ((dynamic)serverItem).UpdatedAt ? "ClientWins" : "ServerWins"
    });
}
```

### 3. Performance Tracking

```csharp
[Function("SyncBatch")]
public async Task<HttpResponseData> SyncBatch(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/batch")] HttpRequestData req)
{
    var stopwatch = Stopwatch.StartNew();
    
    // Process sync batch
    var result = await ProcessSyncBatchAsync(req);
    
    stopwatch.Stop();
    
    // Log performance metrics
    _logger.LogInformation(
        "Sync batch processed in {ElapsedMs}ms. Items: {ItemCount}, Conflicts: {ConflictCount}",
        stopwatch.ElapsedMilliseconds,
        result.TotalItems,
        result.ConflictCount
    );
    
    return result.HttpResponse;
}
```

## Implementation Examples

### Sync Function Example

```csharp
[Function("SyncTournamentData")]
public async Task<HttpResponseData> SyncTournamentData(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournaments/{tournamentId}/sync")] HttpRequestData req,
    string tournamentId)
{
    // Validate authorization
    var user = await _authService.ValidateTokenAsync(req);
    if (user == null)
    {
        return req.CreateResponse(HttpStatusCode.Unauthorized);
    }
    
    // Verify tournament access permissions
    if (!await _tournamentService.CanAccessTournamentAsync(tournamentId, user.Id))
    {
        return req.CreateResponse(HttpStatusCode.Forbidden);
    }
    
    try
    {
        var syncRequest = await JsonSerializer.DeserializeAsync<TournamentSyncRequest>(
            req.Body, _jsonOptions);
            
        if (syncRequest == null)
        {
            return req.CreateBadRequestResponse("Invalid sync request");
        }
        
        // Validate tournament timestamp to detect major conflicts
        var serverTournament = await _tournamentService.GetTournamentWithTimestampAsync(tournamentId);
        if (serverTournament == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        
        // Check if client data is based on an outdated version
        var clientBaseVersion = DateTimeOffset.Parse(syncRequest.BaseTimestamp);
        if (clientBaseVersion < serverTournament.UpdatedAt.AddMinutes(-30))
        {
            // Significant time difference, return full dataset with conflict
            var fullData = await _tournamentService.GetFullTournamentDataAsync(tournamentId);
            
            var response = req.CreateResponse(HttpStatusCode.Conflict);
            await response.WriteAsJsonAsync(new TournamentSyncConflictResponse
            {
                Message = "Client data is significantly outdated",
                ServerData = fullData,
                ServerTimestamp = DateTimeOffset.UtcNow
            }, _jsonOptions);
            
            return response;
        }
        
        // Process changes with entity-specific handlers
        var result = await _syncService.ProcessTournamentSyncAsync(
            tournamentId, 
            syncRequest.Changes,
            user);
            
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new TournamentSyncResponse
        {
            Results = result.Results,
            ServerChanges = result.ServerChanges,
            ServerTimestamp = DateTimeOffset.UtcNow
        }, _jsonOptions);
        
        return response;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error syncing tournament {TournamentId}", tournamentId);
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
```

### Field-Level Merging Example

```csharp
public class ScoreFieldMerger
{
    public Score MergeFields(Score clientScore, Score serverScore)
    {
        // Start with server version
        var mergedScore = new Score
        {
            Id = serverScore.Id,
            PlayerId = serverScore.PlayerId,
            TournamentId = serverScore.TournamentId,
            RoundNumber = serverScore.RoundNumber,
            CreatedAt = serverScore.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        
        // Examine each field and take the latest version
        if (GetFieldTimestamp(clientScore, "Strokes") > GetFieldTimestamp(serverScore, "Strokes"))
        {
            mergedScore.Strokes = clientScore.Strokes;
            mergedScore.SetFieldTimestamp("Strokes", GetFieldTimestamp(clientScore, "Strokes"));
        }
        else
        {
            mergedScore.Strokes = serverScore.Strokes;
            mergedScore.SetFieldTimestamp("Strokes", GetFieldTimestamp(serverScore, "Strokes"));
        }
        
        // Handle notes field separately - more complex merge
        if (clientScore.Notes != null && serverScore.Notes != null &&
            clientScore.Notes != serverScore.Notes)
        {
            // Both have notes - check if we should append or replace
            if (GetFieldTimestamp(clientScore, "Notes") > GetFieldTimestamp(serverScore, "Notes").AddMinutes(30))
            {
                // Client timestamp is significantly newer - replace
                mergedScore.Notes = clientScore.Notes;
                mergedScore.SetFieldTimestamp("Notes", GetFieldTimestamp(clientScore, "Notes"));
            }
            else
            {
                // Close enough in time - append notes with attribution
                mergedScore.Notes = $"{serverScore.Notes}\n---\n{clientScore.Notes}";
                mergedScore.SetFieldTimestamp("Notes", DateTime.UtcNow);
            }
        }
        else if (clientScore.Notes != null)
        {
            mergedScore.Notes = clientScore.Notes;
            mergedScore.SetFieldTimestamp("Notes", GetFieldTimestamp(clientScore, "Notes"));
        }
        else
        {
            mergedScore.Notes = serverScore.Notes;
            mergedScore.SetFieldTimestamp("Notes", GetFieldTimestamp(serverScore, "Notes"));
        }
        
        // Continue with other fields...
        
        return mergedScore;
    }
    
    private DateTime GetFieldTimestamp(Score score, string fieldName)
    {
        // Access field-level timestamps
        var timestampField = $"{fieldName.ToLower()}_timestamp";
        var property = score.GetType().GetProperty(timestampField);
        
        return property != null 
            ? (DateTime)property.GetValue(score) 
            : score.UpdatedAt;
    }
}
```

## Testing Strategy

### 1. Unit Tests

```csharp
[Fact]
public async Task ProcessScoreSync_WhenClientHasNewerTimestamp_ClientWins()
{
    // Arrange
    var mockSupabaseClient = new Mock<SupabaseClient>();
    var processor = new ScoreSyncProcessor();
    
    var clientScore = new SyncItem
    {
        Id = "score-123",
        EntityType = "score",
        Data = JsonDocument.Parse(@"{
            ""id"": ""score-123"",
            ""strokes"": 5,
            ""updated_at"": ""2023-06-15T10:30:00Z"",
            ""last_modified_by"": ""user-1""
        }").RootElement
    };
    
    var serverScore = new Score
    {
        Id = "score-123",
        Strokes = 4,
        UpdatedAt = DateTime.Parse("2023-06-15T10:20:00Z"),
        LastModifiedBy = "user-2"
    };
    
    mockSupabaseClient.Setup(c => c.From<Score>()
        .Where(It.IsAny<Func<Score, bool>>())
        .Single())
        .ReturnsAsync(serverScore);
        
    mockSupabaseClient.Setup(c => c.From<Score>().Upsert(It.IsAny<object>()))
        .ReturnsAsync(new List<Score> { /* updated score */ });
    
    // Act
    var result = await processor.ProcessAsync(new List<SyncItem> { clientScore }, mockSupabaseClient.Object);
    
    // Assert
    Assert.Single(result.Items);
    Assert.Equal("success", result.Items[0].Status);
    Assert.Equal("client_update_applied", result.Items[0].Resolution);
    
    mockSupabaseClient.Verify(c => c.From<Score>().Upsert(It.IsAny<object>()), Times.Once);
}
```

### 2. Integration Tests

```csharp
[Fact]
public async Task SyncEndpoint_ProcessesBatchCorrectly()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    
    // Create test data in the database
    await SetupTestDataAsync();
    
    // Create sync request with multiple entity types
    var syncRequest = new SyncBatchRequest
    {
        Changes = new List<SyncItem>
        {
            new SyncItem
            {
                Id = "score-123",
                EntityType = "score",
                Data = JsonDocument.Parse(@"{
                    ""id"": ""score-123"",
                    ""strokes"": 5,
                    ""updated_at"": ""2023-06-15T10:30:00Z""
                }").RootElement
            },
            new SyncItem
            {
                Id = "player-456",
                EntityType = "player",
                Data = JsonDocument.Parse(@"{
                    ""id"": ""player-456"",
                    ""name"": ""Updated Name"",
                    ""updated_at"": ""2023-06-15T11:00:00Z""
                }").RootElement
            }
        }
    };
    
    // Act
    var response = await client.PostAsJsonAsync("/api/sync/batch", syncRequest);
    
    // Assert
    response.EnsureSuccessStatusCode();
    var syncResponse = await response.Content.ReadFromJsonAsync<SyncBatchResponse>();
    
    Assert.NotNull(syncResponse);
    Assert.Equal(2, syncResponse.Results.Count);
    
    // Verify database state was updated correctly
    var updatedScore = await GetScoreFromDatabaseAsync("score-123");
    Assert.Equal(5, updatedScore.Strokes);
    
    var updatedPlayer = await GetPlayerFromDatabaseAsync("player-456");
    Assert.Equal("Updated Name", updatedPlayer.Name);
}
```

### 3. Load Testing

```csharp
[Fact]
public async Task SyncEndpoint_HandlesHighVolume()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();
    
    // Create large sync request
    var syncRequest = new SyncBatchRequest
    {
        Changes = GenerateLargeChangeSet(500) // 500 changes
    };
    
    // Act
    var stopwatch = Stopwatch.StartNew();
    var response = await client.PostAsJsonAsync("/api/sync/batch", syncRequest);
    stopwatch.Stop();
    
    // Assert
    response.EnsureSuccessStatusCode();
    var syncResponse = await response.Content.ReadFromJsonAsync<SyncBatchResponse>();
    
    Assert.NotNull(syncResponse);
    Assert.Equal(500, syncResponse.Results.Count);
    Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should process in under 5 seconds
}
```

By implementing these patterns and optimizations, the Azure Functions will efficiently handle the conflict resolution process for the Golf Tournament Organizer application, ensuring smooth synchronization even during tournament days with high traffic and potential offline scenarios.
