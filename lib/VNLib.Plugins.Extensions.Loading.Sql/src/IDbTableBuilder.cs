/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IDbTableBuilder.cs 
*
* IDbTableBuilder.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
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
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations.Schema;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// A builder type that allows you to define columns within a database table
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public interface IDbTableBuilder<T>
    {
        /// <summary>
        /// Define a column from your entity type in the new table. Column ordinal positions are defined 
        /// by the order this method is called on a table.
        /// </summary>
        /// <typeparam name="TColumn">The column type</typeparam>
        /// <param name="propSelector">The entity property selector</param>
        /// <returns>The new column builder for the entity</returns>
        /// <remarks>
        /// You may alter the column name by specifying the <see cref="ColumnAttribute"/> on a given property
        /// or by overriding the column name:
        /// <code> .ConfigureColumn(c => c.ColumnName = "MyColumnName")</code>
        /// </remarks>
        IDbColumnBuilder<T> WithColumn<TColumn>(Expression<Func<T, TColumn>> propSelector);
    }
}
