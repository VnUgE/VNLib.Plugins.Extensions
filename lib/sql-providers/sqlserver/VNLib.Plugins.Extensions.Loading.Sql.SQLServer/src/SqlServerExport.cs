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
using System.Text.Json;
using System.Data.Common;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Extensions.Sql
{

    [ServiceExport]
    [ConfigurationName("sql", Required = true)]
    public sealed class SqlServerExport(PluginBase plugin, IConfigScope config)
    {
        private async Task<string> BuildConnStringAsync()
        {
            //See if the user suggested a raw connection string
            if (config.TryGetProperty("connection_string", ps => ps.GetString(), out string? conString))
            {
                return conString!;
            }
            else if (config.TryGetValue("json", out JsonElement value))
            {
                JsonSerializerOptions opt = new(JsonSerializerDefaults.General)
                {
                    AllowTrailingCommas = true,
                    IgnoreReadOnlyFields = true,
                    DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
                };

                SqlConnectionStringBuilder b = value.Deserialize<SqlConnectionStringBuilder>(opt)!;

                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");

                b.Password = secret?.Result.ToString();
                return b.ConnectionString;
            }
            else
            {
                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");

                // Build connection string
                return new SqlConnectionStringBuilder()
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
                }
                .ConnectionString;
            }
        }

        /*
         * NOTICE:
         * Function names must be public and must match the SqlConnectionLoader delegate names.
         * 
         * GetDbConnection - A sync or async function that takes a configuration scope and 
         * returns a DbConnection factory
         * 
         * GetDbOptions - A sync or async function that takes a configuration scope and
         * returns a DbConnectionOptions instance
         * 
         * GetProviderName - Returns a string that is the provider name for the connection
         */

        public string GetProviderName() => "sqlserver";

        public async Task<Func<DbConnection>> GetDbConnectionAsync(IConfigScope sqlConfig)
        {
            //Store local copy of the connection string, probably not the best idea because of the password, but best for now
            string connString = await BuildConnStringAsync();

            return () => new SqlConnection(connString);
        }

        public async Task<DbContextOptions> GetDbOptionsAsync(IConfigScope sqlConfig)
        {
            //Get the connection string from the configuration
            string connString = await BuildConnStringAsync();

            //Build the options using the mysql extension method
            DbContextOptionsBuilder b = new();
            b.UseSqlServer(connString);

            //Write debug loggin to the debug log if the user has it enabled or the plugin is in debug mode
            if (sqlConfig.GetValueOrDefault("debug", p => p.GetBoolean(), false) || plugin.IsDebug())
            {
                //Write the SQL to the debug log
                b.LogTo((v) => plugin.Log.Debug("SqlServer: {v}", v));
            }

            return b.Options;
        }
    }
}
