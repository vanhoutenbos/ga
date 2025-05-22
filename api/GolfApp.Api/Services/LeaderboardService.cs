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
    public interface ILeaderboardService
    {
        Task<LeaderboardResultDto> GenerateLeaderboardAsync(
            string tournamentId, 
            string format = "gross", 
            string flightId = null, 
            int? round = null);
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly ITournamentRepository _tournamentRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IScoreRepository _scoreRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<LeaderboardService> _logger;

        public LeaderboardService(
            ITournamentRepository tournamentRepository,
            IPlayerRepository playerRepository,
            IScoreRepository scoreRepository,
            ICacheService cacheService,
            ILogger<LeaderboardService> logger)
        {
            _tournamentRepository = tournamentRepository;
            _playerRepository = playerRepository;
            _scoreRepository = scoreRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<LeaderboardResultDto> GenerateLeaderboardAsync(
            string tournamentId, 
            string format = "gross", 
            string flightId = null, 
            int? round = null)
        {
            // Build cache key based on parameters
            var cacheKey = $"leaderboard_tournament_{tournamentId}_format_{format}";
            if (!string.IsNullOrEmpty(flightId))
                cacheKey += $"_flight_{flightId}";
            if (round.HasValue)
                cacheKey += $"_round_{round}";
            
            return await _cacheService.GetOrCreateAsync<LeaderboardResultDto>(cacheKey, async () =>
            {
                _logger.LogInformation("Generating leaderboard for tournament {TournamentId}", tournamentId);
                
                // Fetch tournament data
                var tournament = await _tournamentRepository.GetTournamentWithDetailsAsync(tournamentId);
                if (tournament == null)
                {
                    throw new Exception($"Tournament with ID {tournamentId} not found");
                }
                
                // Fetch tournament players
                var allPlayers = await _playerRepository.GetByTournamentAsync(tournamentId);
                
                // Apply flight filter if provided
                var players = allPlayers;
                if (!string.IsNullOrEmpty(flightId))
                {
                    players = allPlayers.Where(p => p.FlightId == flightId).ToList();
                }
                
                // Fetch all scores for the tournament
                var allScores = await _scoreRepository.GetByTournamentAsync(tournamentId);
                
                // Apply round filter if provided
                var scores = allScores;
                if (round.HasValue)
                {
                    scores = allScores.Where(s => s.Round == round.Value).ToList();
                }
                
                // Calculate leaderboard entries
                var entries = CalculateLeaderboard(tournament, players, scores, format, round);
                
                // Sort by position
                var sortedEntries = entries.OrderBy(l => l.Position).ToList();
                
                return new LeaderboardResultDto
                {
                    TournamentId = tournamentId,
                    TournamentName = tournament.Name,
                    Format = format,
                    FlightId = flightId,
                    Round = round,
                    LastUpdated = DateTime.UtcNow.ToString("o"),
                    Entries = sortedEntries
                };
            }, TimeSpan.FromMinutes(5));
        }

        private List<LeaderboardEntryDto> CalculateLeaderboard(
            Tournament tournament,
            IEnumerable<TournamentPlayer> players,
            IEnumerable<Score> scores,
            string format,
            int? round)
        {
            var entries = new List<LeaderboardEntryDto>();
            
            foreach (var player in players)
            {
                // Get this player's scores
                var playerScores = scores.Where(s => s.PlayerId == player.PlayerId).ToList();
                
                if (!playerScores.Any())
                    continue;
                
                // Calculate total score based on format
                int totalScore = 0;
                int totalPar = 0;
                int completedHoles = playerScores.Count;
                
                switch (format.ToLower())
                {
                    case "gross":
                        totalScore = playerScores.Sum(s => s.Strokes);
                        totalPar = playerScores.Sum(s => s.Par ?? 0); // Assuming par is stored in the score
                        break;
                        
                    case "net":
                        // Calculate handicap adjusted score
                        double handicapAdjustment = player.Handicap / 18.0; // Simple calculation, adjust as needed
                        totalScore = playerScores.Sum(s => (int)Math.Round(s.Strokes - handicapAdjustment));
                        totalPar = playerScores.Sum(s => s.Par ?? 0);
                        break;
                        
                    case "stableford":
                        // Calculate stableford points
                        foreach (var score in playerScores)
                        {
                            int par = score.Par ?? 4; // Default to par 4 if not specified
                            int points = CalculateStablefordPoints(score.Strokes, par, player.Handicap);
                            totalScore += points;
                        }
                        break;
                        
                    default:
                        totalScore = playerScores.Sum(s => s.Strokes);
                        totalPar = playerScores.Sum(s => s.Par ?? 0);
                        break;
                }
                
                // Group scores by round
                var roundScores = playerScores
                    .GroupBy(s => s.Round)
                    .Select(g => new RoundScoreDto 
                    { 
                        Round = g.Key, 
                        Score = g.Sum(s => s.Strokes),
                        Par = g.Sum(s => s.Par ?? 0)
                    })
                    .OrderBy(r => r.Round)
                    .ToList();
                
                // Create leaderboard entry
                entries.Add(new LeaderboardEntryDto
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    TotalScore = totalScore,
                    ToPar = format.ToLower() == "stableford" ? null : totalScore - totalPar,
                    CompletedHoles = completedHoles,
                    RoundScores = roundScores,
                    FlightId = player.FlightId,
                    FlightName = player.FlightName,
                    Position = 0 // Will be set after sorting
                });
            }
            
            // Sort entries
            if (format.ToLower() == "stableford")
            {
                // Higher is better for stableford
                entries = entries.OrderByDescending(e => e.TotalScore).ToList();
            }
            else
            {
                // Lower is better for stroke play
                entries = entries.OrderBy(e => e.TotalScore).ToList();
            }
            
            // Assign positions
            int position = 1;
            int? lastScore = null;
            foreach (var entry in entries)
            {
                if (lastScore.HasValue && lastScore == entry.TotalScore)
                {
                    // Tie - use same position
                    entry.Position = position - 1;
                }
                else
                {
                    entry.Position = position;
                }
                
                lastScore = entry.TotalScore;
                position++;
            }
            
            return entries;
        }

        private int CalculateStablefordPoints(int strokes, int par, double handicap)
        {
            // Basic stableford calculation - this can be enhanced based on exact rules
            double adjustedPar = par + (handicap / 18.0); // Simple handicap adjustment
            int pointsOverPar = (int)Math.Ceiling(strokes - adjustedPar);
            
            switch (pointsOverPar)
            {
                case -2: return 4; // Eagle
                case -1: return 3; // Birdie
                case 0: return 2;  // Par
                case 1: return 1;  // Bogey
                default: return 0; // Double bogey or worse
            }
        }
    }
}
