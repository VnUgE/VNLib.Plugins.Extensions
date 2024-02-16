/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IRuntimeDbProvider.cs 
*
* IRuntimeDbProvider.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Data.Common;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// Provides a dynamic database provider for the current plugin
    /// </summary>
    public interface IRuntimeDbProvider
    {
        /// <summary>
        /// Asynchronously gets the <see cref="DbConnection"/> factory for the current plugin
        /// </summary>
        /// <returns>A task that resolves a new DB factory function</returns>
        Task<Func<DbConnection>> GetDbConnectionAsync();

        /// <summary>
        /// Asynchronously gets the <see cref="DbContextOptions"/> instance for 
        /// the provider's database
        /// </summary>
        /// <returns>A task that resolves the <see cref="DbContextOptions"/> instance</returns>
        Task<DbContextOptions> GetDbOptionsAsync();

        /// <summary>
        /// Gets the command generator for the specific database provider
        /// </summary>
        /// <returns>A command generator instance build DB specific commands</returns>
        IDBCommandGenerator GetCommandGenerator();
    }
}
