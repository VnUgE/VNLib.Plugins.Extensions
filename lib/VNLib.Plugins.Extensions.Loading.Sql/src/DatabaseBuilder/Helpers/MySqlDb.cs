﻿/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: MySqlDb.cs 
*
* MySqlDb.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder.Helpers
{
    internal sealed class MySqlDb : AbstractDb
    {
        const int MAX_VARIABLE_SIZE = 8000;

        ///<inheritdoc/>
        public override void BuildCreateStatment(StringBuilder builder, DataTable table)
        {
            builder.AppendLine("CREATE TABLE IF NOT EXISTS `@tableName` (");

            //Add columns
            foreach (DataColumn col in table.Columns)
            {
                //Get dbType string
                string dbType;

                //Timestamps/rowversion must be handled specially for MySql optimistic concurrency
                if (col.IsTimeStamp())
                {
                    dbType = "TIMESTAMP";
                }
                else
                {
                    dbType = GetTypeStringFromType(col.DataType);
                }

                builder.Append('`')
                    .Append(col.ColumnName)
                    .Append("` ")
                    .Append(dbType);

                //Set primary key contraint
                if (col.IsPrimaryKey())
                {
                    builder.Append(" PRIMARY KEY");
                }
                //Set unique constraint (only if not pk)
                else if (col.Unique)
                {
                    builder.Append(" UNIQUE");
                }

                //If the value is not null, we can specify the default value
                if (!col.AllowDBNull)
                {
                    if (!string.IsNullOrWhiteSpace(col.DefaultValue?.ToString()))
                    {
                        builder.Append(" DEFAULT ");
                        builder.Append(col.DefaultValue);
                    }
                    else
                    {
                        //Set not null 
                        builder.Append(" NOT NULL");
                    }
                }

                //Set auto increment
                if (col.AutoIncrement)
                {
                    builder.Append(" AUTO_INCREMENT=")
                        .Append(col.AutoIncrementSeed);
                }

                //Trailing comma
                builder.AppendLine(",");

                //Set size if defined, we need to bypass column max length
                if (col.MaxLength() > MAX_VARIABLE_SIZE)
                {
                    builder.Replace("@size", "MAX");
                }
                else if(col.MaxLength() > 0)
                {
                    builder.Replace("@size", col.MaxLength().ToString());
                }
                else
                {
                    builder.Replace("(@size)", "");
                }
            }

            int index = builder.Length;
            while (builder[--index] != ',')
            { }

            //Remove the trailing comma
            builder.Remove(index, 1);

            //Close the create table command
            builder.AppendLine(")");

            //Replaced the table name variables
            builder.Replace("@tableName", table.TableName);
        }

        ///<inheritdoc/>
        protected override string GetTypeStringFromDbType(DbType type)
        {
            return type switch
            {
                DbType.AnsiString => "VARCHAR(@size)",
                DbType.Binary => "VARBINARY(@size)",
                DbType.Byte => "TINYINT",
                DbType.Boolean => "BOOL",
                DbType.Currency => "DECIMAL",
                DbType.Date => "DATE",
                DbType.DateTime => "DATETIME",
                DbType.Decimal => "DECIMAL",
                DbType.Double => "DOUBLE",
                DbType.Guid => "VARCHAR(@size)",
                DbType.Int16 => "SMALLINT",
                DbType.Int32 => "INT",
                DbType.Int64 => "BIGINT",
                DbType.Object => throw new NotSupportedException("A .NET object type is not a supported MySql data-type"),
                DbType.SByte => "TINYINT",
                DbType.Single => "FLOAT",
                DbType.String => "VARCHAR(@size)",
                DbType.Time => "TIME",
                DbType.UInt16 => "SMALLINT",
                DbType.UInt32 => "INT",
                DbType.UInt64 => "BIGINT",
                DbType.VarNumeric => throw new NotSupportedException("Variable numeric value is not a supported MySql data-type"),
                DbType.AnsiStringFixedLength => "TEXT(@size)",
                DbType.StringFixedLength => "TEXT(@size)",
                DbType.Xml => "VARCHAR(@size)",
                DbType.DateTime2 => "DATETIME",
                DbType.DateTimeOffset => throw new NotSupportedException("DateTimeOffset is not a supported MySql data-type"),
                _ => throw new NotSupportedException("The desired property data-type is not a supported MySql data-type"),
            };
        }
    }
}