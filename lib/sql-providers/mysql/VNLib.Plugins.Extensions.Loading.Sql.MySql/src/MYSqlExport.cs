/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.Mysql
* File: MySqlExport.cs 
*
* MySqlExport.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Data.Common;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using MySqlConnector;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;

namespace VNLib.Plugins.Extensions.Sql
{

    [ServiceExport]
    [ConfigurationName("sql", Required = true)]
    public sealed class MySQLExport(PluginBase plugin, IConfigScope config) : IRuntimeDbProvider
    {
        private async Task<string> BuildConnStringAsync()
        {
            IOnDemandSecret pwd = plugin.Secrets().GetOnDemandSecret("db_password");

            MySqlConnectionStringBuilder sb;

            //See if the user suggested a raw connection string
            if (
                config.TryGetProperty("connection_string", ps => ps.GetString(), out string? conString) &&
                !string.IsNullOrEmpty(conString)
            )
            {
                sb = new(conString);

                //If the user did not provide a password, try to get it from secret storage
                if (string.IsNullOrWhiteSpace(sb.Password))
                {
                    using ISecretResult? password = await pwd.FetchSecretAsync();
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

                sb = value.Deserialize<MySqlConnectionStringBuilder>(opt)!;

                //Get the db password from the secret manager
                using ISecretResult? secret = await pwd.FetchSecretAsync();
                sb.Password = secret?.Result.ToString();
            }
            else
            {
                //Get the password from the secret manager
                using ISecretResult? secret = await pwd.FetchSecretAsync();

                sb = new()
                {
                    Pooling         = true,
                    Server          = config.GetRequiredProperty<string>("hostname"),
                    Database        = config.GetRequiredProperty<string>("catalog"),
                    UserID          = config.GetRequiredProperty<string>("username"),
                    MinimumPoolSize = config.GetValueOrDefault("min_pool_size", 10u),
                    MaximumPoolSize = config.GetValueOrDefault("max_pool_size", 50u),
                    Password        = secret?.Result.ToString(),
                };

                if (config.TryGetProperty("port", out ushort port))
                {
                    sb.Port = port;
                }

                if (config.TryGetProperty("ssl_mode", out string? sslMode)
                    && Enum.TryParse(sslMode, true, out MySqlSslMode mode))
                {
                    sb.SslMode = mode;
                }

                if (config.TryGetProperty("connection_lifetime", out uint connLife))
                {
                    sb.ConnectionLifeTime = connLife;
                }

                if (config.TryGetProperty("connection_timeout", out uint connTimeout))
                {
                    sb.ConnectionTimeout = connTimeout;
                }

                if (config.TryGetProperty("pipe_name", out string? pipeName))
                {
                    sb.PipeName = pipeName;
                }

                if (config.TryGetProperty("allow_load_local_infile", out bool allowLoadLocalInfile))
                {
                    sb.AllowLoadLocalInfile = allowLoadLocalInfile;
                }

                if (config.TryGetProperty("default_command_timeout", out uint defaultCommandTimeout))
                {
                    sb.DefaultCommandTimeout = defaultCommandTimeout;
                }

                if (config.TryGetProperty("interactive_session", out bool interactiveSession))
                {
                    sb.InteractiveSession = interactiveSession;
                }
            }

            return sb.ConnectionString;
        }

        ///<inheritdoc/>
        public async Task<Func<DbConnection>> GetDbConnectionAsync()
        {
            //Store local copy of the connection string, probably not the best idea because of the password, but best for now
            string connString = await BuildConnStringAsync();

            return () => new MySqlConnection(connString);
        }

        ///<inheritdoc/>
        public async Task<DbContextOptions> GetDbOptionsAsync()
        {
            //Get the connection string from the configuration
            string connString = await BuildConnStringAsync();

            //Build the options using the mysql extension method
            DbContextOptionsBuilder b = new();
            b.UseMySql(connString, ServerVersion.AutoDetect(connString));

            //Write debug loggin to the debug log if the user has it enabled or the plugin is in debug mode
            if (config.GetValueOrDefault("debug", false) || plugin.IsDebug())
            {
                //Write the SQL to the debug log
                b.LogTo((v) => plugin.Log.Debug("MySql: {v}", v));
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
                        dbType = GetTypeStringFromDbType(col);
                    }

                    builder.Append('`')
                        .Append(col.ColumnName)
                        .Append("` ")
                        .Append(dbType);

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

                AddConstraints(builder, table);

                //Close the create table command
                builder.AppendLine(")");

                //Replaced the table name variables
                builder.Replace("@tableName", table.TableName);
            }

            private static void AddConstraints(StringBuilder builder, DataTable table)
            {
                DataColumn[] primaryKeys = table.Columns.OfType<DataColumn>()
                .Where(static c => c.IsPrimaryKey())
                .ToArray();

                if (primaryKeys.Length > 0)
                {
                    builder.AppendLine(",")
                        .Append("CONSTRAINT ")
                        .Append(table.TableName)
                        .Append("_pk PRIMARY KEY (")
                        .AppendJoin(", ", primaryKeys.Select(static c => c.ColumnName))
                        .Append(')');
                }

                //Repeat for unique constraints
                DataColumn[] uniqueKeys = table.Columns.OfType<DataColumn>()
                .Where(static c => c.Unique && !c.IsPrimaryKey())
                .ToArray();

                if (uniqueKeys.Length > 0)
                {
                    builder.AppendLine(",")
                        .Append("CONSTRAINT ")
                        .Append(table.TableName)
                        .Append("_unique UNIQUE (")
                        .AppendJoin(", ", uniqueKeys.Select(static c => c.ColumnName))
                        .Append(')');
                }

                builder.AppendLine();
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
}
