using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GolfApp.Api.Auth;
using GolfApp.Api.Models;
using GolfApp.Api.Services;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Repositories
{
    public interface IPlayerRepository : IBaseRepository<TournamentPlayer>
    {
        Task<IEnumerable<TournamentPlayer>> GetByTournamentAsync(string tournamentId);
        Task<TournamentPlayer> GetByTournamentAndPlayerAsync(string tournamentId, string playerId);
        Task<IEnumerable<TournamentPlayer>> GetByFlightAsync(string flightId);
        Task UpdatePlayerFlightAsync(string playerId, string flightId);
    }

    public class PlayerRepository : BaseRepository<TournamentPlayer>, IPlayerRepository
    {
        public PlayerRepository(
            ISupabaseService supabaseService,
            IUserContext userContext,
            ICacheService cacheService,
            ILogger<PlayerRepository> logger)
            : base(supabaseService, userContext, cacheService, logger, "tournament_players")
        {
        }

        public async Task<IEnumerable<TournamentPlayer>> GetByTournamentAsync(string tournamentId)
        {
            var cacheKey = $"{_tableName}_tournament_{tournamentId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"tournament_id=eq.{tournamentId}";
                return await _supabaseService.GetAsync<TournamentPlayer>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<TournamentPlayer> GetByTournamentAndPlayerAsync(string tournamentId, string playerId)
        {
            var cacheKey = $"{_tableName}_tournament_{tournamentId}_player_{playerId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"tournament_id=eq.{tournamentId}&player_id=eq.{playerId}";
                var results = await _supabaseService.GetAsync<TournamentPlayer>(_tableName, filter);
                return results.FirstOrDefault();
            }, _defaultCacheTime);
        }

        public async Task<IEnumerable<TournamentPlayer>> GetByFlightAsync(string flightId)
        {
            var cacheKey = $"{_tableName}_flight_{flightId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"flight_id=eq.{flightId}";
                return await _supabaseService.GetAsync<TournamentPlayer>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task UpdatePlayerFlightAsync(string playerId, string flightId)
        {
            var filter = $"id=eq.{playerId}";
            
            // Create a dynamic object with just the fields we want to update
            var updateData = new 
            { 
                FlightId = flightId,
                UpdatedAt = System.DateTime.UtcNow.ToString("o")
            };
            
            await _supabaseService.UpdateAsync(_tableName, updateData, filter);
            
            // Invalidate cache
            await InvalidateCacheAsync();
            await _cacheService.RemoveAsync($"{_tableName}_{playerId}");
            await _cacheService.RemoveAsync($"{_tableName}_flight_{flightId}");
        }
        
        protected override async Task InvalidateCacheAsync()
        {
            await base.InvalidateCacheAsync();
            
            // Find and remove all tournament-related player caches
            // This is a simplification - in a real app, you'd need a more sophisticated cache invalidation strategy
            await _cacheService.RemoveAsync($"{_tableName}_tournament_*");
            await _cacheService.RemoveAsync($"{_tableName}_flight_*");
        }
    }
}
