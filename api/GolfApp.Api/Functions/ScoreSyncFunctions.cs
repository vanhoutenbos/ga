using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using GolfApp.Api.Models.DTOs;
using GolfApp.Api.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Functions
{
    public class ScoreSyncFunctions
    {
        private readonly IScoreRepository _scoreRepository;
        private readonly ILogger<ScoreSyncFunctions> _logger;

        public ScoreSyncFunctions(
            IScoreRepository scoreRepository,
            ILogger<ScoreSyncFunctions> logger)
        {
            _scoreRepository = scoreRepository;
            _logger = logger;
        }

        [Function("SyncScores")]
        public async Task<HttpResponseData> SyncScores(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/scores")] HttpRequestData req,
            FunctionContext functionContext)
        {
            _logger.LogInformation("Processing score synchronization request");
            
            try
            {
                // Deserialize the batch of scores to sync
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var syncRequest = JsonSerializer.Deserialize<ScoreSyncRequestDto>(
                    requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (syncRequest == null || syncRequest.Scores == null || syncRequest.Scores.Count == 0)
                {
                    _logger.LogWarning("Invalid or empty sync request received");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid or empty sync request");
                    return badResponse;
                }
                
                _logger.LogInformation("Syncing {ScoreCount} scores", syncRequest.Scores.Count);
                
                var result = await _scoreRepository.SyncScoresAsync(syncRequest);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new 
                { 
                    Results = result.Results,
                    ServerTimestamp = DateTime.UtcNow.ToString("o")
                });
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing scores");
                
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error syncing scores: {ex.Message}");
                return response;
            }
        }
    }
}
