using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GolfApp.Api.Models;
using GolfApp.Api.Models.DTOs;
using GolfApp.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Services
{
    public interface IPlayerStatisticsService
    {
        Task<PlayerStatisticsDto> CalculatePlayerStatisticsAsync(string tournamentId, string playerId);
    }

    public class PlayerStatisticsService : IPlayerStatisticsService
    {
        private readonly IScoreRepository _scoreRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly ITournamentRepository _tournamentRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PlayerStatisticsService> _logger;

        public PlayerStatisticsService(
            IScoreRepository scoreRepository,
            IPlayerRepository playerRepository,
            ITournamentRepository tournamentRepository,
            ICacheService cacheService,
            ILogger<PlayerStatisticsService> logger)
        {
            _scoreRepository = scoreRepository;
            _playerRepository = playerRepository;
            _tournamentRepository = tournamentRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<PlayerStatisticsDto> CalculatePlayerStatisticsAsync(string tournamentId, string playerId)
        {
            var cacheKey = $"player_statistics_tournament_{tournamentId}_player_{playerId}";
            
            return await _cacheService.GetOrCreateAsync<PlayerStatisticsDto>(cacheKey, async () =>
            {
                _logger.LogInformation("Calculating statistics for player {PlayerId} in tournament {TournamentId}", playerId, tournamentId);
                
                // Get the player's scores
                var scores = await _scoreRepository.GetByTournamentAndPlayerAsync(tournamentId, playerId);
                
                if (!scores.Any())
                {
                    _logger.LogWarning("No scores found for player {PlayerId} in tournament {TournamentId}", playerId, tournamentId);
                    return new PlayerStatisticsDto 
                    { 
                        TournamentId = tournamentId,
                        PlayerId = playerId,
                        HolesPlayed = 0
                    };
                }
                
                // Get the player details
                var player = await _playerRepository.GetByTournamentAndPlayerAsync(tournamentId, playerId);
                
                if (player == null)
                {
                    _logger.LogWarning("Player {PlayerId} not found in tournament {TournamentId}", playerId, tournamentId);
                    return new PlayerStatisticsDto 
                    { 
                        TournamentId = tournamentId,
                        PlayerId = playerId,
                        HolesPlayed = scores.Count()
                    };
                }
                
                // Get the tournament
                var tournament = await _tournamentRepository.GetTournamentWithDetailsAsync(tournamentId);
                
                // Calculate statistics
                var statistics = new PlayerStatisticsDto
                {
                    TournamentId = tournamentId,
                    PlayerId = playerId,
                    PlayerName = player.PlayerName,
                    Handicap = player.Handicap,
                    HolesPlayed = scores.Count(),
                    RoundsPlayed = scores.Select(s => s.Round).Distinct().Count(),
                    AverageScore = scores.Any() ? scores.Average(s => s.Strokes) : 0,
                    TotalScore = scores.Sum(s => s.Strokes),
                    GreenInRegulationPercentage = CalculateGIR(scores),
                    FairwaysHitPercentage = CalculateFairwaysHit(scores),
                    AveragePuttsPerHole = CalculateAveragePutts(scores),
                    ParSavingPercentage = CalculateParSaves(scores),
                    ScoringByHolePar = CalculateScoringByPar(scores),
                    SandSaves = CalculateSandSaves(scores),
                    Eagles = CountScoreType(scores, -2),
                    Birdies = CountScoreType(scores, -1),
                    Pars = CountScoreType(scores, 0),
                    Bogeys = CountScoreType(scores, 1),
                    DoubleBogeys = CountScoreType(scores, 2),
                    TripleBogeyPlus = CountTripleBogeyOrWorse(scores)
                };
                
                // Calculate round statistics
                statistics.RoundStatistics = CalculateRoundStatistics(scores);
                
                return statistics;
            }, TimeSpan.FromMinutes(10));
        }

        private double CalculateGIR(IEnumerable<Score> scores)
        {
            var validScores = scores.Where(s => s.GreenInRegulation.HasValue);
            
            if (!validScores.Any())
                return 0;
                
            return validScores.Count(s => s.GreenInRegulation.Value) / (double)validScores.Count() * 100;
        }

        private double CalculateFairwaysHit(IEnumerable<Score> scores)
        {
            var validScores = scores.Where(s => s.FairwayHit.HasValue);
            
            if (!validScores.Any())
                return 0;
                
            return validScores.Count(s => s.FairwayHit.Value) / (double)validScores.Count() * 100;
        }

        private double CalculateAveragePutts(IEnumerable<Score> scores)
        {
            var validScores = scores.Where(s => s.Putts.HasValue);
            
            if (!validScores.Any())
                return 0;
                
            return validScores.Average(s => s.Putts.Value);
        }

        private double CalculateParSaves(IEnumerable<Score> scores)
        {
            var validScores = scores.Where(s => s.Strokes <= s.Par && !s.GreenInRegulation.GetValueOrDefault());
            
            if (!scores.Any(s => !s.GreenInRegulation.GetValueOrDefault()))
                return 0;
                
            return validScores.Count() / (double)scores.Count(s => !s.GreenInRegulation.GetValueOrDefault()) * 100;
        }

        private Dictionary<string, double> CalculateScoringByPar(IEnumerable<Score> scores)
        {
            var result = new Dictionary<string, double>();
            
            // Group scores by par value
            var groupedByPar = scores.Where(s => s.Par.HasValue)
                                    .GroupBy(s => s.Par.Value);
            
            foreach (var group in groupedByPar)
            {
                string parKey = $"Par {group.Key}";
                double avgScore = group.Average(s => s.Strokes);
                result[parKey] = avgScore;
            }
            
            return result;
        }

        private double CalculateSandSaves(IEnumerable<Score> scores)
        {
            var sandShots = scores.Where(s => s.SandShots > 0);
            
            if (!sandShots.Any())
                return 0;
                
            return sandShots.Count(s => s.Strokes <= s.Par) / (double)sandShots.Count() * 100;
        }

        private int CountScoreType(IEnumerable<Score> scores, int relativeToPar)
        {
            return scores.Count(s => s.Par.HasValue && (s.Strokes - s.Par.Value) == relativeToPar);
        }

        private int CountTripleBogeyOrWorse(IEnumerable<Score> scores)
        {
            return scores.Count(s => s.Par.HasValue && (s.Strokes - s.Par.Value) >= 3);
        }

        private List<RoundStatisticsDto> CalculateRoundStatistics(IEnumerable<Score> scores)
        {
            var result = new List<RoundStatisticsDto>();
            
            var roundGroups = scores.GroupBy(s => s.Round);
            
            foreach (var round in roundGroups)
            {
                var roundScores = round.ToList();
                
                result.Add(new RoundStatisticsDto
                {
                    Round = round.Key,
                    TotalScore = roundScores.Sum(s => s.Strokes),
                    GreenInRegulationPercentage = CalculateGIR(roundScores),
                    FairwaysHitPercentage = CalculateFairwaysHit(roundScores),
                    AveragePuttsPerHole = CalculateAveragePutts(roundScores),
                    HolesPlayed = roundScores.Count
                });
            }
            
            return result.OrderBy(r => r.Round).ToList();
        }
    }
}
