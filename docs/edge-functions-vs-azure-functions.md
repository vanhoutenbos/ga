# Edge Functions vs Azure Functions Usage Guide

This document outlines when to use Supabase Edge Functions versus Azure Functions in the Golf Tournament Organizer application. It provides guidelines, use cases, and implementation details for each service.

## Overview

The Golf Tournament Organizer application uses two serverless function platforms:

1. **Supabase Edge Functions**: For lightweight, Supabase-proximate operations
2. **Azure Functions (.NET 8)**: For complex business logic and .NET-specific operations

Each platform has different strengths, constraints, and ideal use cases.

## Decision Framework

When determining which platform to use for a particular function, consider these factors:

| Factor | Supabase Edge Functions | Azure Functions |
|--------|------------------------|-----------------|
| **Language** | TypeScript/JavaScript | C#/.NET 8 |
| **Proximity to Data** | Direct Supabase access | Via API/SDK |
| **Execution Time** | Max 2 seconds (Free tier) | Up to 10 minutes (Consumption plan) |
| **Memory** | Limited (1GB) | Up to 1.5GB (can be increased) |
| **Cold Start** | Fast | Moderate (optimized in .NET 8) |
| **Access to Supabase** | Direct, same region | Cross-cloud, via API |
| **Ecosystem** | Node.js libraries | Full .NET ecosystem |
| **Authentication** | Direct Supabase Auth integration | JWT validation required |
| **Monitoring** | Basic | Advanced (Application Insights) |

## Function Assignment Matrix

| Function Type | Platform | Rationale |
|--------------|----------|-----------|
| Real-time data transformations | Supabase Edge Functions | Proximity to data source |
| Webhook receivers | Supabase Edge Functions | Low latency, publicly accessible |
| Notification generators | Supabase Edge Functions | Access to database events |
| Search operations | Azure Functions | Complex queries, caching |
| Tournament management | Azure Functions | Complex business logic |
| Reporting and analytics | Azure Functions | Computation intensive |
| PDF generation | Azure Functions | Library support, longer runtime |
| User authentication workflows | Supabase Edge Functions | Direct auth integration |

## Supabase Edge Functions Implementation

### Configuration and Deployment

Edge Functions are deployed directly from the Supabase CLI:

```bash
# Install Supabase CLI
npm install -g supabase

# Login to Supabase
supabase login

# Initialize Edge Functions in project
supabase init

# Create a new Edge Function
supabase functions new notify-tournament-update

# Deploy the function
supabase functions deploy notify-tournament-update --project-ref abc123
```

### Structure for Edge Functions

```
supabase/
├── functions/
│   ├── notify-tournament-update/
│   │   └── index.ts
│   ├── process-score-submission/
│   │   └── index.ts
│   └── update-leaderboard/
│       └── index.ts
└── config.toml
```

### Example Edge Function

```typescript
// supabase/functions/notify-tournament-update/index.ts
import { serve } from 'https://deno.land/std@0.131.0/http/server.ts'
import { createClient } from 'https://esm.sh/@supabase/supabase-js@2.0.0'

serve(async (req) => {
  // Create Supabase client
  const supabaseClient = createClient(
    Deno.env.get('SUPABASE_URL') ?? '',
    Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') ?? ''
  )

  // Extract data from request
  const { tournamentId, updateType } = await req.json()

  try {
    // Get tournament details
    const { data: tournament, error: tournamentError } = await supabaseClient
      .from('tournaments')
      .select('name, tenant_id')
      .eq('id', tournamentId)
      .single()

    if (tournamentError) throw tournamentError

    // Get user emails for notification
    const { data: subscribers, error: subscriberError } = await supabaseClient
      .from('tournament_subscribers')
      .select('user_id')
      .eq('tournament_id', tournamentId)
      .eq('notifications_enabled', true)

    if (subscriberError) throw subscriberError

    // Send notifications
    const notifications = subscribers.map(sub => ({
      user_id: sub.user_id,
      tournament_id: tournamentId,
      message: `Tournament ${tournament.name} has been ${updateType}`,
      created_at: new Date().toISOString()
    }))

    const { error: notificationError } = await supabaseClient
      .from('notifications')
      .insert(notifications)

    if (notificationError) throw notificationError

    return new Response(
      JSON.stringify({ success: true, count: subscribers.length }),
      { headers: { 'Content-Type': 'application/json' } }
    )
  } catch (error) {
    return new Response(
      JSON.stringify({ error: error.message }),
      { status: 400, headers: { 'Content-Type': 'application/json' } }
    )
  }
})
```

## Azure Functions Implementation

### Configuration and Deployment

Azure Functions are created and deployed using Visual Studio or the Azure Functions Core Tools:

```bash
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4

# Create a new function app
func init GolfApp.Functions --worker-runtime dotnet-isolated

# Add a new function
func new --name ProcessScores --template HttpTrigger

# Publish to Azure
func azure functionapp publish golf-app-functions
```

### Structure for Azure Functions

```
src/
└── api/
    └── GolfApp.Functions/
        ├── Functions/
        │   ├── ProcessScoresFunction.cs
        │   ├── GenerateReportFunction.cs
        │   └── SearchTournamentsFunction.cs
        ├── Models/
        ├── Services/
        ├── host.json
        └── local.settings.json
```

### Example Azure Function

```csharp
// src/api/GolfApp.Functions/Functions/ProcessScoresFunction.cs
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GolfApp.Functions.Services;
using GolfApp.Functions.Models;

namespace GolfApp.Functions.Functions
{
    public class ProcessScoresFunction
    {
        private readonly IScoringSummaryService _scoringSummaryService;
        private readonly ILeaderboardService _leaderboardService;
        private readonly ILogger<ProcessScoresFunction> _logger;

        public ProcessScoresFunction(
            IScoringSummaryService scoringSummaryService,
            ILeaderboardService leaderboardService,
            ILoggerFactory loggerFactory)
        {
            _scoringSummaryService = scoringSummaryService;
            _leaderboardService = leaderboardService;
            _logger = loggerFactory.CreateLogger<ProcessScoresFunction>();
        }

        [Function("ProcessScores")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Processing scores");
            
            // Deserialize request
            var scoreSubmission = await req.ReadFromJsonAsync<ScoreSubmissionDto>();
            
            if (scoreSubmission == null)
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid score submission data");
                return badResponse;
            }
            
            try
            {
                // Process the scores with business logic
                var result = await _scoringSummaryService.ProcessScoresAsync(
                    scoreSubmission.TournamentId,
                    scoreSubmission.PlayerId,
                    scoreSubmission.Scores);
                    
                // Update leaderboard with new results
                await _leaderboardService.UpdateLeaderboardAsync(
                    scoreSubmission.TournamentId);
                
                // Create response
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scores");
                
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    error = "Failed to process scores",
                    message = ex.Message
                });
                return errorResponse;
            }
        }
    }
}
```

## Use Cases and Examples

### Supabase Edge Function Use Cases

1. **Real-time Notifications**

```typescript
// When tournament status changes, notify subscribers
serve(async (req) => {
  const { record, type } = await req.json()
  
  if (type === 'UPDATE' && record.old_status !== record.new_status) {
    // Send notifications to subscribers
    // ...
  }
})
```

2. **User Registration Workflows**

```typescript
// Create user profile after signup webhook
serve(async (req) => {
  const { user } = await req.json()
  
  const supabaseAdmin = createClient(
    Deno.env.get('SUPABASE_URL') ?? '',
    Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') ?? ''
  )
  
  // Create user profile
  const { error } = await supabaseAdmin
    .from('profiles')
    .insert({
      id: user.id,
      email: user.email,
      created_at: new Date().toISOString()
    })
    
  // Return response
  if (error) {
    return new Response(JSON.stringify({ error: error.message }), { status: 400 })
  }
  
  return new Response(JSON.stringify({ success: true }))
})
```

3. **Access Code Validation**

```typescript
// Verify tournament access codes
serve(async (req) => {
  const { code, tournamentId } = await req.json()
  const { user } = await getUser(req)
  
  if (!user) {
    return new Response(JSON.stringify({ error: 'Unauthorized' }), { status: 401 })
  }
  
  const supabaseClient = createClient(
    Deno.env.get('SUPABASE_URL') ?? '',
    Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') ?? ''
  )
  
  // Verify code
  const { data, error } = await supabaseClient
    .from('access_codes')
    .select('*')
    .eq('code', code)
    .eq('tournament_id', tournamentId)
    .single()
    
  if (error || !data) {
    return new Response(JSON.stringify({ error: 'Invalid code' }), { status: 400 })
  }
  
  // Check if expired
  if (new Date(data.expires_at) < new Date()) {
    return new Response(JSON.stringify({ error: 'Code expired' }), { status: 400 })
  }
  
  // Update code as used
  await supabaseClient
    .from('access_codes')
    .update({ 
      used_at: new Date().toISOString(),
      used_by: user.id
    })
    .eq('id', data.id)
    
  // Grant access to tournament
  await supabaseClient
    .from('tournament_players')
    .upsert({
      tournament_id: tournamentId,
      player_id: user.id,
      status: 'confirmed',
      joined_at: new Date().toISOString()
    })
    
  return new Response(JSON.stringify({ success: true }))
})
```

### Azure Function Use Cases

1. **Complex Tournament Processing**

```csharp
[Function("CalculateTournamentResults")]
public async Task<HttpResponseData> CalculateResults(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var tournamentId = await req.ReadFromJsonAsync<string>();
    
    _logger.LogInformation("Calculating final results for tournament {TournamentId}", tournamentId);
    
    // Get all scores
    var scores = await _tournamentService.GetAllScoresAsync(tournamentId);
    
    // Calculate results with complex business logic
    var results = await _scoringService.CalculateFinalResultsAsync(
        tournamentId, scores, _tournamentService.GetTournamentFormat(tournamentId));
        
    // Generate PDF scorecards
    var scorecardUrls = await _reportService.GenerateScorecardsAsync(tournamentId, results);
    
    // Generate leaderboard PDF
    var leaderboardUrl = await _reportService.GenerateLeaderboardPdfAsync(tournamentId, results);
    
    // Send email notifications with results
    await _notificationService.SendTournamentCompletionEmailsAsync(tournamentId, results);
    
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new {
        tournamentId,
        playerCount = results.Count,
        scorecardUrls,
        leaderboardUrl
    });
    
    return response;
}
```

2. **Advanced Search and Query**

```csharp
[Function("SearchTournaments")]
public async Task<HttpResponseData> Search(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
{
    // Get search parameters from query string
    var query = req.GetQueryParameters();
    var searchTerm = query["q"] ?? string.Empty;
    var startDate = query.TryGetValue("startDate", out var start) 
        ? DateTime.Parse(start) 
        : DateTime.MinValue;
    var endDate = query.TryGetValue("endDate", out var end)
        ? DateTime.Parse(end)
        : DateTime.MaxValue;
    var page = query.TryGetValue("page", out var p) 
        ? int.Parse(p) 
        : 1;
    var pageSize = query.TryGetValue("pageSize", out var ps)
        ? int.Parse(ps)
        : 20;
    
    // Use search service with caching
    var results = await _searchService.SearchTournamentsAsync(
        searchTerm, startDate, endDate, page, pageSize);
        
    // Build HATEOAS links
    var links = new List<Link>();
    if (results.HasNextPage)
    {
        links.Add(new Link
        {
            Rel = "next",
            Href = $"/api/SearchTournaments?q={searchTerm}&page={page+1}&pageSize={pageSize}"
        });
    }
    
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new {
        results = results.Items,
        totalCount = results.TotalCount,
        page,
        pageSize,
        links
    });
    
    return response;
}
```

3. **Integration with External Systems**

```csharp
[Function("SyncWithHandicapSystem")]
public async Task SyncHandicaps(
    [TimerTrigger("0 0 * * *")] TimerInfo timer) // Daily at midnight
{
    _logger.LogInformation("Starting handicap synchronization");
    
    // Get all active players
    var players = await _playerService.GetActivePlayers();
    
    foreach (var player in players)
    {
        try
        {
            // Call external handicap system API
            var handicapInfo = await _handicapSystemClient.GetPlayerHandicapAsync(
                player.ExternalId);
                
            // Update player handicap in our system
            await _playerService.UpdatePlayerHandicapAsync(
                player.Id, 
                handicapInfo.CurrentHandicap,
                handicapInfo.LastUpdated);
                
            _logger.LogInformation("Updated handicap for player {PlayerId}", player.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync handicap for player {PlayerId}", player.Id);
            await _notificationService.SendHandicapSyncFailureAlert(player, ex.Message);
        }
    }
    
    _logger.LogInformation("Handicap synchronization completed");
}
```

## Integration Between Edge Functions and Azure Functions

### Communication Patterns

1. **HTTP API Calls**

```typescript
// Edge Function calling Azure Function
serve(async (req) => {
  const { tournamentId } = await req.json()
  
  // Call Azure Function
  const response = await fetch(
    `${Deno.env.get('AZURE_FUNCTION_URL')}/api/CalculateTournamentResults`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-functions-key': Deno.env.get('AZURE_FUNCTION_KEY')
      },
      body: JSON.stringify({ tournamentId })
    }
  )
  
  const result = await response.json()
  
  return new Response(JSON.stringify(result), { 
    headers: { 'Content-Type': 'application/json' } 
  })
})
```

2. **Event-Based Integration**

Both Azure Functions and Edge Functions can subscribe to events from a common source (e.g., EventGrid or Event Hub) to create a loosely coupled integration.

## Performance Considerations

### Edge Function Optimization

1. **Minimize Cold Starts**
   - Keep function size small
   - Use warmer mechanisms
   - Consolidate related functionality

2. **Respect Timeout Limits**
   - Free tier: 2 seconds
   - Pro tier: 60 seconds
   - Design for early return patterns

### Azure Function Optimization

1. **Memory and Performance Tuning**
   - Use dependency injection
   - Implement proper caching
   - Consider durable functions for long-running processes

2. **Connection Pooling**
   - Reuse HTTP clients
   - Implement proper database connection pooling
   - Use static clients where appropriate

## Monitoring and Troubleshooting

### Edge Function Logging

```typescript
serve(async (req) => {
  try {
    console.log("Function started", { params: await req.json() })
    
    // Function logic...
    
    console.log("Function completed successfully")
    return new Response(JSON.stringify({ success: true }))
  } catch (error) {
    console.error("Function failed", { error: error.message, stack: error.stack })
    return new Response(JSON.stringify({ error: error.message }), { status: 500 })
  }
})
```

### Azure Function Logging

```csharp
[Function("ProcessPayment")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
{
    var correlationId = Guid.NewGuid().ToString();
    _logger.LogInformation("Payment processing started. CorrelationId: {CorrelationId}", correlationId);
    
    try {
        var payment = await req.ReadFromJsonAsync<PaymentRequest>();
        
        _logger.LogInformation("Processing payment {PaymentId} for {Amount}", 
            payment.Id, payment.Amount);
            
        // Payment processing logic...
        
        _logger.LogInformation("Payment {PaymentId} processed successfully", payment.Id);
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true, correlationId });
        return response;
    }
    catch (Exception ex) {
        _logger.LogError(ex, "Payment processing failed. CorrelationId: {CorrelationId}", correlationId);
        
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteAsJsonAsync(new { 
            error = "Payment processing failed", 
            correlationId,
            message = ex.Message
        });
        return response;
    }
}
```

## Cost Analysis

| Aspect | Supabase Edge Functions | Azure Functions |
|--------|------------------------|-----------------|
| Free Tier | 500K invocations/mo, 2-second timeout | 1M executions/mo, 400,000 GB-s |
| Pro Tier | 2M invocations/mo, 60-second timeout | Pay-as-you-go |
| Consumption Model | Per function invocation | Per execution and memory consumption |
| Cold Start Costs | Minimal | More significant for .NET (improved in .NET 8) |
| Network Costs | Minimal for Supabase interactions | Cross-cloud data transfer fees |

## Conclusion

The Golf Tournament Organizer application uses a strategic combination of Supabase Edge Functions and Azure Functions to optimize for performance, cost, and developer productivity:

- **Supabase Edge Functions**: For lightweight operations closely tied to Supabase data, real-time updates, and basic integrations
- **Azure Functions**: For complex business logic, intensive computations, reporting, and integrations with external systems

This hybrid approach leverages the strengths of each platform while minimizing their limitations, creating a scalable and maintainable serverless architecture.
