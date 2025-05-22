using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GolfApp.Api.Services
{
    public interface ISupabaseService
    {
        Task<IEnumerable<T>> GetAsync<T>(string table, string filter = null);
        Task<T> GetSingleAsync<T>(string table, string filter);
        Task<T> InsertAsync<T>(string table, T data);
        Task<IEnumerable<T>> InsertAsync<T>(string table, IEnumerable<T> data);
        Task UpdateAsync<T>(string table, T data, string filter);
        Task DeleteAsync<T>(string table, string filter);
        Task<IEnumerable<T>> ExecuteRpcAsync<T>(string function, object parameters);
    }
}
