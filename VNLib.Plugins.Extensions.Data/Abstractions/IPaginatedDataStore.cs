/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: IPaginatedDataStore.cs 
*
* IPaginatedDataStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
