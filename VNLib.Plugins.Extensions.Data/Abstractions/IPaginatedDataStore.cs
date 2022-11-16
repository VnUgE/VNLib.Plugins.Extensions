using System.Collections.Generic;
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Data.Abstractions
{
    /// <summary>
    /// Defines a Data-Store that can retirieve and manipulate paginated 
    /// data
    /// </summary>
    /// <typeparam name="T">The data-model type</typeparam>
    public interface IPaginatedDataStore<T>
    {
        /// <summary>
        /// Gets a collection of records using a pagination style query, and adds the records to the collecion
        /// </summary>
        /// <param name="collection">The collection to add records to</param>
        /// <param name="page">Pagination page to get records from</param>
        /// <param name="limit">The maximum number of items to retrieve from the store</param>
        /// <returns>A task that resolves the number of items added to the collection</returns>
        Task<int> GetPageAsync(ICollection<T> collection, int page, int limit);
        /// <summary>
        /// Gets a collection of records using a pagination style query with constraint arguments, and adds the records to the collecion
        /// </summary>
        /// <param name="collection">The collection to add records to</param>
        /// <param name="page">Pagination page to get records from</param>
        /// <param name="limit">The maximum number of items to retrieve from the store</param>
        /// <param name="constraints">A params array of strings to constrain the result set from the db</param>
        /// <returns>A task that resolves the number of items added to the collection</returns>
        Task<int> GetPageAsync(ICollection<T> collection, int page, int limit, params string[] constraints);
    }
    
}
