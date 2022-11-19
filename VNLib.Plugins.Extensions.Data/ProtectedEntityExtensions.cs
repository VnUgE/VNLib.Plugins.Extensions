/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: ProtectedEntityExtensions.cs 
*
* ProtectedEntityExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data
{
    public static class ProtectedEntityExtensions
    {
        /// <summary>
        /// Updates the specified record within the store
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to update</param>
        /// <param name="userId">The userid of the record owner</param>
        /// <returns>A task that evaluates to the number of records modified</returns>
        public static Task<ERRNO> UpdateAsync<TEntity>(this IDataStore<TEntity> store, TEntity record, string userId) where TEntity : class, IDbModel, IUserEntity
        {
            record.UserId = userId;
            return store.UpdateAsync(record);
        }

        /// <summary>
        /// Updates the specified record within the store
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to update</param>
        /// <param name="userId">The userid of the record owner</param>
        /// <returns>A task that evaluates to the number of records modified</returns>
        public static Task<ERRNO> CreateAsync<TEntity>(this IDataStore<TEntity> store, TEntity record, string userId) where TEntity : class, IDbModel, IUserEntity
        {
            record.UserId = userId;
            return store.CreateAsync(record);
        }

        /// <summary>
        /// Gets a single entity from its ID and user-id
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key">The unique id of the entity</param>
        /// <param name="userId">The user's id that owns the resource</param>
        /// <returns>A task that resolves the entity or null if not found</returns>
        public static Task<TEntity?> GetSingleAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId) where TEntity : class, IDbModel, IUserEntity
        {
            return store.GetSingleAsync(key, userId);
        }

        /// <summary>
        /// Deletes a single entiry by its ID only if it belongs to the speicifed user
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key">The unique id of the entity</param>
        /// <param name="userId">The user's id that owns the resource</param>
        /// <returns>A task the resolves the number of eneities deleted (should evaluate to true or false)</returns>
        public static Task<ERRNO> DeleteAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId) where TEntity : class, IDbModel, IUserEntity
        {
            return store.DeleteAsync(key, userId);
        }
    }
}
