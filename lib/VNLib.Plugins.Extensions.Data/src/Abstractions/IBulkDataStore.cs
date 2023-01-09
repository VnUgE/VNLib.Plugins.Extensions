/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: IBulkDataStore.cs 
*
* IBulkDataStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Data. If not, see http://www.gnu.org/licenses/.
*/

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
