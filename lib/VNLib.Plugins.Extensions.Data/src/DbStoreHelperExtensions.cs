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

using System.Threading;
using System.Transactions;
using System.Threading.Tasks;

using VNLib.Utils;

namespace VNLib.Plugins.Extensions.Data
{
    internal static class DbStoreHelperExtensions
    {
        /// <summary>
        /// Commits saves changes on the context and commits the transaction if the result
        /// of the operation was successful
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task that resolves the result of the operation</returns>
        public static async Task<ERRNO> SaveAndCloseAsync(this TransactionalDbContext ctx, CancellationToken cancellation = default)
        {
            //Save changes
            ERRNO result = await ctx.SaveChangesAsync(cancellation);

            if (result)
            {
                //commit transaction if update was successful
                await ctx.CommitTransactionAsync(cancellation);
            }

            return result;
        }

        /// <summary>
        /// Opens a new database connection and begins a transaction with the specified isolation level
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="level">The transaction isolation level</param>
        /// <param name="cancellation">A token to cancel the transaction operation</param>
        /// <returns></returns>
        public static async Task<TransactionalDbContext> OpenAsync<T>(this DbStore<T> store, IsolationLevel level, CancellationToken cancellation = default)
            where T : class, IDbModel
        {
            //Open new db context
            TransactionalDbContext ctx = store.NewContext();
            try
            {
                //Open transaction
                await ctx.OpenTransactionAsync(level, cancellation);
                return ctx;
            }
            catch
            {
                await ctx.DisposeAsync();
                throw;
            }
        }
    }
}
