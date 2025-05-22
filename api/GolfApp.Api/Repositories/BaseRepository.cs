using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using GolfApp.Api.Auth;
using GolfApp.Api.Services;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Repositories
{
    public abstract class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        protected readonly ISupabaseService _supabaseService;
        protected readonly IUserContext _userContext;
        protected readonly ICacheService _cacheService;
        protected readonly ILogger _logger;
        protected readonly string _tableName;
        protected readonly TimeSpan _defaultCacheTime = TimeSpan.FromMinutes(5);

        public BaseRepository(
            ISupabaseService supabaseService,
            IUserContext userContext,
            ICacheService cacheService,
            ILogger logger,
            string tableName)
        {
            _supabaseService = supabaseService;
            _userContext = userContext;
            _cacheService = cacheService;
            _logger = logger;
            _tableName = tableName;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            var cacheKey = $"{_tableName}_all";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Cache miss for {CacheKey}, querying database", cacheKey);
                return await _supabaseService.GetAsync<T>(_tableName);
            }, _defaultCacheTime);
        }

        public virtual async Task<T> GetByIdAsync(string id)
        {
            var cacheKey = $"{_tableName}_{id}";
            
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Cache miss for {CacheKey}, querying database", cacheKey);
                var filter = $"id=eq.{id}";
                return await _supabaseService.GetSingleAsync<T>(_tableName, filter);
            }, _defaultCacheTime);
        }

        public virtual async Task<T> CreateAsync(T entity)
        {
            SetAuditFields(entity, true);
            var result = await _supabaseService.InsertAsync(_tableName, entity);
            await InvalidateCacheAsync();
            return result;
        }

        public virtual async Task<IEnumerable<T>> CreateManyAsync(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                SetAuditFields(entity, true);
            }
            
            var results = await _supabaseService.InsertAsync(_tableName, entities);
            await InvalidateCacheAsync();
            return results;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            string id = GetIdValue(entity);
            SetAuditFields(entity, false);
            
            var filter = $"id=eq.{id}";
            await _supabaseService.UpdateAsync(_tableName, entity, filter);
            
            // Invalidate specific cache items
            await _cacheService.RemoveAsync($"{_tableName}_{id}");
            await _cacheService.RemoveAsync($"{_tableName}_all");
        }

        public virtual async Task DeleteAsync(string id)
        {
            var filter = $"id=eq.{id}";
            await _supabaseService.DeleteAsync<T>(_tableName, filter);
            
            // Invalidate cache
            await _cacheService.RemoveAsync($"{_tableName}_{id}");
            await _cacheService.RemoveAsync($"{_tableName}_all");
        }
        
        protected virtual async Task InvalidateCacheAsync()
        {
            await _cacheService.RemoveAsync($"{_tableName}_all");
        }
        
        protected virtual void SetAuditFields(T entity, bool isNew)
        {
            var userId = _userContext.GetUserId();
            var timestamp = DateTime.UtcNow.ToString("o");
            
            PropertyInfo createdByProp = typeof(T).GetProperty("CreatedBy");
            PropertyInfo createdAtProp = typeof(T).GetProperty("CreatedAt");
            PropertyInfo updatedAtProp = typeof(T).GetProperty("UpdatedAt");
            
            if (isNew)
            {
                createdByProp?.SetValue(entity, userId);
                createdAtProp?.SetValue(entity, timestamp);
            }
            
            updatedAtProp?.SetValue(entity, timestamp);
        }
        
        protected virtual string GetIdValue(T entity)
        {
            PropertyInfo idProp = typeof(T).GetProperty("Id");
            return idProp?.GetValue(entity)?.ToString();
        }
    }
}
