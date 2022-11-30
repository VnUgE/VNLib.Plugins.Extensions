/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: SqlVariable.cs 
*
* SqlVariable.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Data;

namespace VNLib.Plugins.Extensions.Data.SQL
{
    /// <summary>
    /// Property attribute that specifies the property is to be used for a given command variable
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SqlVariable : Attribute
    {
        public string VariableName { get; init; }
        public DbType DataType { get; init; }
        public ParameterDirection Direction { get; init; }
        public int Size { get; init; }
        public bool Nullable { get; init; }
        /// <summary>
        /// Specifies the property to be used as an SQL variable
        /// </summary>
        /// <param name="variableName">Sql statement variable this property will substitute</param>
        /// <param name="dataType">The sql data the property will represent</param>
        /// <param name="direction">Data direction during execution</param>
        /// <param name="size">Column size</param>
        /// <param name="isNullable">Is this property allowed to be null</param>
        public SqlVariable(string variableName, DbType dataType, ParameterDirection direction, int size, bool isNullable)
        {
            this.VariableName = variableName;
            this.DataType = dataType;
            this.Direction = direction;
            this.Size = size;
            this.Nullable = isNullable;
        }
    }
}
