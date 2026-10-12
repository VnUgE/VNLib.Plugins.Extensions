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
        /// <param name="userId">The userid of the record owner</param>
        /// <inheritdoc cref="DbStoreExtensions.UpdateAsync{T}(IDataStore{T}, T, CancellationToken)"/>
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

        /// <param name="userId">The userid of the record owner</param>
        /// <inheritdoc cref="DbStoreExtensions.CreateAsync{T}(IDataStore{T}, T, CancellationToken)"/>
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
        /// <param name="userId">The user's id that owns the resource</param>
        /// <inheritdoc cref="DbStoreExtensions.GetSingleAsync{T}(IDataStore{T}, string[])"/>
        public static Task<TEntity?> GetSingleUserRecordAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId) 
            where TEntity : class, IDbModel, IUserEntity
        {
            return DbStoreExtensions.GetSingleAsync(store, key, userId);
        }

        /// <summary>
        /// Gets a page by its number offset constrained by its limit, 
        /// for the given user id
        /// </summary>
        /// <param name="userId">The user to get the page for</param>
        /// <inheritdoc cref="DbStoreExtensions.GetPageAsync{T}(IDataStore{T}, ICollection{T}, int, int, string[])"/>
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
        /// <param name="userId">The user's id that owns the resource</param>
        /// <inheritdoc cref="DbStoreExtensions.DeleteAsync{T}(IDataStore{T}, string, CancellationToken)"/>
        public static Task<ERRNO> DeleteUserRecordAsync<TEntity>(this IDataStore<TEntity> store, string key, string userId)
            where TEntity : class, IDbModel, IUserEntity 
            => DbStoreExtensions.DeleteAsync(store, key, userId);

        /// <summary>
        /// Gets the record count for the specified userId
        /// </summary>
        /// <param name="userId">The unique id of the user to query record count</param>
        /// <returns>A task that resolves the number of records belonging to the specified user</returns>
        /// <inheritdoc cref="DbStoreExtensions.GetCountAsync{T}(IDataStore{T}, string, CancellationToken)"/>
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
