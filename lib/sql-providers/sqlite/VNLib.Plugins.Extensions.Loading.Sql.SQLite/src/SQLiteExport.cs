/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.SQLite
* File: SQLiteExport.cs 
*
* SQLiteExport.cs is part of VNLib.Plugins.Extensions.Loading.Sql.SQLite which 
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
using System.Text;
using System.Text.Json;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using Microsoft.Data.Sqlite;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;

namespace VNLib.Plugins.Extensions.Sql
{

    [ServiceExport]
    [ConfigurationName("sql", Required = true)]
    public sealed class SQLiteExport(PluginBase plugin, IConfigScope config) : IRuntimeDbProvider
    {

        private async Task<string> BuildConnStringAsync()
        {
            SqliteConnectionStringBuilder sb;

            //See if the user suggested a raw connection string
            if (config.TryGetProperty("connection_string", ps => ps.GetString(), out string? conString))
            {
                sb = new(conString);

                //If the user did not provide a password, try to get it from secret storage
                if (string.IsNullOrWhiteSpace(sb.Password))
                {
                    using ISecretResult? password = await plugin.TryGetSecretAsync("db_password");
                    sb.Password = password?.Result.ToString();
                }
            }
            else if (config.TryGetValue("json", out JsonElement value))
            {
                JsonSerializerOptions opt = new(JsonSerializerDefaults.General)
                {
                    AllowTrailingCommas = true,
                    IgnoreReadOnlyFields = true,
                    DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
                };

                sb = value.Deserialize<SqliteConnectionStringBuilder>(opt)!;

                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");
                sb.Password = secret?.Result.ToString();
            }
            else
            {
                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");

                // Build connection strin
                sb = new()
                {
                    DataSource = config["source"].GetString(),
                    Pooling = true,
                    Cache = SqliteCacheMode.Default,
                    RecursiveTriggers = config.GetValueOrDefault("recursive_triggers", p => p.GetBoolean(), false),
                    DefaultTimeout = config.GetValueOrDefault("timeout", p => p.GetInt32(), 30),
                    Mode = config.GetValueOrDefault("mode", p => (SqliteOpenMode)p.GetInt32(), SqliteOpenMode.ReadWriteCreate),

                    Password = secret?.Result.ToString(),
                };                
            }

            return sb.ConnectionString;
        }

        /// <inheritdoc/>
        public async Task<Func<DbConnection>> GetDbConnectionAsync()
        {
            //Store local copy of the connection string, probably not the best idea because of the password, but best for now
            string connString = await BuildConnStringAsync();

            return () => new SqliteConnection(connString);
        }

        /// <inheritdoc/>
        public async Task<DbContextOptions> GetDbOptionsAsync()
        {
            //Get the connection string from the configuration
            string connString = await BuildConnStringAsync();

            DbContextOptionsBuilder b = new();
            b.UseSqlite(connString);

            //Write debug loggin to the debug log if the user has it enabled or the plugin is in debug mode
            if (config.GetValueOrDefault("debug", p => p.GetBoolean(), false) || plugin.IsDebug())
            {
                //Write the SQL to the debug log
                b.LogTo((v) => plugin.Log.Debug("SQLite: {v}", v));
            }

            return b.Options;
        }
        
        /// <inheritdoc/>
        public IDBCommandGenerator GetCommandGenerator() => new SqlLiteDb();
        
        /// <inheritdoc/>
        public override string ToString() => $"SQLite DB runtime provider : {GetHashCode()}";

        internal sealed class SqlLiteDb : IDBCommandGenerator
        {
            public void BuildCreateStatment(StringBuilder builder, DataTable table)
            {
                builder.AppendLine("CREATE TABLE IF NOT EXISTS @tableName (");

                List<DataColumn> uniqueCols = new();

                //Add columns
                foreach (DataColumn col in table.Columns)
                {
                    //Get dbType string
                    string dbType;

                    //Timestamps/rowversion must be handled specially for MySql optimistic concurrency
                    if (col.IsTimeStamp())
                    {
                        dbType = "BINARY(8)";
                        //We may also set the AllowNull property
                        col.AllowDBNull = true;
                    }
                    else
                    {
                        dbType = GetTypeStringFromDbType(col);
                    }

                    builder.Append('[')
                      .Append(col.ColumnName)
                      .Append("] ")
                      .Append(dbType);

                    //Set primary key contraint
                    if (col.IsPrimaryKey())
                    {
                        builder.Append(" PRIMARY KEY");
                    }
                    //Set unique constraint (only if not pk)
                    else if (col.Unique)
                    {
                        //Add the column to unique list for later
                        uniqueCols.Add(col);
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
                        builder.Append(" AUTOINCREMENT ")
                            .Append(col.AutoIncrementSeed);
                    }

                    //Trailing comma
                    builder.AppendLine(",");

                    //No sizing for sqlite
                }

                //Add unique column contraints
                if (uniqueCols.Count != 0)
                {
                    builder.Append("UNIQUE(");
                    for (int i = 0; i < uniqueCols.Count;)
                    {
                        //Add column name
                        builder.Append(uniqueCols[i].ColumnName);

                        i++;

                        //Add trailing commas
                        if (i < uniqueCols.Count)
                        {
                            builder.Append(',');
                        }
                    }

                    //Add trailing )
                    builder.AppendLine(")");
                }
                else
                {
                    //remove trailing comma
                    int index = builder.Length;
                    while (builder[--index] != ',')
                    { }

                    //Remove the trailing comma
                    builder.Remove(index, 1);
                }

                //Close the create table command
                builder.AppendLine(")");

                //Replaced the table name variables
                builder.Replace("@tableName", table.TableName);
            }

            private static string GetTypeStringFromDbType(DataColumn col)
            {
                return col.GetDbType() switch
                {
                    DbType.AnsiString => "TEXT",
                    DbType.Binary => "BLOB",
                    DbType.Byte => "INTEGER",
                    DbType.Boolean => "INTEGER",
                    DbType.Currency => "NUMERIC",
                    DbType.Date => "NUMERIC",
                    DbType.DateTime => "NUMERIC",
                    DbType.Decimal => "NUMERIC",
                    DbType.Double => "NUMERIC",
                    DbType.Guid => "TEXT",
                    DbType.Int16 => "INTEGER",
                    DbType.Int32 => "INTEGER",
                    DbType.Int64 => "INTEGER",
                    DbType.Object => throw new NotSupportedException("A .NET object type is not a supported MySql data-type"),
                    DbType.SByte => "INTEGER",
                    DbType.Single => "NUMERIC",
                    DbType.String => "TEXT",
                    DbType.Time => "TEXT",
                    DbType.UInt16 => "INTEGER",
                    DbType.UInt32 => "INTEGER",
                    DbType.UInt64 => "INTEGER",
                    DbType.VarNumeric => "BLOB",
                    DbType.AnsiStringFixedLength => "TEXT",
                    DbType.StringFixedLength => "TEXT",
                    DbType.Xml => "TEXT",
                    DbType.DateTime2 => "NUMERIC",
                    DbType.DateTimeOffset => "NUMERIC",
                    _ => throw new NotSupportedException("The desired property data-type is not a supported MySql data-type"),
                };
            }
        }
    }
}
