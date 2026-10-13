/*
* Copyright (c) 2026 Vaughn Nugent
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
        /// Updates an entry in the store with the specified record, scoped to the given user
        /// </summary>
        /// <param name="store">The database store to operate on.</param>
        /// <param name="record">The record to update</param>
        /// <param name="userId">The userid of the record owner</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task that resolves an error code (should evaluate to false on failure, and true on success)</returns>
        public static Task<ERRNO> UpdateUserRecordAsync<TEntity>(
            this IDataStore<TEntity> store,
            TEntity record,
            string userId,
            CancellationToken cancellation = default
        ) where TEntity : class, IDbModel, IUserEntity
        {
            record.UserId = userId;
            return DbStoreExtensions.UpdateAsync(store, record, cancellation);
        }

        /// <summary>
        /// Creates a new entry in the store representing the specified record, scoped to the given user
        /// </summary>
        /// <param name="store">The database store to operate on.</param>
        /// <param name="record">The record to add to the store</param>
        /// <param name="userId">The userid of the record owner</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task that resolves an error code (should evaluate to false on failure, and true on success)</returns>
        public static Task<ERRNO> CreateUserRecordAsync<TEntity>(
            this IDataStore<TEntity> store,
            TEntity record,
            string userId,
            CancellationToken cancellation = default
        ) where TEntity : class, IDbModel, IUserEntity
        {
            record.UserId = userId;
            return DbStoreExtensions.CreateAsync(store, record, cancellation);
        }

        /// <summary>
        /// Gets a single entity from its ID and user-id
        /// </summary>
        /// <param name="store">The database store to operate on.</param>
        /// <param name="key">The unique key that identifies the record</param>
        /// <param name="userId">The user's id that owns the resource</param>
        /// <returns>A task that resolves the entity if it exists</returns>
        public static Task<TEntity?> GetSingleUserRecordAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId)
            where TEntity : class, IDbModel, IUserEntity
        {
            return DbStoreExtensions.GetSingleAsync(store, key, userId);
        }

        /// <summary>
        /// Gets a page by its number offset constrained by its limit,
        /// for the given user id
        /// </summary>
        /// <param name="store">The database store to operate on.</param>
        /// <param name="collection">The collection to add records to</param>
        /// <param name="userId">The user to get the page for</param>
        /// <param name="page">Pagination page to get records from</param>
        /// <param name="limit">The maximum number of items to retrieve from the store</param>
        /// <returns>A task that resolves the number of items added to the collection</returns>
        public static Task<int> GetUserPageAsync<TEntity>(
            this IDataStore<TEntity> store,
            ICollection<TEntity> collection,
            string userId,
            int page,
            int limit
        )
            where TEntity : class, IDbModel, IUserEntity
        {
            return DbStoreExtensions.GetPageAsync(store, collection, page, limit, [userId], CancellationToken.None);
        }

        /// <summary>
        /// Deletes a single entry by its ID only if it belongs to the specified user
        /// </summary>
        /// <param name="store">The database store to operate on.</param>
        /// <param name="key">The unique key that identifies the record</param>
        /// <param name="userId">The user's id that owns the resource</param>
        /// <returns>A task that resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        public static Task<ERRNO> DeleteUserRecordAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId)
            where TEntity : class, IDbModel, IUserEntity
            => DbStoreExtensions.DeleteAsync(store, key, userId);

        /// <summary>
        /// Gets the record count for the specified userId
        /// </summary>
        /// <param name="store">The database store to operate on.</param>
        /// <param name="userId">The unique id of the user to query record count</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task that resolves the number of records belonging to the specified user</returns>
        public static Task<long> GetUserRecordCountAsync<TEntity>(
            this IDataStore<TEntity> store,
            string userId,
            CancellationToken cancellation = default
        )
            where TEntity : class, IDbModel, IUserEntity
        {
            return DbStoreExtensions.GetCountAsync(store, userId, cancellation);
        }

    }
}
