using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GolfApp.Api.Repositories
{
    public interface IBaseRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> GetByIdAsync(string id);
        Task<T> CreateAsync(T entity);
        Task<IEnumerable<T>> CreateManyAsync(IEnumerable<T> entities);
        Task UpdateAsync(T entity);
        Task DeleteAsync(string id);
    }
}
