using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GolfApp.Api.Auth;
using GolfApp.Api.Models;
using GolfApp.Api.Models.DTOs;
using GolfApp.Api.Services;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Repositories
{
    public interface IScoreRepository : IBaseRepository<Score>
    {
        Task<IEnumerable<Score>> GetByTournamentAsync(string tournamentId);
        Task<IEnumerable<Score>> GetByPlayerAsync(string playerId);
        Task<IEnumerable<Score>> GetByTournamentAndPlayerAsync(string tournamentId, string playerId);
        Task<IEnumerable<Score>> GetByTournamentAndRoundAsync(string tournamentId, int round);
        Task<ScoreSyncResult> SyncScoresAsync(ScoreSyncRequestDto syncRequest);
    }

    public class ScoreRepository : BaseRepository<Score>, IScoreRepository
    {
        public ScoreRepository(
            ISupabaseService supabaseService,
            IUserContext userContext,
            ICacheService cacheService,
            ILogger<ScoreRepository> logger)
            : base(supabaseService, userContext, cacheService, logger, "scores")
        {
        }

        public async Task<IEnumerable<Score>> GetByTournamentAsync(string tournamentId)
        {
            var cacheKey = $"{_tableName}_tournament_{tournamentId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"tournament_id=eq.{tournamentId}";
                return await _supabaseService.GetAsync<Score>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<IEnumerable<Score>> GetByPlayerAsync(string playerId)
        {
            var cacheKey = $"{_tableName}_player_{playerId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"player_id=eq.{playerId}";
                return await _supabaseService.GetAsync<Score>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<IEnumerable<Score>> GetByTournamentAndPlayerAsync(string tournamentId, string playerId)
        {
            var cacheKey = $"{_tableName}_tournament_{tournamentId}_player_{playerId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"tournament_id=eq.{tournamentId}&player_id=eq.{playerId}";
                return await _supabaseService.GetAsync<Score>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<IEnumerable<Score>> GetByTournamentAndRoundAsync(string tournamentId, int round)
        {
            var cacheKey = $"{_tableName}_tournament_{tournamentId}_round_{round}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"tournament_id=eq.{tournamentId}&round=eq.{round}";
                return await _supabaseService.GetAsync<Score>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<ScoreSyncResult> SyncScoresAsync(ScoreSyncRequestDto syncRequest)
        {
            var results = new List<ScoreSyncItemResultDto>();
            
            foreach (var score in syncRequest.Scores)
            {
                try
                {
                    // Check for conflicts by looking up the existing score
                    Score existingScore = null;
                    if (!string.IsNullOrEmpty(score.Id))
                    {
                        existingScore = await GetByIdAsync(score.Id);
                    }
                    else
                    {
                        // Try to find by natural key
                        var filter = $"tournament_id=eq.{score.TournamentId}&player_id=eq.{score.PlayerId}&hole=eq.{score.Hole}&round=eq.{score.Round}";
                        var existingScores = await _supabaseService.GetAsync<Score>(_tableName, filter);
                        existingScore = existingScores.FirstOrDefault();
                    }
                    
                    // Map DTO to entity
                    var scoreEntity = MapToEntity(score);
                    
                    if (existingScore != null)
                    {
                        // Determine which version is newer
                        var serverTimestamp = DateTime.Parse(existingScore.UpdatedAt);
                        var clientTimestamp = DateTime.Parse(score.UpdatedAt);
                        
                        if (clientTimestamp > serverTimestamp)
                        {
                            // Client version is newer, update the server
                            await UpdateAsync(scoreEntity);
                            
                            results.Add(new ScoreSyncItemResultDto
                            { 
                                Id = score.Id, 
                                Status = "updated",
                                Entity = "score"
                            });
                        }
                        else
                        {
                            // Server version is newer, return the server version
                            results.Add(new ScoreSyncItemResultDto
                            { 
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
                        var newScore = await CreateAsync(scoreEntity);
                        
                        results.Add(new ScoreSyncItemResultDto
                        { 
                            Id = newScore.Id, 
                            Status = "created",
                            Entity = "score"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing score {ScoreId}", score.Id);
                    
                    results.Add(new ScoreSyncItemResultDto
                    { 
                        Id = score.Id, 
                        Status = "error",
                        Entity = "score",
                        ErrorMessage = ex.Message
                    });
                }
            }
            
            await InvalidateScoreCacheAsync(syncRequest.Scores);
            
            return new ScoreSyncResult
            {
                Results = results,
                ServerTimestamp = DateTime.UtcNow.ToString("o")
            };
        }
        
        private Score MapToEntity(ScoreSyncItemDto dto)
        {
            return new Score
            {
                Id = dto.Id ?? Guid.NewGuid().ToString(),
                TournamentId = dto.TournamentId,
                PlayerId = dto.PlayerId,
                Round = dto.Round,
                Hole = dto.Hole,
                Strokes = dto.Strokes,
                Putts = dto.Putts,
                GreenInRegulation = dto.GreenInRegulation,
                FairwayHit = dto.FairwayHit,
                SandShots = dto.SandShots,
                PenaltyStrokes = dto.PenaltyStrokes,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                CreatedBy = dto.CreatedBy
            };
        }
        
        private async Task InvalidateScoreCacheAsync(IEnumerable<ScoreSyncItemDto> scores)
        {
            // Group by tournament and player to minimize cache invalidation
            var tournamentIds = scores.Select(s => s.TournamentId).Distinct();
            var playerIds = scores.Select(s => s.PlayerId).Distinct();
            var rounds = scores.Select(s => s.Round).Distinct();
            
            foreach (var tournamentId in tournamentIds)
            {
                await _cacheService.RemoveAsync($"{_tableName}_tournament_{tournamentId}");
                
                foreach (var round in rounds)
                {
                    await _cacheService.RemoveAsync($"{_tableName}_tournament_{tournamentId}_round_{round}");
                }
            }
            
            foreach (var playerId in playerIds)
            {
                await _cacheService.RemoveAsync($"{_tableName}_player_{playerId}");
                
                foreach (var tournamentId in tournamentIds)
                {
                    await _cacheService.RemoveAsync($"{_tableName}_tournament_{tournamentId}_player_{playerId}");
                }
            }
            
            // Also invalidate leaderboard caches
            foreach (var tournamentId in tournamentIds)
            {
                await _cacheService.RemoveAsync($"leaderboard_tournament_{tournamentId}*");
            }
        }
    }
}
