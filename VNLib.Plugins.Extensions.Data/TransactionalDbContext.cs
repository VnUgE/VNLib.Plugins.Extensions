﻿/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: TransactionalDbContext.cs 
*
* TransactionalDbContext.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Data. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace VNLib.Plugins.Extensions.Data
{
    public abstract class TransactionalDbContext : DbContext, IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected TransactionalDbContext()
        {}
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected TransactionalDbContext(DbContextOptions options) : base(options)
        {}

        /// <summary>
        /// The transaction that was opened on the current context
        /// </summary>
        public IDbContextTransaction? Transaction { get; set; }
        ///<inheritdoc/>
        public override void Dispose()
        {
            //dispose the transaction
            this.Transaction?.Dispose();
            base.Dispose();
        }

        /// <summary>
        /// Opens a single transaction on the current context. If a transaction is already open, 
        /// it is disposed and a new transaction is begun.
        /// </summary>
        public async Task OpenTransactionAsync(CancellationToken cancellationToken = default)
        {
            //open a new transaction on the current database
            this.Transaction = await base.Database.BeginTransactionAsync(cancellationToken);
        }
        /// <summary>
        /// Invokes the <see cref="IDbContextTransaction.Commit"/> on the current context
        /// </summary>
        public Task CommitTransactionAsync(CancellationToken token = default)
        {
            return Transaction != null ? Transaction.CommitAsync(token) : Task.CompletedTask;
        }
        /// <summary>
        /// Invokes the <see cref="IDbContextTransaction.Rollback"/> on the current context
        /// </summary>
        public Task RollbackTransctionAsync(CancellationToken token = default)
        {
            return Transaction != null ? Transaction.RollbackAsync(token) : Task.CompletedTask;
        }
        ///<inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            //If transaction has been created, dispose the transaction
            if(this.Transaction != null)
            {
                await this.Transaction.DisposeAsync();
            }
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}