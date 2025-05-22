using System.Net;
using System.Threading.Tasks;
using GolfApp.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Functions
{
    public class LeaderboardFunctions
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly ILogger<LeaderboardFunctions> _logger;

        public LeaderboardFunctions(
            ILeaderboardService leaderboardService,
            ILogger<LeaderboardFunctions> logger)
        {
            _leaderboardService = leaderboardService;
            _logger = logger;
        }

        [Function("GetTournamentLeaderboard")]
        public async Task<HttpResponseData> GetLeaderboard(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tournaments/{tournamentId}/leaderboard")] HttpRequestData req,
            string tournamentId,
            FunctionContext functionContext)
        {
            _logger.LogInformation("Processing leaderboard request for tournament {TournamentId}", tournamentId);
            
            // Get query parameters for filtering options
            var queryDictionary = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string format = queryDictionary["format"] ?? "gross"; // gross, net, stableford
            string flightId = queryDictionary["flightId"]; // Optional flight filter
            int? round = queryDictionary["round"] != null ? int.Parse(queryDictionary["round"]) : null; // Optional round filter
            
            try
            {
                var leaderboard = await _leaderboardService.GenerateLeaderboardAsync(
                    tournamentId, 
                    format, 
                    flightId, 
                    round);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(leaderboard);
                return response;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error generating leaderboard for tournament {TournamentId}", tournamentId);
                
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error generating leaderboard: {ex.Message}");
                return response;
            }
        }
    }
}
