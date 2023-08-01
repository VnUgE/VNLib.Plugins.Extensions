/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: IConcurrentDbContext.cs 
*
* IConcurrentDbContext.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Transactions;

namespace VNLib.Plugins.Extensions.Data
{
    /// <summary>
    /// Represents a database context that can manage concurrency via transactions
    /// </summary>
    public interface IConcurrentDbContext : ITransactionalDbContext
    {
        /// <summary>
        /// Opens a single transaction on the current context. If a transaction is already open, 
        /// it is disposed and a new transaction is begun.
        /// </summary>
        /// <param name="isolationLevel">The isolation level of the transaction</param>
        /// <param name="cancellationToken">A token to cancel the operations</param>
        Task OpenTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    }
}