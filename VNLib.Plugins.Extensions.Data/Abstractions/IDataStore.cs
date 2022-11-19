/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: IDataStore.cs 
*
* IDataStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils;

namespace VNLib.Plugins.Extensions.Data.Abstractions
{
    /// <summary>
    /// An abstraction that defines a Data-Store and common 
    /// operations that retrieve or manipulate records of data
    /// </summary>
    /// <typeparam name="T">The data-model type</typeparam>
    public interface IDataStore<T>
    {
        /// <summary>
        /// Gets the total number of records in the current store
        /// </summary>
        /// <returns>A task that resolves the number of records in the store</returns>
        Task<long> GetCountAsync();
        /// <summary>
        /// Gets the number of records that belong to the specified constraint
        /// </summary>
        /// <param name="specifier">A specifier to constrain the reults</param>
        /// <returns>The number of records that belong to the specifier</returns>
        Task<long> GetCountAsync(string specifier);
        /// <summary>
        /// Gets a record from its key
        /// </summary>
        /// <param name="key">The key identifying the unique record</param>
        /// <returns>A promise that resolves the record identified by the specified key</returns>
        Task<T?> GetSingleAsync(string key);
        /// <summary>
        /// Gets a record from its key
        /// </summary>
        /// <param name="specifiers">A variable length specifier arguemnt array for retreiving a single application</param>
        /// <returns></returns>
        Task<T?> GetSingleAsync(params string[] specifiers);
        /// <summary>
        /// Gets a record from the store with a partial model, intended to complete the model
        /// </summary>
        /// <param name="record">The partial model used to query the store</param>
        /// <returns>A task the resolves the completed data-model</returns>
        Task<T?> GetSingleAsync(T record);     
        /// <summary>
        /// Fills a collection with enires retireved from the store using the specifer
        /// </summary>
        /// <param name="collection">The collection to add entires to</param>
        /// <param name="specifier">A specifier argument to constrain results</param>
        /// <param name="limit">The maximum number of elements to retrieve</param>
        /// <returns>A Task the resolves to the number of items added to the collection</returns>
        Task<ERRNO> GetCollectionAsync(ICollection<T> collection, string specifier, int limit);        
        /// <summary>
        /// Fills a collection with enires retireved from the store using a variable length specifier
        /// parameter
        /// </summary>
        /// <param name="collection">The collection to add entires to</param>
        /// <param name="limit">The maximum number of elements to retrieve</param>
        /// <param name="args"></param>
        /// <returns>A Task the resolves to the number of items added to the collection</returns>
        Task<ERRNO> GetCollectionAsync(ICollection<T> collection, int limit, params string[] args);
        /// <summary>
        /// Updates an entry in the store with the specified record
        /// </summary>
        /// <param name="record">The record to update</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        Task<ERRNO> UpdateAsync(T record);       
        /// <summary>
        /// Creates a new entry in the store representing the specified record
        /// </summary>
        /// <param name="record">The record to add to the store</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        Task<ERRNO> CreateAsync(T record);
        /// <summary>
        /// Deletes one or more entrires from the store matching the specified record
        /// </summary>
        /// <param name="record">The record to remove from the store</param>
        /// <returns>A task the resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        Task<ERRNO> DeleteAsync(T record);
        /// <summary>
        /// Deletes one or more entires from the store matching the specified unique key
        /// </summary>
        /// <param name="key">The unique key that identifies the record</param>
        /// <returns>A task the resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        Task<ERRNO> DeleteAsync(string key);
        /// <summary>
        /// Deletes one or more entires from the store matching the supplied specifiers
        /// </summary>
        /// <param name="specifiers">A variable length array of specifiers used to delete one or more entires</param>
        /// <returns>A task the resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        Task<ERRNO> DeleteAsync(params string[] specifiers);
        /// <summary>
        /// Updates an entry in the store if it exists, or creates a new entry if one does not already exist
        /// </summary>
        /// <param name="record">The record to add to the store</param>
        /// <returns>A task the resolves the result of the operation</returns>
        Task<ERRNO> AddOrUpdateAsync(T record);
    }
}
