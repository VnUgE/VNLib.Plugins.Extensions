/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.Tests
* File: SqlProviderIntegrationService.cs 
*
* SqlProviderIntegrationService.cs is part of VNLib.Plugins.Extensions.Loading.Sql.Tests which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Data.Common;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace VNLib.Plugins.Extensions.Loading.Sql.Tests.Loading
{
    /// <summary>
    /// Service that exposes SQL provider connection factory and context options for testing.
    /// </summary>
    public sealed class SqlProviderIntegrationService
    {
        private readonly IAsyncLazy<Func<DbConnection>> _connectionFactory;
        private readonly IAsyncLazy<DbContextOptions> _contextOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlProviderIntegrationService"/> class.
        /// </summary>
        /// <param name="connectionFactory">The async lazy connection factory</param>
        /// <param name="contextOptions">The async lazy context options</param>
        public SqlProviderIntegrationService(
            IAsyncLazy<Func<DbConnection>> connectionFactory,
            IAsyncLazy<DbContextOptions> contextOptions
        )
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);
            ArgumentNullException.ThrowIfNull(contextOptions);

            _connectionFactory = connectionFactory;
            _contextOptions = contextOptions;
        }

        /// <summary>
        /// Creates a new database connection asynchronously.
        /// </summary>
        /// <returns>A task that resolves to a new database connection</returns>
        public async Task<DbConnection> CreateConnectionAsync()
        {
            Func<DbConnection> factory = await _connectionFactory;
            return factory();
        }

        /// <summary>
        /// Gets the EF Core context options asynchronously.
        /// </summary>
        /// <returns>A task that resolves to the context options</returns>
        public Task<DbContextOptions> GetDbContextOptionsAsync() => _contextOptions.AsTask();
    }
}
