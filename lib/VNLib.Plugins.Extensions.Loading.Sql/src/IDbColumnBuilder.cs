/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IDbColumnBuilder.cs 
*
* IDbColumnBuilder.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
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
using System.Data;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// A tool used to configure your new columns within the database
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDbColumnBuilder<T>
    {
        /// <summary>
        /// Gets the original <see cref="IDbTableBuilder{T}"/> to move to the next column
        /// (allows method chaining)
        /// </summary>
        /// <returns>The original <see cref="IDbTableBuilder{T}"/> to define the next column</returns>
        IDbTableBuilder<T> Next();

        /// <summary>
        /// Allows you to configure your new column. You may call this method as many times as necessary 
        /// to configure your new column.
        /// <para> 
        /// <code> 
        /// .ConfigureColumn(c => c.ColumnName = "ColumnName")
        /// .ConfigureColumn(c => c.MaxLength = 1000)
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="columnSetter">Your callback action that alters the column state</param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        IDbColumnBuilder<T> ConfigureColumn(Action<DataColumn> columnSetter);
    }
}
