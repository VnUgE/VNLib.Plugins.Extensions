/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.SQLServer
* File: SQLServerExport.cs 
*
* SQLServerExport.cs is part of VNLib.Plugins.Extensions.Loading.Sql.SQLServer which 
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

using Microsoft.Data.SqlClient;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;

namespace VNLib.Plugins.Extensions.Sql
{

    [ServiceExport]
    [ConfigurationName("sql", Required = true)]
    public sealed class SqlServerExport(PluginBase plugin, IConfigScope config) : IRuntimeDbProvider
    {
        private async Task<string> BuildConnStringAsync()
        {
            SqlConnectionStringBuilder sb;

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

                //try to get the connection string from the json serialzied object directly
                sb = value.Deserialize<SqlConnectionStringBuilder>(opt)!;

                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");
                sb.Password = secret?.Result.ToString();
            }
            else
            {
                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");

                // Build connection string
                sb = new()
                {
                    DataSource = config["hostname"].GetString(),
                    InitialCatalog = config["catalog"].GetString(),
                    UserID = config["username"].GetString(),
                    Pooling = true,


                    ApplicationName = config.GetValueOrDefault("application_name", p => p.GetString(), string.Empty),
                    HostNameInCertificate = config.GetValueOrDefault("hostname_in_certificate", p => p.GetString(), string.Empty),
                    PacketSize = config.GetValueOrDefault("packet_size", p => p.GetInt32(), 8000),
                    Encrypt = config.GetValueOrDefault("encrypted", p => p.GetBoolean(), false),
                    IntegratedSecurity = config.GetValueOrDefault("integrated_security", p => p.GetBoolean(), false),
                    MultipleActiveResultSets = config.GetValueOrDefault("multiple_active_result_sets", p => p.GetBoolean(), false),
                    ConnectTimeout = config.GetValueOrDefault("connect_timeout", p => p.GetInt32(), 15),
                    LoadBalanceTimeout = config.GetValueOrDefault("load_balance_timeout", p => p.GetInt32(), 0),
                    MaxPoolSize = config.GetValueOrDefault("max_pool_size", p => p.GetInt32(), 100),
                    MinPoolSize = config.GetValueOrDefault("min_pool_size", p => p.GetInt32(), 0),
                    TransactionBinding = config.GetValueOrDefault("transaction_binding", p => p.GetString(), "Implicit Unbind"),
                    TypeSystemVersion = config.GetValueOrDefault("type_system_version", p => p.GetString(), "Latest"),
                    WorkstationID = config.GetValueOrDefault("workstation_id", p => p.GetString(), string.Empty),
                    CurrentLanguage = config.GetValueOrDefault("current_language", p => p.GetString(), "us_english"),
                    PersistSecurityInfo = config.GetValueOrDefault("persist_security_info", p => p.GetBoolean(), false),
                    Replication = config.GetValueOrDefault("replication", p => p.GetBoolean(), false),
                    TrustServerCertificate = config.GetValueOrDefault("trust_server_certificate", p => p.GetBoolean(), false),
                    UserInstance = config.GetValueOrDefault("user_instance", p => p.GetBoolean(), false),

                    Password = secret?.Result.ToString(),
                };
            }

            return sb.ConnectionString;
        }        

        ///<inheritdoc/>
        public async Task<Func<DbConnection>> GetDbConnectionAsync()
        {
            //Store local copy of the connection string, probably not the best idea because of the password, but best for now
            string connString = await BuildConnStringAsync();
            return () => new SqlConnection(connString);
        }

        ///<inheritdoc/>
        public async Task<DbContextOptions> GetDbOptionsAsync()
        {
            //Get the connection string from the configuration
            string connString = await BuildConnStringAsync();

            //Build the options using the mysql extension method
            DbContextOptionsBuilder b = new();
            b.UseSqlServer(connString);

            //Write debug loggin to the debug log if the user has it enabled or the plugin is in debug mode
            if (config.GetValueOrDefault("debug", p => p.GetBoolean(), false) || plugin.IsDebug())
            {
                //Write the SQL to the debug log
                b.LogTo((v) => plugin.Log.Debug("SqlServer: {v}", v));
            }

            return b.Options;
        }

        ///<inheritdoc/>
        public IDBCommandGenerator GetCommandGenerator() => new MsSqlDb();


        internal class MsSqlDb : IDBCommandGenerator
        {
            const int MAX_VARIABLE_SIZE = 8000;

            ///<inheritdoc/>
            public void BuildCreateStatment(StringBuilder builder, DataTable table)
            {
                builder.AppendLine("IF OBJECT_ID(N'[dbo].[@tableName]', N'U') IS NULL");
                builder.AppendLine("CREATE TABLE [dbo].[@tableName] (");

                //Add columns
                foreach (DataColumn col in table.Columns)
                {
                    //Get dbType string
                    string dbType;

                    //Timestamps/rowversion must be handled specially for msSql
                    if (col.IsTimeStamp())
                    {
                        dbType = "ROWVERSION";
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
                        builder.Append(" IDENTITY(")
                            .Append(col.AutoIncrementSeed)
                            .Append(',')
                            .Append(col.AutoIncrementStep)
                            .Append(')');
                    }

                    //Trailing comma
                    builder.AppendLine(",");


                    //Set size if defined
                    if (col.MaxLength() > MAX_VARIABLE_SIZE)
                    {
                        builder.Replace("@size", "MAX");
                    }
                    else if (col.MaxLength() > 0)
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
            private static string GetTypeStringFromDbType(DataColumn col)
            {
                return col.GetDbType() switch
                {
                    DbType.AnsiString => "VARCHAR(@size)",
                    DbType.Binary => "VARBINARY(@size)",
                    DbType.Byte => "TINYINT",
                    DbType.Boolean => "BOOL",
                    DbType.Currency => "MONEY",
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
                    //unicode string support
                    DbType.String => "NVARCHAR(@size)",
                    DbType.Time => "TIME",
                    DbType.UInt16 => "SMALLINT",
                    DbType.UInt32 => "INT",
                    DbType.UInt64 => "BIGINT",
                    DbType.VarNumeric => throw new NotSupportedException("Variable numeric value is not a supported MySql data-type"),
                    DbType.AnsiStringFixedLength => "TEXT(@size)",
                    //unicode text support
                    DbType.StringFixedLength => "NTEXT(@size)",
                    //Define custom xml schema variable
                    DbType.Xml => "XML(@xml_schema_collection)",
                    DbType.DateTime2 => "DATETIME2",
                    DbType.DateTimeOffset => "DATETIMEOFFSET",
                    _ => throw new NotSupportedException("The desired property data-type is not a supported MySql data-type"),
                };
            }
        }
    }
}
