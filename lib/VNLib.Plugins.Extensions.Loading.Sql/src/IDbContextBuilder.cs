/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IDbContextBuilder.cs 
*
* IDbContextBuilder.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part of the larger 
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

using System.ComponentModel.DataAnnotations.Schema;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// Passed to a <see cref="IDbTableDefinition"/> during a database creation event.
    /// </summary>
    public interface IDbContextBuilder
    {
        /// <summary>
        /// Defines the existance of a table within the database by its type name
        /// <para>
        /// If your entity defines a <see cref="TableAttribute"/>, this name value is used
        /// </para>
        /// </summary>
        /// <typeparam name="T">The entity type to build</typeparam>
        /// <returns>A new <see cref="IDbTableBuilder{T}"/> used to build the table for this entity</returns>
        IDbTableBuilder<T> DefineTable<T>();

        /// <summary>
        /// Defines the existance of a table within the database by the supplied table name
        /// </summary>
        /// <typeparam name="T">The entity type to build</typeparam>
        /// <returns>A new <see cref="IDbTableBuilder{T}"/> used to build the table for this entity</returns>
        IDbTableBuilder<T> DefineTable<T>(string tableName);
    }
}
