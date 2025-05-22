using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GolfApp.Api.Auth;
using GolfApp.Api.Models;
using GolfApp.Api.Services;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Repositories
{
    public interface ITournamentRepository : IBaseRepository<Tournament>
    {
        Task<IEnumerable<Tournament>> GetByTenantAsync(string tenantId);
        Task<IEnumerable<Tournament>> GetPublicAsync();
        Task<Tournament> GetTournamentWithDetailsAsync(string id);
        Task UpdateStatusAsync(string id, string status);
    }

    public class TournamentRepository : BaseRepository<Tournament>, ITournamentRepository
    {
        public TournamentRepository(
            ISupabaseService supabaseService,
            IUserContext userContext,
            ICacheService cacheService,
            ILogger<TournamentRepository> logger)
            : base(supabaseService, userContext, cacheService, logger, "tournaments")
        {
        }

        public async Task<IEnumerable<Tournament>> GetByTenantAsync(string tenantId)
        {
            var cacheKey = $"{_tableName}_tenant_{tenantId}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = $"tenant_id=eq.{tenantId}";
                return await _supabaseService.GetAsync<Tournament>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<IEnumerable<Tournament>> GetPublicAsync()
        {
            var cacheKey = $"{_tableName}_public";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var filter = "is_public=eq.true";
                return await _supabaseService.GetAsync<Tournament>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public async Task<Tournament> GetTournamentWithDetailsAsync(string id)
        {
            var cacheKey = $"{_tableName}_{id}_details";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                // This would typically be a join operation, but since we're using Supabase,
                // we'll need to make multiple queries
                var tournament = await GetByIdAsync(id);
                
                if (tournament == null)
                {
                    return null;
                }
                
                // Get course details
                var courseFilter = $"id=eq.{tournament.CourseId}";
                var courses = await _supabaseService.GetAsync<Course>("courses", courseFilter);
                tournament.Course = courses.FirstOrDefault();
                
                // Get flights for the tournament
                var flightFilter = $"tournament_id=eq.{id}";
                tournament.Flights = await _supabaseService.GetAsync<Flight>("flights", flightFilter);
                
                return tournament;
            }, _defaultCacheTime);
        }

        public async Task UpdateStatusAsync(string id, string status)
        {
            var filter = $"id=eq.{id}";
            var timestamp = DateTime.UtcNow.ToString("o");
            
            // Create a dynamic object with just the fields we want to update
            var updateData = new 
            { 
                Status = status,
                UpdatedAt = timestamp
            };
            
            await _supabaseService.UpdateAsync(_tableName, updateData, filter);
            
            // Invalidate cache items
            await _cacheService.RemoveAsync($"{_tableName}_{id}");
            await _cacheService.RemoveAsync($"{_tableName}_{id}_details");
            await _cacheService.RemoveAsync($"{_tableName}_all");
        }
    }
}
