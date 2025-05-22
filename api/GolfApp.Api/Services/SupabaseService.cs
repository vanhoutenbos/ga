using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Supabase;

namespace GolfApp.Api.Services
{
    public class SupabaseService : ISupabaseService
    {
        private readonly Client _client;
        private readonly ILogger<SupabaseService> _logger;

        public SupabaseService(IOptions<SupabaseOptions> options, ILogger<SupabaseService> logger)
        {
            var supabaseOptions = options.Value;
            
            _client = new Client(
                supabaseOptions.Url,
                supabaseOptions.Key,
                new ClientOptions 
                { 
                    AutoConnectRealtime = false 
                });
                
            _logger = logger;
        }

        public async Task<IEnumerable<T>> GetAsync<T>(string table, string filter = null)
        {
            try
            {
                var query = _client.From<T>(table);
                
                if (!string.IsNullOrEmpty(filter))
                {
                    query = query.Filter(filter);
                }
                
                return await query.Get();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data from Supabase table {Table}", table);
                throw;
            }
        }

        public async Task<T> GetSingleAsync<T>(string table, string filter)
        {
            try
            {
                var query = _client.From<T>(table);
                
                if (!string.IsNullOrEmpty(filter))
                {
                    query = query.Filter(filter);
                }
                
                var results = await query.Get();
                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching single data from Supabase table {Table}", table);
                throw;
            }
        }

        public async Task<T> InsertAsync<T>(string table, T data)
        {
            try
            {
                var result = await _client.From<T>(table).Insert(data);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting data into Supabase table {Table}", table);
                throw;
            }
        }

        public async Task<IEnumerable<T>> InsertAsync<T>(string table, IEnumerable<T> data)
        {
            try
            {
                var result = await _client.From<T>(table).Insert(data);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch inserting data into Supabase table {Table}", table);
                throw;
            }
        }

        public async Task UpdateAsync<T>(string table, T data, string filter)
        {
            try
            {
                var query = _client.From<T>(table);
                
                if (!string.IsNullOrEmpty(filter))
                {
                    query = query.Filter(filter);
                }
                
                await query.Update(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data in Supabase table {Table}", table);
                throw;
            }
        }

        public async Task DeleteAsync<T>(string table, string filter)
        {
            try
            {
                var query = _client.From<T>(table);
                
                if (!string.IsNullOrEmpty(filter))
                {
                    query = query.Filter(filter);
                }
                
                await query.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data from Supabase table {Table}", table);
                throw;
            }
        }
        
        public async Task<IEnumerable<T>> ExecuteRpcAsync<T>(string function, object parameters)
        {
            try
            {
                var result = await _client.Rpc<T>(function, parameters);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing RPC function {Function}", function);
                throw;
            }
        }
    }

    public class SupabaseOptions
    {
        public string Url { get; set; }
        public string Key { get; set; }
        public string Secret { get; set; }
        public string JwtSecret { get; set; }
        public string JwtIssuer { get; set; }
    }
}
