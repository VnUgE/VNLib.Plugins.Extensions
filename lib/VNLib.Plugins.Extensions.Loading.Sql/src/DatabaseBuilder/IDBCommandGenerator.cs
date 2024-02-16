/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IDBCommandGenerator.cs 
*
* IDBCommandGenerator.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Data;
using System.Text;

namespace VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder
{
    /// <summary>
    /// Generates specialized statments used to modify a database 
    /// </summary>
    public interface IDBCommandGenerator
    {
        /// <summary>
        /// Compiles a valid database table creation statment from the <see cref="DataTable"/>
        /// defining data columns
        /// </summary>
        /// <param name="builder">The string builder used to build the creation statment</param>
        /// <param name="table">The <see cref="DataTable"/> that defines the columns within the table</param>
        void BuildCreateStatment(StringBuilder builder, DataTable table);
    }
}
