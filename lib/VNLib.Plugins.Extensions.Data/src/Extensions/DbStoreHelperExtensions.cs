/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DbStoreHelperExtensions.cs 
*
* DbStoreHelperExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Transactions;
using System.Threading.Tasks;

using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data.Extensions
{
    internal static class DbStoreHelperExtensions
    {
        /// <summary>
        /// If the current context instance inherits the <see cref="IConcurrentDbContext"/> interface,
        /// attempts to open a transaction with the specified isolation level.
        /// </summary>
        /// <param name="tdb"></param>
        /// <param name="isolationLevel">The transaction isolation level</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns></returns>
        internal static Task OpenTransactionAsync(this ITransactionalDbContext tdb, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            if (tdb is IConcurrentDbContext ccdb)
            {
                return ccdb.OpenTransactionAsync(isolationLevel, cancellationToken);
            }
            else
            {
                //Just ignore the isolation level
                return tdb.OpenTransactionAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Opens a new database connection. If the context supports transactions, it will
        /// open a transaction with the specified isolation level.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="level">The transaction isolation level</param>
        /// <param name="cancellation">A token to cancel the transaction operation</param>
        /// <returns>A task that resolves the new open <see cref="IDbContextHandle"/> </returns>
        public static async Task<IDbContextHandle> OpenAsync<T>(this IDataStore<T> store, IsolationLevel level, CancellationToken cancellation = default)
            where T : class, IDbModel
        {
            //Open new db context
            IDbContextHandle ctx = store.GetNewContext();

            //Support transactions and start them if the context supports it
            if(ctx is ITransactionalDbContext tdb)
            {
                try
                {
                    //Open transaction
                    await tdb.OpenTransactionAsync(level, cancellation);
                }
                catch
                {
                    await ctx.DisposeAsync();
                    throw;
                }
            }

            return ctx;
        }
    }
}
