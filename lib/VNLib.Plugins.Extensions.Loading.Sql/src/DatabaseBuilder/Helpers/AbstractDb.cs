/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: AbstractDb.cs 
*
* AbstractDb.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
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
using System.IO;
using System.Data;
using System.Text;
using System.Collections.Generic;

namespace VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder.Helpers
{
    internal abstract class AbstractDb : IDBCommandGenerator
    {
        private static readonly Dictionary<Type, DbType> TypeMap = new()
        {
            [typeof(byte)] = DbType.Byte,
            [typeof(sbyte)] = DbType.SByte,
            [typeof(short)] = DbType.Int16,
            [typeof(ushort)] = DbType.UInt16,
            [typeof(int)] = DbType.Int32,
            [typeof(uint)] = DbType.UInt32,
            [typeof(long)] = DbType.Int64,
            [typeof(ulong)] = DbType.UInt64,
            [typeof(float)] = DbType.Single,
            [typeof(double)] = DbType.Double,
            [typeof(decimal)] = DbType.Decimal,
            [typeof(bool)] = DbType.Boolean,
            [typeof(string)] = DbType.String,
            [typeof(char)] = DbType.StringFixedLength,
            [typeof(Guid)] = DbType.Guid,
            [typeof(DateTime)] = DbType.DateTime,
            [typeof(DateTimeOffset)] = DbType.DateTimeOffset,

            [typeof(byte[])] = DbType.Binary,
            [typeof(byte?)] = DbType.Byte,
            [typeof(sbyte?)] = DbType.SByte,
            [typeof(short?)] = DbType.Int16,
            [typeof(ushort?)] = DbType.UInt16,
            [typeof(int?)] = DbType.Int32,
            [typeof(uint?)] = DbType.UInt32,
            [typeof(long?)] = DbType.Int64,
            [typeof(ulong?)] = DbType.UInt64,
            [typeof(float?)] = DbType.Single,
            [typeof(double?)] = DbType.Double,
            [typeof(decimal?)] = DbType.Decimal,
            [typeof(bool?)] = DbType.Boolean,
            [typeof(char?)] = DbType.StringFixedLength,
            [typeof(Guid?)] = DbType.Guid,
            [typeof(DateTime?)] = DbType.DateTime,
            [typeof(DateTimeOffset?)] = DbType.DateTimeOffset,
            [typeof(Stream)] = DbType.Binary
        };

        /// <summary>
        /// Gets the database string type name from the given .NET runtime type 
        /// information.
        /// </summary>
        /// <param name="type">The type to resolve</param>
        /// <returns>The type string that is realtive to the given database backend</returns>
        /// <exception cref="DbCreationException"></exception>
        public string GetTypeStringFromType(Type type)
        {
            if(!TypeMap.TryGetValue(type, out DbType dbType))
            {
                throw new DbCreationException($"The type {type} is not a supporeted database type");
            }

            //Get the type string
            return GetTypeStringFromDbType(dbType);
        }
       

        /// <summary>
        /// Gets a string property value from a discovered <see cref="DbType"/>
        /// </summary>
        /// <param name="type">The dbType discovered from the type according to the backing database</param>
        /// <returns>The parameter type as a string with an optional size variable</returns>
        protected abstract string GetTypeStringFromDbType(DbType type);

        ///<inheritdoc/>
        public abstract void BuildCreateStatment(StringBuilder builder, DataTable table);
    }
}
