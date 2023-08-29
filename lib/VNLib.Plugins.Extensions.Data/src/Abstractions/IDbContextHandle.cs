/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: IDbContextHandle.cs 
*
* IDbContextHandle.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils;

namespace VNLib.Plugins.Extensions.Data.Abstractions
{
    /// <summary>
    /// Represents an open database connection and interfaces with the database,
    /// allows queries, and modifications of the set
    /// </summary>
    public interface IDbContextHandle : IAsyncDisposable
    {
        /// <summary>
        /// Gets a supported set of the desired entity type within the context
        /// </summary>
        /// <typeparam name="T">The entity model type</typeparam>
        /// <returns>A querriable instance to execute queries on</returns>
        IQueryable<T> Set<T>() where T : class;

        /// <summary>
        /// Adds a new entity to the set
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">The entity instance to add to the set</param>
        void Add<T>(T entity) where T : class;

        /// <summary>
        /// Adds a range of entities to the set of the given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities">The range of entitites to add to the set</param>
        void AddRange<T>(IEnumerable<T> entities) where T : class;

        /// <summary>
        /// Removes an entity of a given type from the set
        /// </summary>
        /// <typeparam name="T">The entity type to remove</typeparam>
        /// <param name="entity">The entity instance containing required information to remove</param>
        void Remove<T>(T entity) where T : class;

        /// <summary>
        /// Removes a range of entities of a given type from the set
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities">The range of entities to remove</param>
        void RemoveRange<T>(IEnumerable<T> entities) where T : class;

        /// <summary>
        /// Commits saves changes on the context and optionally commits changes to the database
        /// </summary>
        /// <param name="commit">A value that indicates whether the changes should be commited to the database</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The result of the database commit</returns>
        Task<ERRNO> SaveAndCloseAsync(bool commit, CancellationToken cancellation = default);
    }
}
