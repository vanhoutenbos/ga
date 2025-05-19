# Azure Functions Requirements for Golf Tournament MVP

This document outlines the specific Azure Functions required for the MVP of the Golf Tournament Organizer application, providing a clear implementation path for the serverless backend components.

## Core Azure Function Categories

Based on the application architecture and requirements, the following categories of Azure Functions are essential for the MVP:

1. [Leaderboard and Scoring Calculations](#leaderboard-and-scoring-calculations)
2. [Data Synchronization and Conflict Resolution](#data-synchronization-and-conflict-resolution)
3. [Tournament Management](#tournament-management)
4. [Notifications and External Integrations](#notifications-and-external-integrations)
5. [Batch Processing and Reports](#batch-processing-and-reports)

## Leaderboard and Scoring Calculations

These functions handle complex scoring calculations that should be consistent across all clients.

### 1. Tournament Leaderboard Generator

```csharp
[FunctionName("GetTournamentLeaderboard")]
public static async Task<IActionResult> GetLeaderboard(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tournaments/{tournamentId}/leaderboard")] HttpRequest req,
    string tournamentId,
    ILogger log)
{
    // Get query parameters for filtering options
    string format = req.Query["format"]; // gross, net, stableford
    string flightId = req.Query["flightId"]; // Optional flight filter
    int? round = req.Query["round"].Count > 0 ? int.Parse(req.Query["round"]) : null; // Optional round filter
    
    // Fetch tournament, players, scores from Supabase
    var tournament = await _supabaseClient.From<Tournament>().Where(t => t.Id == tournamentId).Get();
    
    // Get all scores for the tournament
    var scores = await _supabaseClient.From<Score>()
        .Where(s => s.TournamentId == tournamentId)
        .Get();
    
    // Fetch players in the tournament
    var players = await _supabaseClient.From<TournamentPlayer>()
        .Where(p => p.TournamentId == tournamentId)
        .Get();
    
    // Apply flight filter if provided
    if (!string.IsNullOrEmpty(flightId))
    {
        players = players.Where(p => p.FlightId == flightId).ToList();
    }
    
    // Calculate leaderboard based on format
    var leaderboard = CalculateLeaderboard(tournament, players, scores, format, round);
    
    // Sort by rank
    leaderboard = leaderboard.OrderBy(l => l.Position).ToList();
    
    return new OkObjectResult(leaderboard);
}
```

### 2. Score Statistics Calculator

```csharp
[FunctionName("CalculatePlayerStatistics")]
public static async Task<IActionResult> CalculateStats(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "players/{playerId}/tournaments/{tournamentId}/statistics")] HttpRequest req,
    string playerId,
    string tournamentId,
    ILogger log)
{
    // Fetch player scores
    var scores = await _supabaseClient.From<Score>()
        .Where(s => s.TournamentId == tournamentId && s.PlayerId == playerId)
        .Get();
    
    // Calculate various statistics
    var statistics = new PlayerStatistics
    {
        GreenInRegulationPercentage = CalculateGIR(scores),
        FairwaysHitPercentage = CalculateFairwaysHit(scores),
        AveragePuttsPerHole = CalculateAveragePutts(scores),
        ParSavingPercentage = CalculateParSaves(scores),
        ScoringByHolePar = CalculateScoringByPar(scores),
        // Additional statistics...
    };
    
    return new OkObjectResult(statistics);
}
```

### 3. Handicap Adjustment Calculator

```csharp
[FunctionName("CalculateHandicapAdjustment")]
public static async Task<IActionResult> CalculateHandicapAdjustment(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournaments/{tournamentId}/players/{playerId}/handicap-adjustment")] HttpRequest req,
    string tournamentId,
    string playerId,
    ILogger log)
{
    // Get player and their current handicap
    var player = await _supabaseClient.From<TournamentPlayer>()
        .Where(p => p.TournamentId == tournamentId && p.PlayerId == playerId)
        .Single();
    
    // Get tournament information (slope, rating)
    var tournament = await _supabaseClient.From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Include(t => t.Course)
        .Single();
    
    // Get completed scores
    var scores = await _supabaseClient.From<Score>()
        .Where(s => s.TournamentId == tournamentId && s.PlayerId == playerId)
        .Get();
    
    // Calculate handicap adjustment based on World Handicap System formula
    var adjustment = CalculateWHSHandicapChange(
        player.Handicap,
        scores,
        tournament.Course.SlopeRating,
        tournament.Course.CourseRating
    );
    
    return new OkObjectResult(new { 
        CurrentHandicap = player.Handicap,
        Adjustment = adjustment,
        NewHandicap = player.Handicap + adjustment
    });
}
```

## Data Synchronization and Conflict Resolution

These functions manage offline data synchronization and resolve conflicts when multiple users modify the same data.

### 1. Score Synchronization Handler

```csharp
[FunctionName("SyncScores")]
public static async Task<IActionResult> SyncScores(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/scores")] HttpRequest req,
    ILogger log)
{
    // Deserialize the batch of scores to sync
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var syncRequest = JsonConvert.DeserializeObject<ScoreSyncRequest>(requestBody);
    
    var results = new List<SyncResult>();
    
    foreach (var score in syncRequest.Scores)
    {
        // Check for conflicts
        var existingScore = await _supabaseClient.From<Score>()
            .Where(s => s.Id == score.Id)
            .Single();
        
        if (existingScore != null)
        {
            // Determine which version is newer
            var serverTimestamp = DateTime.Parse(existingScore.UpdatedAt);
            var clientTimestamp = DateTime.Parse(score.UpdatedAt);
            
            if (clientTimestamp > serverTimestamp)
            {
                // Client version is newer, update the server
                await _supabaseClient.From<Score>()
                    .Update(score)
                    .Where(s => s.Id == score.Id)
                    .Execute();
                
                results.Add(new SyncResult { 
                    Id = score.Id, 
                    Status = "updated",
                    Entity = "score"
                });
            }
            else
            {
                // Server version is newer, return the server version
                results.Add(new SyncResult { 
                    Id = score.Id, 
                    Status = "conflict",
                    ServerData = existingScore,
                    Entity = "score"
                });
            }
        }
        else
        {
            // New score, insert it
            await _supabaseClient.From<Score>()
                .Insert(score)
                .Execute();
            
            results.Add(new SyncResult { 
                Id = score.Id, 
                Status = "created",
                Entity = "score"
            });
        }
    }
    
    return new OkObjectResult(new { 
        Results = results,
        ServerTimestamp = DateTime.UtcNow.ToString("o")
    });
}
```

### 2. Bulk Synchronization Processor

```csharp
[FunctionName("ProcessBulkSync")]
public static async Task<IActionResult> ProcessBulkSync(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/bulk")] HttpRequest req,
    ILogger log)
{
    // Deserialize the bulk sync request
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var syncRequest = JsonConvert.DeserializeObject<BulkSyncRequest>(requestBody);
    
    // Group by entity type
    var scoreSync = syncRequest.Changes
        .Where(c => c.EntityType == "score")
        .Select(c => JsonConvert.DeserializeObject<Score>(c.Data.ToString()))
        .ToList();
    
    var playerSync = syncRequest.Changes
        .Where(c => c.EntityType == "player")
        .Select(c => JsonConvert.DeserializeObject<TournamentPlayer>(c.Data.ToString()))
        .ToList();
    
    // Process each entity type in parallel
    var scoreTask = ProcessScoreSync(scoreSync);
    var playerTask = ProcessPlayerSync(playerSync);
    
    // Wait for all sync operations to complete
    await Task.WhenAll(scoreTask, playerTask);
    
    // Combine results
    var results = new List<SyncResult>();
    results.AddRange(await scoreTask);
    results.AddRange(await playerTask);
    
    return new OkObjectResult(new { 
        Results = results,
        ServerTimestamp = DateTime.UtcNow.ToString("o")
    });
}
```

### 3. Change Tracking Log

```csharp
[FunctionName("LogChangeOperation")]
public static async Task<IActionResult> LogChange(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "changes/log")] HttpRequest req,
    ILogger log)
{
    // Deserialize the change operation
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var changeRequest = JsonConvert.DeserializeObject<ChangeLogRequest>(requestBody);
    
    // Create change log entry
    var changeLog = new ChangeLog
    {
        Id = Guid.NewGuid().ToString(),
        EntityType = changeRequest.EntityType,
        EntityId = changeRequest.EntityId,
        Operation = changeRequest.Operation,
        UserId = changeRequest.UserId,
        DeviceId = changeRequest.DeviceId,
        Timestamp = DateTime.UtcNow,
        PreviousData = changeRequest.PreviousData,
        NewData = changeRequest.NewData
    };
    
    // Save to Supabase
    await _supabaseClient.From<ChangeLog>()
        .Insert(changeLog)
        .Execute();
    
    return new OkObjectResult(changeLog);
}
```

## Tournament Management

These functions handle complex tournament operations beyond basic CRUD operations.

### 1. Tournament Creator

```csharp
[FunctionName("CreateTournament")]
public static async Task<IActionResult> CreateTournament(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournaments")] HttpRequest req,
    ILogger log)
{
    // Deserialize the tournament request
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var tournamentRequest = JsonConvert.DeserializeObject<TournamentCreateRequest>(requestBody);
    
    // Generate a new tournament
    var tournament = new Tournament
    {
        Id = Guid.NewGuid().ToString(),
        Name = tournamentRequest.Name,
        Description = tournamentRequest.Description,
        CourseId = tournamentRequest.CourseId,
        StartDate = tournamentRequest.StartDate,
        EndDate = tournamentRequest.EndDate,
        Format = tournamentRequest.Format,
        Status = "draft",
        IsPublic = tournamentRequest.IsPublic,
        MaxPlayers = tournamentRequest.MaxPlayers,
        TenantId = tournamentRequest.TenantId,
        CreatedBy = tournamentRequest.UserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    
    // Save to Supabase
    await _supabaseClient.From<Tournament>()
        .Insert(tournament)
        .Execute();
    
    // If flights are provided, create them
    if (tournamentRequest.Flights != null && tournamentRequest.Flights.Any())
    {
        var flights = tournamentRequest.Flights.Select(f => new Flight
        {
            Id = Guid.NewGuid().ToString(),
            TournamentId = tournament.Id,
            Name = f.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();
        
        await _supabaseClient.From<Flight>()
            .Insert(flights)
            .Execute();
    }
    
    return new OkObjectResult(tournament);
}
```

### 2. Player Flight Assignment

```csharp
[FunctionName("AssignPlayersToFlights")]
public static async Task<IActionResult> AssignPlayersToFlights(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournaments/{tournamentId}/assign-flights")] HttpRequest req,
    string tournamentId,
    ILogger log)
{
    // Deserialize the flight assignment request
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var assignmentRequest = JsonConvert.DeserializeObject<FlightAssignmentRequest>(requestBody);
    
    // Get the tournament
    var tournament = await _supabaseClient.From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Single();
    
    if (tournament == null)
    {
        return new NotFoundResult();
    }
    
    // Process each assignment
    foreach (var assignment in assignmentRequest.Assignments)
    {
        // Update player's flight
        await _supabaseClient.From<TournamentPlayer>()
            .Update(new { FlightId = assignment.FlightId })
            .Where(p => p.Id == assignment.PlayerId)
            .Execute();
    }
    
    // Return updated players
    var updatedPlayers = await _supabaseClient.From<TournamentPlayer>()
        .Where(p => p.TournamentId == tournamentId)
        .Include(p => p.Flight)
        .Get();
    
    return new OkObjectResult(updatedPlayers);
}
```

### 3. Tournament Status Manager

```csharp
[FunctionName("UpdateTournamentStatus")]
public static async Task<IActionResult> UpdateTournamentStatus(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournaments/{tournamentId}/status")] HttpRequest req,
    string tournamentId,
    ILogger log)
{
    // Deserialize the status update request
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var statusRequest = JsonConvert.DeserializeObject<TournamentStatusRequest>(requestBody);
    
    // Get the tournament
    var tournament = await _supabaseClient.From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Single();
    
    if (tournament == null)
    {
        return new NotFoundResult();
    }
    
    // Validate the status transition
    if (!IsValidStatusTransition(tournament.Status, statusRequest.Status))
    {
        return new BadRequestObjectResult(
            $"Invalid status transition from {tournament.Status} to {statusRequest.Status}"
        );
    }
    
    // Update the tournament status
    await _supabaseClient.From<Tournament>()
        .Update(new { 
            Status = statusRequest.Status,
            UpdatedAt = DateTime.UtcNow
        })
        .Where(t => t.Id == tournamentId)
        .Execute();
    
    // If transitioning to active, initialize any necessary tournament data
    if (statusRequest.Status == "active" && tournament.Status != "active")
    {
        await InitializeTournamentData(tournamentId);
    }
    
    // If transitioning to completed, finalize any tournament calculations
    if (statusRequest.Status == "completed" && tournament.Status != "completed")
    {
        await FinalizeTournamentResults(tournamentId);
    }
    
    // Return the updated tournament
    var updatedTournament = await _supabaseClient.From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Single();
    
    return new OkObjectResult(updatedTournament);
}
```

## Notifications and External Integrations

These functions handle integrations with external systems and notification delivery.

### 1. Email Notification Service

```csharp
[FunctionName("SendTournamentNotification")]
public static async Task<IActionResult> SendNotification(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournaments/{tournamentId}/notifications")] HttpRequest req,
    string tournamentId,
    ILogger log)
{
    // Deserialize the notification request
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var notificationRequest = JsonConvert.DeserializeObject<NotificationRequest>(requestBody);
    
    // Get the tournament details
    var tournament = await _supabaseClient.From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Single();
    
    if (tournament == null)
    {
        return new NotFoundResult();
    }
    
    // Get the recipients (tournament players)
    var players = await _supabaseClient.From<TournamentPlayer>()
        .Where(p => p.TournamentId == tournamentId)
        .Get();
    
    var emailsSent = 0;
    var emailAddresses = new List<string>();
    
    // Collect email addresses based on recipient type
    switch (notificationRequest.RecipientType)
    {
        case "all_players":
            emailAddresses = players
                .Where(p => !string.IsNullOrEmpty(p.Email))
                .Select(p => p.Email)
                .ToList();
            break;
        case "specific_flight":
            emailAddresses = players
                .Where(p => p.FlightId == notificationRequest.FlightId && !string.IsNullOrEmpty(p.Email))
                .Select(p => p.Email)
                .ToList();
            break;
        // Additional recipient types...
    }
    
    // Send the emails
    foreach (var email in emailAddresses)
    {
        try
        {
            await _emailService.SendEmailAsync(
                email,
                notificationRequest.Subject,
                notificationRequest.Message,
                tournament.Name
            );
            emailsSent++;
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to send email to {email}: {ex.Message}");
        }
    }
    
    return new OkObjectResult(new { 
        TotalRecipients = emailAddresses.Count,
        EmailsSent = emailsSent,
        Status = "completed"
    });
}
```

### 2. Push Notification Handler

```csharp
[FunctionName("SendPushNotification")]
public static async Task<IActionResult> SendPushNotification(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "notifications/push")] HttpRequest req,
    ILogger log)
{
    // Deserialize the push notification request
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var pushRequest = JsonConvert.DeserializeObject<PushNotificationRequest>(requestBody);
    
    // Validate the request
    if (string.IsNullOrEmpty(pushRequest.Title) || string.IsNullOrEmpty(pushRequest.Message))
    {
        return new BadRequestObjectResult("Title and message are required");
    }
    
    // Get the targeted users' device tokens
    var deviceTokens = new List<string>();
    
    if (pushRequest.UserIds != null && pushRequest.UserIds.Any())
    {
        // Get device tokens for specific users
        var devices = await _supabaseClient.From<UserDevice>()
            .Where(d => pushRequest.UserIds.Contains(d.UserId))
            .Get();
        
        deviceTokens = devices.Select(d => d.DeviceToken).ToList();
    }
    else if (!string.IsNullOrEmpty(pushRequest.TournamentId))
    {
        // Get device tokens for all players in a tournament
        var players = await _supabaseClient.From<TournamentPlayer>()
            .Where(p => p.TournamentId == pushRequest.TournamentId)
            .Get();
        
        var userIds = players.Select(p => p.PlayerId).ToList();
        
        var devices = await _supabaseClient.From<UserDevice>()
            .Where(d => userIds.Contains(d.UserId))
            .Get();
        
        deviceTokens = devices.Select(d => d.DeviceToken).ToList();
    }
    
    if (!deviceTokens.Any())
    {
        return new OkObjectResult(new { 
            Status = "completed",
            DevicesTargeted = 0,
            NotificationsSent = 0
        });
    }
    
    // Send the push notifications
    var successCount = await _pushService.SendNotificationsAsync(
        deviceTokens,
        pushRequest.Title,
        pushRequest.Message,
        pushRequest.Data
    );
    
    return new OkObjectResult(new { 
        Status = "completed",
        DevicesTargeted = deviceTokens.Count,
        NotificationsSent = successCount
    });
}
```

### 3. Webhook Processor

```csharp
[FunctionName("ProcessWebhook")]
public static async Task<IActionResult> ProcessWebhook(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhooks/{hookType}")] HttpRequest req,
    string hookType,
    ILogger log)
{
    // Read the webhook payload
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    
    // Process based on hook type
    switch (hookType.ToLower())
    {
        case "supabase":
            return await ProcessSupabaseWebhook(requestBody, log);
        
        case "payment":
            return await ProcessPaymentWebhook(requestBody, log);
        
        case "external_scoring":
            return await ProcessExternalScoringWebhook(requestBody, log);
        
        default:
            return new BadRequestObjectResult($"Unsupported webhook type: {hookType}");
    }
}
```

## Batch Processing and Reports

These functions handle scheduled tasks and report generation.

### 1. Tournament Results Exporter

```csharp
[FunctionName("ExportTournamentResults")]
public static async Task<IActionResult> ExportResults(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tournaments/{tournamentId}/export")] HttpRequest req,
    string tournamentId,
    ILogger log)
{
    string format = req.Query["format"]; // pdf, csv, excel
    
    // Get tournament data
    var tournament = await _supabaseClient.From<Tournament>()
        .Where(t => t.Id == tournamentId)
        .Single();
    
    if (tournament == null)
    {
        return new NotFoundResult();
    }
    
    // Get players and scores
    var players = await _supabaseClient.From<TournamentPlayer>()
        .Where(p => p.TournamentId == tournamentId)
        .Get();
    
    var scores = await _supabaseClient.From<Score>()
        .Where(s => s.TournamentId == tournamentId)
        .Get();
    
    // Calculate leaderboard
    var leaderboard = CalculateLeaderboard(tournament, players, scores, tournament.Format, null);
    
    // Generate the report file based on format
    byte[] fileBytes;
    string contentType;
    string fileName;
    
    switch (format.ToLower())
    {
        case "pdf":
            fileBytes = GeneratePdfReport(tournament, leaderboard);
            contentType = "application/pdf";
            fileName = $"{tournament.Name}_Results.pdf";
            break;
        
        case "csv":
            fileBytes = GenerateCsvReport(tournament, leaderboard);
            contentType = "text/csv";
            fileName = $"{tournament.Name}_Results.csv";
            break;
        
        case "excel":
            fileBytes = GenerateExcelReport(tournament, leaderboard);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            fileName = $"{tournament.Name}_Results.xlsx";
            break;
        
        default:
            return new BadRequestObjectResult($"Unsupported format: {format}");
    }
    
    // Return the file
    return new FileContentResult(fileBytes, contentType)
    {
        FileDownloadName = fileName
    };
}
```

### 2. Daily Statistics Processor

```csharp
[FunctionName("ProcessDailyStatistics")]
public static async Task ProcessDailyStatistics(
    [TimerTrigger("0 0 0 * * *")] TimerInfo timer,
    ILogger log)
{
    log.LogInformation($"Daily statistics processing started at: {DateTime.Now}");
    
    // Get active tournaments
    var activeTournaments = await _supabaseClient.From<Tournament>()
        .Where(t => t.Status == "active")
        .Get();
    
    foreach (var tournament in activeTournaments)
    {
        try
        {
            // Calculate tournament statistics
            var players = await _supabaseClient.From<TournamentPlayer>()
                .Where(p => p.TournamentId == tournament.Id)
                .Get();
            
            var scores = await _supabaseClient.From<Score>()
                .Where(s => s.TournamentId == tournament.Id)
                .Get();
            
            // Generate daily statistics
            var stats = new TournamentDailyStatistics
            {
                Id = Guid.NewGuid().ToString(),
                TournamentId = tournament.Id,
                Date = DateTime.UtcNow.Date,
                PlayerCount = players.Count,
                CompletedScorecardsCount = scores.GroupBy(s => s.PlayerId).Count(),
                AverageScore = scores.Average(s => s.Strokes),
                AveragePutts = scores.Average(s => s.Putts ?? 0),
                BirdieCount = scores.Count(s => (s.Strokes - s.Par) == -1),
                EagleCount = scores.Count(s => (s.Strokes - s.Par) <= -2),
                ParCount = scores.Count(s => s.Strokes == s.Par),
                BogeysAndWorseCount = scores.Count(s => (s.Strokes - s.Par) >= 1)
            };
            
            // Save statistics to database
            await _supabaseClient.From<TournamentDailyStatistics>()
                .Insert(stats)
                .Execute();
            
            log.LogInformation($"Processed statistics for tournament {tournament.Id} - {tournament.Name}");
        }
        catch (Exception ex)
        {
            log.LogError($"Error processing statistics for tournament {tournament.Id}: {ex.Message}");
        }
    }
    
    log.LogInformation($"Daily statistics processing completed at: {DateTime.Now}");
}
```

### 3. Scheduled Score Verification

```csharp
[FunctionName("VerifyTournamentScores")]
public static async Task VerifyTournamentScores(
    [TimerTrigger("0 */30 * * * *")] TimerInfo timer, // Run every 30 minutes
    ILogger log)
{
    log.LogInformation($"Score verification process started at: {DateTime.Now}");
    
    // Get active tournaments
    var activeTournaments = await _supabaseClient.From<Tournament>()
        .Where(t => t.Status == "active")
        .Get();
    
    foreach (var tournament in activeTournaments)
    {
        try
        {
            // Get unverified scores that are old enough to need verification
            var cutoffTime = DateTime.UtcNow.AddHours(-1); // Scores older than 1 hour
            
            var unverifiedScores = await _supabaseClient.From<Score>()
                .Where(s => 
                    s.TournamentId == tournament.Id && 
                    s.Status == "submitted" && 
                    DateTime.Parse(s.CreatedAt) < cutoffTime)
                .Get();
            
            if (unverifiedScores.Any())
            {
                log.LogInformation($"Found {unverifiedScores.Count} unverified scores for tournament {tournament.Id}");
                
                // Check for scoring anomalies
                var anomalies = DetectScoringAnomalies(unverifiedScores);
                
                // Auto-verify non-anomalous scores
                var scoresToVerify = unverifiedScores
                    .Where(s => !anomalies.Contains(s.Id))
                    .ToList();
                
                if (scoresToVerify.Any())
                {
                    // Update scores to verified status
                    foreach (var score in scoresToVerify)
                    {
                        await _supabaseClient.From<Score>()
                            .Update(new { Status = "confirmed" })
                            .Where(s => s.Id == score.Id)
                            .Execute();
                    }
                    
                    log.LogInformation($"Auto-verified {scoresToVerify.Count} scores for tournament {tournament.Id}");
                }
                
                // Flag anomalous scores for review
                if (anomalies.Any())
                {
                    log.LogWarning($"Found {anomalies.Count} anomalous scores for tournament {tournament.Id}");
                    
                    // Update anomalous scores with a flag
                    foreach (var anomalyId in anomalies)
                    {
                        await _supabaseClient.From<Score>()
                            .Update(new { 
                                NeedsReview = true,
                                ReviewReason = "Scoring anomaly detected"
                            })
                            .Where(s => s.Id == anomalyId)
                            .Execute();
                    }
                    
                    // Notify tournament administrators
                    await NotifyScoreAnomalies(tournament.Id, anomalies);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError($"Error verifying scores for tournament {tournament.Id}: {ex.Message}");
        }
    }
    
    log.LogInformation($"Score verification process completed at: {DateTime.Now}");
}
```

## Implementation Recommendations

1. **Function Organization**:
   - Group related functions in the same Azure Function App
   - Use shared services for common operations
   - Implement dependency injection for testability

2. **Performance Optimization**:
   - Use connection pooling for database connections
   - Implement caching for frequently accessed data
   - Use batch operations for bulk processing

3. **Security**:
   - Validate JWT tokens from Supabase
   - Implement proper authorization checks
   - Use secure function-level authorization

4. **Monitoring**:
   - Set up comprehensive logging
   - Configure alerts for errors
   - Track key performance metrics

5. **Deployment Strategy**:
   - Use Infrastructure as Code (Bicep or ARM templates)
   - Implement CI/CD pipelines
   - Stage deployments with automated testing

## Azure Functions Configuration

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      }
    }
  },
  "extensions": {
    "http": {
      "routePrefix": "api/v1",
      "maxOutstandingRequests": 200,
      "maxConcurrentRequests": 100
    }
  },
  "concurrency": {
    "dynamicConcurrencyEnabled": true,
    "snapshotPersistenceEnabled": true
  },
  "functionTimeout": "00:10:00"
}
```

## Next Steps

1. Implement the core functions for the MVP:
   - Leaderboard generation
   - Score synchronization
   - Tournament management

2. Set up automated testing:
   - Unit tests for business logic
   - Integration tests for API endpoints
   - Performance tests for high-load scenarios

3. Deploy to Azure:
   - Set up staging and production environments
   - Configure monitoring and alerting
   - Implement CI/CD pipelines
