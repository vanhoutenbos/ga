using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GolfApp.Api.Services
{
    public interface ICacheService
    {
        Task<T> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
    }

    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                var data = await _cache.GetStringAsync(key);
                
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }
                
                return JsonSerializer.Deserialize<T>(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cached item with key {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
                };
                
                var data = JsonSerializer.Serialize(value);
                await _cache.SetStringAsync(key, data, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached item with key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached item with key {Key}", key);
            }
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cached = await GetAsync<T>(key);
            
            if (cached != null)
            {
                return cached;
            }
            
            var value = await factory();
            
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }
            
            return value;
        }
    }
}
