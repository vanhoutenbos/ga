using System.Net;
using System.Threading.Tasks;
using GolfApp.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Functions
{
    public class PlayerStatisticsFunctions
    {
        private readonly IPlayerStatisticsService _playerStatisticsService;
        private readonly ILogger<PlayerStatisticsFunctions> _logger;

        public PlayerStatisticsFunctions(
            IPlayerStatisticsService playerStatisticsService,
            ILogger<PlayerStatisticsFunctions> logger)
        {
            _playerStatisticsService = playerStatisticsService;
            _logger = logger;
        }

        [Function("CalculatePlayerStatistics")]
        public async Task<HttpResponseData> CalculateStats(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "players/{playerId}/tournaments/{tournamentId}/statistics")] HttpRequestData req,
            string playerId,
            string tournamentId,
            FunctionContext functionContext)
        {
            _logger.LogInformation("Processing statistics calculation request for player {PlayerId} in tournament {TournamentId}", playerId, tournamentId);
            
            try
            {
                var statistics = await _playerStatisticsService.CalculatePlayerStatisticsAsync(tournamentId, playerId);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(statistics);
                return response;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error calculating statistics for player {PlayerId} in tournament {TournamentId}", playerId, tournamentId);
                
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error calculating player statistics: {ex.Message}");
                return response;
            }
        }
    }
}
