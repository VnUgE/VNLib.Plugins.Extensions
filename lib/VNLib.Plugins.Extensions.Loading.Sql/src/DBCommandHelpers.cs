/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: DBCommandHelpers.cs 
*
* DBCommandHelpers.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part of the larger 
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
using System.IO;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// Contains helper methods for loading and configuring SQL database connections
    /// </summary>
    public static class DBCommandHelpers
    {
        private const string MAX_LEN_BYPASS_KEY = "MaxLen";
        private const string TIMESTAMP_BYPASS = "TimeStamp";

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
        /// Gets the <see cref="DbType"/> from the given .NET runtime type information.
        /// </summary>
        /// <param name="col"></param>
        /// <returns>The columns <see cref="DbType"/> or an exception if the type is not supported</returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static DbType GetDbType(this DataColumn col)
        {
            ArgumentNullException.ThrowIfNull(col);
            ArgumentNullException.ThrowIfNull(col.DataType, nameof(col.DataType));

            if (!TypeMap.TryGetValue(col.DataType, out DbType dbType))
            {
                throw new NotSupportedException($"The type {col.DataType} is not a supporeted database type");
            }

            return dbType;
        }

        /// <summary>
        /// Sets the column ordinal index, or column position, within the table.
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="columOridinalIndex">The column's ordinal postion with the database</param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> SetPosition<T>(this IDbColumnBuilder<T> builder, int columOridinalIndex)
        {
            //Add ourself to the primary keys list
            builder.ConfigureColumn(col => col.SetOrdinal(columOridinalIndex));
            return builder;
        }

        /// <summary>
        /// Sets the auto-increment property on the column, this is just a short-cut to 
        /// setting the properties yourself on the column.
        /// </summary>
        /// <param name="seed">The starting (seed) of the increment parameter</param>
        /// <param name="increment">The increment/step parameter</param>
        /// <param name="builder"></param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> AutoIncrement<T>(this IDbColumnBuilder<T> builder, int seed = 1, int increment = 1)
        {
            //Set the auto-increment features
            builder.ConfigureColumn(col =>
            {
                col.AutoIncrement = true;
                col.AutoIncrementSeed = seed;
                col.AutoIncrementStep = increment;
            });
            return builder;
        }

        /// <summary>
        /// Sets the <see cref="DataColumn.MaxLength"/> property to the desired value. This value is set 
        /// via a <see cref="MaxLengthAttribute"/> if defined on the property, this method will override
        /// that value.
        /// </summary>
        /// <param name="maxLength">Override the maxium length property on the column</param>
        /// <param name="builder"></param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> MaxLength<T>(this IDbColumnBuilder<T> builder, int maxLength)
        {
            builder.ConfigureColumn(col => col.MaxLength(maxLength));
            return builder;
        }

        /// <summary>
        /// Override the <see cref="DataColumn.AllowDBNull"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="value">A value that indicate if you allow null in the column</param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> AllowNull<T>(this IDbColumnBuilder<T> builder, bool value)
        {
            builder.ConfigureColumn(col => col.AllowDBNull = value);
            return builder;
        }

        /// <summary>
        /// Sets the <see cref="DataColumn.Unique"/> property to true
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="builder"></param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> Unique<T>(this IDbColumnBuilder<T> builder)
        {
            builder.ConfigureColumn(static col => col.Unique = true);
            return builder;
        }

        /// <summary>
        /// Sets the default value for the column
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="builder"></param>
        /// <param name="defaultValue">The column default value</param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> WithDefault<T>(this IDbColumnBuilder<T> builder, object defaultValue)
        {
            builder.ConfigureColumn(col => col.DefaultValue = defaultValue);
            return builder;
        }

        /// <summary>
        /// Specifies this column is a RowVersion/TimeStamp for optimistic concurrency for some 
        /// databases.
        /// <para>
        /// This vaule is set by default if the entity property specifies a <see cref="TimestampAttribute"/>
        /// </para>
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="builder"></param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> TimeStamp<T>(this IDbColumnBuilder<T> builder)
        {
            builder.ConfigureColumn(static col => col.SetTimeStamp());
            return builder;
        }


        /// <summary>
        /// Sets the column as a PrimaryKey in the table. You may also set the 
        /// <see cref="KeyAttribute"/> on the property.
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="builder"></param>
        /// <returns>The chainable <see cref="IDbColumnBuilder{T}"/></returns>
        public static IDbColumnBuilder<T> SetIsKey<T>(this IDbColumnBuilder<T> builder)
        {
            //Add ourself to the primary keys list
            builder.ConfigureColumn(static col => col.AddToPrimaryKeys());
            return builder;
        }

        /// <summary>
        /// Gets a value that determines if the current column is a primary key
        /// </summary>
        /// <param name="col"></param>
        /// <returns>True if the collumn is part of the primary keys</returns>
        public static bool IsPrimaryKey(this DataColumn col)
        {
            ArgumentNullException.ThrowIfNull(col);
            ArgumentNullException.ThrowIfNull(col.Table, nameof(col.Table));
            return col.Table.PrimaryKey.Contains(col);
        }

        /*
         * I am bypassing the DataColumn.MaxLength property because it does more validation
         * than we need against the type and can cause unecessary issues, so im just bypassing it 
         * for now
         */

        internal static void MaxLength(this DataColumn column, int length)
        {
            column.ExtendedProperties[MAX_LEN_BYPASS_KEY] = length;
        }

        /// <summary>
        /// Gets the max length of the column
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public static int MaxLength(this DataColumn column)
        {
            if (column.ExtendedProperties.ContainsKey(MAX_LEN_BYPASS_KEY))
            {
                object? value = column.ExtendedProperties[MAX_LEN_BYPASS_KEY];
                if (value is int length)
                {
                    return length;
                }
            }
            return column.MaxLength;
        }

        internal static void SetTimeStamp(this DataColumn column)
        {
            //We just need to set the key
            column.ExtendedProperties[TIMESTAMP_BYPASS] = null;
        }

        /// <summary>
        /// Gets a value that indicates if the column is a timestamp
        /// </summary>
        /// <param name="column"></param>
        /// <returns>True if the column is a timestamp column</returns>
        public static bool IsTimeStamp(this DataColumn column)
        {
            return column.ExtendedProperties.ContainsKey(TIMESTAMP_BYPASS);
        }

        internal static void AddToPrimaryKeys(this DataColumn col)
        {
            //Add the column to the table's primary key array
            List<DataColumn> cols = new(col.Table!.PrimaryKey)
            {
                col
            };

            //Update the table primary keys now that this col has been added
            col.Table.PrimaryKey = cols.Distinct().ToArray();
        }
    }
}
