/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: IDataStore.cs 
*
* IDataStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Data.Abstractions
{
    /// <summary>
    /// An abstraction that defines a Data-Store and common 
    /// operations that retrieve or manipulate records of data
    /// </summary>
    /// <typeparam name="T">The data-model type</typeparam>
    public interface IDataStore<T> where T: class, IDbModel
    {
        /// <summary>
        /// Gets a unique ID for a new record being added to the store
        /// </summary>
        string GetNewRecordId();

        /// <summary>
        /// Gets a new <see cref="TransactionalDbContext"/> ready for use
        /// </summary>
        /// <returns></returns>
        IDbContextHandle GetNewContext();

        /// <summary>
        /// Represents a table of ef queryies that can be used to execute operations against a a database
        /// </summary>
        IDbQueryLookup<T> QueryTable { get; }

        /// <summary>
        /// Updates the current record (if found) to the new record before
        /// storing the updates.
        /// </summary>
        /// <param name="newRecord">The new record to capture data from</param>
        /// <param name="existing">The current record to be updated</param>
        void OnRecordUpdate(T newRecord, T existing);
    }
}
