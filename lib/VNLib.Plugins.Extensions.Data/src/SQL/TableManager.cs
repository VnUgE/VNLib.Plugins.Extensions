/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: TableManager.cs 
*
* TableManager.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Data.Common;

namespace VNLib.Plugins.Extensions.Data.SQL
{
    /// <summary>
    /// A class that contains basic structures for interacting with an SQL driven database
    /// </summary>
    public abstract class TableManager
    {
        private readonly Func<DbConnection> Factory;
        protected string Insert { get; set; }
        protected string Select { get; set; }
        protected string Update { get; set; }
        protected string Delete { get; set; }

        /// <summary>
        /// The name of the table specified during initialized 
        /// </summary>
        protected string TableName { get; }

        protected TableManager(Func<DbConnection> factory, string tableName)
        {
            this.Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.TableName = !string.IsNullOrWhiteSpace(tableName) ? tableName : throw new ArgumentNullException(nameof(tableName));
        }

        protected TableManager(Func<DbConnection> factory)
        {
            this.Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.TableName = "";
        }
        /// <summary>
        /// Opens a new <see cref="DbConnection"/> by invoking the factory callback method
        /// </summary>
        /// <returns>The open connection</returns>
        protected DbConnection GetConnection()
        {
            return Factory();
        }
    }
}
