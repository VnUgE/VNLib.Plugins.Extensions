/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DBContextBase.cs 
*
* DBContextBase.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using Microsoft.EntityFrameworkCore;

using VNLib.Utils;
using VNLib.Plugins.Extensions.Data.Abstractions;


namespace VNLib.Plugins.Extensions.Data
{
    /// <summary>
    /// Provides abstract implementation of a database context that can manage concurrency via transactions
    /// </summary>
    public abstract class DBContextBase : DbContext, IAsyncDisposable, IDbContextHandle
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected DBContextBase()
        { }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected DBContextBase(DbContextOptions options) : base(options)
        { }

        ///<inheritdoc/>
        IQueryable<T> IDbContextHandle.Set<T>() => base.Set<T>();

        ///<inheritdoc/>
        void IDbContextHandle.Add<T>(T entity) => base.Add(entity);

        ///<inheritdoc/>
        void IDbContextHandle.Remove<T>(T entity) => base.Remove<T>(entity);

        ///<inheritdoc/>
        public virtual void AddRange<T>(IEnumerable<T> entities) where T : class
        {
            DbSet<T> set = base.Set<T>();
            set.AddRange(entities);
        }

        ///<inheritdoc/>
        public virtual async Task<ERRNO> SaveAndCloseAsync(bool commit, CancellationToken cancellation = default)
        {
            return await base.SaveChangesAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual void RemoveRange<T>(IEnumerable<T> entities) where T : class
        {
            DbSet<T> set = base.Set<T>();
            set.RemoveRange(entities);
        }
    }
}