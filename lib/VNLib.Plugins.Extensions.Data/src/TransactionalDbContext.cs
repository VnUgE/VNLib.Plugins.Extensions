/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: TransactionalDbContext.cs 
*
* TransactionalDbContext.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace VNLib.Plugins.Extensions.Data
{
    /// <summary>
    /// Abstract implementation of <see cref="ITransactionalDbContext"/> that provides a transactional context for database operations
    /// </summary>
    public abstract class TransactionalDbContext : DbContext, IAsyncDisposable, ITransactionalDbContext
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected TransactionalDbContext()
        { }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected TransactionalDbContext(DbContextOptions options) : base(options)
        { }

        ///<inheritdoc/>
        public IDbContextTransaction? Transaction { get; set; }


#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize, ignore because base.Dispose() is called
        ///<inheritdoc/>
        public sealed override void Dispose()
        {
            //dispose the transaction
            Transaction?.Dispose();
            base.Dispose();
        }

        ///<inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            //If transaction has been created, dispose the transaction
            if (Transaction != null)
            {
                await Transaction.DisposeAsync();
            }
            await base.DisposeAsync();
        }
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

        ///<inheritdoc/>
        public async Task OpenTransactionAsync(CancellationToken cancellationToken = default)
        {
            //open a new transaction on the current database
            this.Transaction = await base.Database.BeginTransactionAsync(cancellationToken);
        }

        ///<inheritdoc/>
        public Task CommitTransactionAsync(CancellationToken token = default)
        {
            return Transaction != null ? Transaction.CommitAsync(token) : Task.CompletedTask;
        }

        ///<inheritdoc/>
        public Task RollbackTransctionAsync(CancellationToken token = default)
        {
            return Transaction != null ? Transaction.RollbackAsync(token) : Task.CompletedTask;
        }

    }
}