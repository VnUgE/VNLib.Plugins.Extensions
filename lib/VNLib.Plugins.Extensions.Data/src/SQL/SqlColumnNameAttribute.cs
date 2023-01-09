/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: SqlColumnNameAttribute.cs 
*
* SqlColumnNameAttribute.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Data.SQL
{
    /// <summary>
    /// Property attribute that specifies the property represents an SQL column in the database
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SqlColumnNameAttribute : Attribute
    {
        public bool Nullable { get; }
        public bool Unique { get; }
        public bool PrimaryKey { get; }
        public string ColumnName { get; }
        /// <summary>
        /// Specifies the property is an SQL column name
        /// </summary>
        /// <param name="columnName">Name of the SQL column</param>
        /// <param name="primaryKey"></param>
        /// <param name="nullable"></param>
        /// <param name="unique"></param>
        public SqlColumnNameAttribute(string columnName, bool primaryKey = false, bool nullable = true, bool unique = false)
        {
            this.ColumnName = columnName;
            this.PrimaryKey = primaryKey;
            this.Nullable = nullable;
            this.Unique = unique;
        }
    }
}