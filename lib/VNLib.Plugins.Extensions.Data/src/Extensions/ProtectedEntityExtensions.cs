/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: ProtectedEntityExtensions.cs 
*
* ProtectedEntityExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IDataStore{TEntity}"/> implementations that support user-protected entities
    /// </summary>
    public static class ProtectedEntityExtensions
    {
        /// <summary>
        /// Updates the specified record within the store
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to update</param>
        /// <param name="userId">The userid of the record owner</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that evaluates to the number of records modified</returns>
        public static Task<ERRNO> UpdateUserRecordAsync<TEntity>(this IDataStore<TEntity> store, TEntity record, string userId, CancellationToken cancellation = default)
            where TEntity : class, IDbModel, IUserEntity
        {
            record.UserId = userId;
            return store.UpdateAsync(record, cancellation);
        }

        /// <summary>
        /// Updates the specified record within the store
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to update</param>
        /// <param name="userId">The userid of the record owner</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that evaluates to the number of records modified</returns>
        public static Task<ERRNO> CreateUserRecordAsync<TEntity>(this IDataStore<TEntity> store, TEntity record, string userId, CancellationToken cancellation = default)
            where TEntity : class, IDbModel, IUserEntity
        {
            record.UserId = userId;
            return store.CreateAsync(record, cancellation);
        }

        /// <summary>
        /// Gets a single entity from its ID and user-id
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key">The unique id of the entity</param>
        /// <param name="userId">The user's id that owns the resource</param>
        /// <returns>A task that resolves the entity or null if not found</returns>
        public static Task<TEntity?> GetSingleUserRecordAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId) where TEntity : class, IDbModel, IUserEntity
        {
            return store.GetSingleAsync(key, userId);
        }

        /// <summary>
        /// Gets a page by its number offset constrained by its limit, 
        /// for the given user id
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="store"></param>
        /// <param name="collection">The collection to store found records</param>
        /// <param name="userId">The user to get the page for</param>
        /// <param name="page">The page offset</param>
        /// <param name="limit">The record limit for the page</param>
        /// <returns>A task that resolves the number of entities added to the collection</returns>
        public static Task<int> GetUserPageAsync<TEntity>(this IDataStore<TEntity> store, ICollection<TEntity> collection, string userId, int page, int limit)
            where TEntity : class, IDbModel, IUserEntity
        {
            return store.GetPageAsync(collection, page, limit, userId);
        }

        /// <summary>
        /// Deletes a single entiry by its ID only if it belongs to the speicifed user
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key">The unique id of the entity</param>
        /// <param name="userId">The user's id that owns the resource</param>
        /// <returns>A task the resolves the number of eneities deleted (should evaluate to true or false)</returns>
        public static Task<ERRNO> DeleteUserRecordAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId) where TEntity : class, IDbModel, IUserEntity
        {
            return store.DeleteAsync(key, userId);
        }

        /// <summary>
        /// Gets the record count for the specified userId
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="store"></param>
        /// <param name="userId">The unique id of the user to query record count</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that resolves the number of records belonging to the specified user</returns>
        public static Task<long> GetUserRecordCountAsync<TEntity>(this IDataStore<TEntity> store, string userId, CancellationToken cancellation = default)
            where TEntity : class, IDbModel, IUserEntity
        {
            return store.GetCountAsync(userId, cancellation);
        }

    }
}
