/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DbStore.cs 
*
* DbStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using System.Collections.Generic;

using VNLib.Utils.Memory.Caching;
using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data
{

    /// <summary>
    /// Implements basic data-store functionality with abstract query builders
    /// </summary>
    /// <typeparam name="T">A <see cref="DbModelBase"/> implemented type</typeparam>
    public abstract class DbStore<T> : IDataStore<T> where T: class, IDbModel
    {

        /// <summary>
        /// Gets a new <see cref="TransactionalDbContext"/> ready for use
        /// </summary>
        /// <returns></returns>
        public abstract IDbContextHandle GetNewContext();

        ///<inheritdoc/>
        public abstract string GetNewRecordId();

        ///<inheritdoc/>
        public abstract void OnRecordUpdate(T newRecord, T existing);

        ///<inheritdoc/>
        public abstract IDbQueryLookup<T> QueryTable { get; }

        /// <summary>
        /// An object rental for entity collections
        /// </summary>
        public ObjectRental<List<T>> ListRental { get; } = ObjectRental.Create<List<T>>(null, static ret => ret.Clear());
        
    }
}
