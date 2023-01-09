/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: EnumerableTable.cs 
*
* EnumerableTable.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Data;
using System.Threading;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VNLib.Plugins.Extensions.Data.SQL
{
    /// <summary>
    /// A base class for client side async enumerable SQL queries
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public abstract class EnumerableTable<T> : TableManager, IAsyncEnumerable<T>
    {
        const string DEFAULT_ENUM_STATMENT = "SELECT *\r\nFROM @table\r\n;";

        public EnumerableTable(Func<DbConnection> factory, string tableName) : base(factory, tableName)
        {
            //Build the default select all statment
            Enumerate = DEFAULT_ENUM_STATMENT.Replace("@table", tableName);
        }
        public EnumerableTable(Func<DbConnection> factory) : base(factory)
        { }

        /// <summary>
        /// The command that will be run against the database to return rows for enumeration
        /// </summary>
        protected string Enumerate { get; set; }

        /// <summary>
        /// The isolation level to use when creating the transaction during enumerations
        /// </summary>
        protected IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.ReadUncommitted;

        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Transforms a row from the <paramref name="reader"/> into the item type
        /// to be returned when yielded.
        /// </summary>
        /// <param name="reader">The reader to get the item data from</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A task that returns the transformed item</returns>
        /// <remarks>The <paramref name="reader"/> position is set before this method is invoked</remarks>
        protected abstract Task<T> GetItemAsync(DbDataReader reader, CancellationToken cancellationToken);
        /// <summary>
        /// Invoked when an item is no longer in the enumerator scope, in the enumeration process.
        /// </summary>
        /// <param name="item">The item to cleanup</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A ValueTask that represents the cleanup process</returns>
        protected abstract ValueTask CleanupItemAsync(T item, CancellationToken cancellationToken);

        /// <summary>
        /// Gets an <see cref="IAsyncEnumerator{T}"/> to enumerate items within the backing store.
        /// </summary>
        /// <param name="closeItems">Cleanup items after each item is enumerated and the enumeration scope has 
        /// returned to the enumerator</param>
        /// <param name="cancellationToken">A token to cancel the enumeration</param>
        /// <returns>A <see cref="IAsyncEnumerator{T}"/> to enumerate records within the store</returns>
        public virtual async IAsyncEnumerator<T> GetAsyncEnumerator(bool closeItems = true, CancellationToken cancellationToken = default)
        {
            await using DbConnection db = GetConnection();
            await db.OpenAsync(cancellationToken);
            await using DbTransaction transaction = await db.BeginTransactionAsync(cancellationToken);
            //Start the enumeration command
            await using DbCommand cmd = db.CreateTextCommand(Enumerate, transaction);
            await cmd.PrepareAsync(cancellationToken);
            await using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
            //loop through results and transform each element
            while (reader.Read())
            {
                //get the item 
                T item = await GetItemAsync(reader, cancellationToken);
                try
                {
                    yield return item;
                }
                finally
                {
                    if (closeItems)
                    {
                        //Cleanup the item
                        await CleanupItemAsync(item, cancellationToken);
                    }
                }
            }
        }
    }
}