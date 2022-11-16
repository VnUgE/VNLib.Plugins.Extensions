using System.Collections.Generic;
using System.Threading.Tasks;

using VNLib.Utils;

namespace VNLib.Plugins.Extensions.Data.Abstractions
{
    /// <summary>
    /// An abstraction that defines a Data-Store that supports 
    /// bulk data operations
    /// </summary>
    /// <typeparam name="T">The data-model type</typeparam>
    public interface IBulkDataStore<T>
    {
        /// <summary>
        /// Deletes a collection of records from the store
        /// </summary>
        /// <param name="records">A collection of records to delete</param>
        ///<returns>A task the resolves the number of entires removed from the store</returns>
        Task<ERRNO> DeleteBulkAsync(ICollection<T> records);
        /// <summary>
        /// Updates a collection of records
        /// </summary>
        /// <param name="records">The collection of records to update</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        Task<ERRNO> UpdateBulkAsync(ICollection<T> records);
        /// <summary>
        /// Creates a bulk collection of records as entries in the store
        /// </summary>
        /// <param name="records">The collection of records to add</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        Task<ERRNO> CreateBulkAsync(ICollection<T> records);
        /// <summary>
        /// Creates or updates individual records from a bulk collection of records
        /// </summary>
        /// <param name="records">The collection of records to add</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        Task<ERRNO> AddOrUpdateBulkAsync(ICollection<T> records);
    }
    
}
