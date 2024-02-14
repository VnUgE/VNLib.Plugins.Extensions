/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Text.Json;
using System.Data.Common;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using MySql.Data.MySqlClient;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Extensions.Sql
{

    [ServiceExport]
    [ConfigurationName("sql", Required = true)]
    public sealed class MySqlExport(PluginBase plugin, IConfigScope config)
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

                MySqlConnectionStringBuilder b = value.Deserialize<MySqlConnectionStringBuilder>(opt)!;

                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");

                b.Password = secret?.Result.ToString();
                return b.ConnectionString;
            }
            else
            {
                //Get the password from the secret manager
                using ISecretResult? secret = await plugin.TryGetSecretAsync("db_password");

                // Build connection strin
                return new MySqlConnectionStringBuilder()
                {
                    Server = config["hostname"].GetString(),
                    Database = config["catalog"].GetString(),
                    UserID = config["username"].GetString(),
                    Pooling = true,
                    MinimumPoolSize = config.GetValueOrDefault("min_pool_size", p => p.GetUInt32(), 10u),
                    MaximumPoolSize = config.GetValueOrDefault("max_pool_size", p => p.GetUInt32(), 50u),
                    AllowBatch = config.GetValueOrDefault("allow_batch", p => p.GetBoolean(), true),
                    ConnectionLifeTime = config.GetValueOrDefault("connection_lifetime", p => p.GetUInt32(), 0u),
                    ConnectionTimeout = config.GetValueOrDefault("connection_timeout", p => p.GetUInt32(), 15u),
                    Port = config.GetValueOrDefault("port", p => p.GetUInt32(), 3306u),
                    PipeName = config.GetValueOrDefault("pipe_name", p => p.GetString(), null),
                    AllowLoadLocalInfile = config.GetValueOrDefault("allow_load_local_infile", p => p.GetBoolean(), false),
                    AllowLoadLocalInfileInPath = config.GetValueOrDefault("allow_load_local_infile_in_path", p => p.GetString(), null),

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

        public string GetProviderName() => "mysql";

        public async Task<Func<DbConnection>> GetDbConnectionAsync(IConfigScope sqlConfig)
        {
            //Store local copy of the connection string, probably not the best idea because of the password, but best for now
            string connString = await BuildConnStringAsync();

            return () => new MySqlConnection(connString);
        }

        public async Task<DbContextOptions> GetDbOptionsAsync(IConfigScope sqlConfig)
        {
            //Get the connection string from the configuration
            string connString = await BuildConnStringAsync();

            //Build the options using the mysql extension method
            DbContextOptionsBuilder b = new();
            b.UseMySQL(connString);

            //Write debug loggin to the debug log if the user has it enabled or the plugin is in debug mode
            if (sqlConfig.GetValueOrDefault("debug", p => p.GetBoolean(), false) || plugin.IsDebug())
            {
                //Write the SQL to the debug log
                b.LogTo((v) => plugin.Log.Debug("MySql: {v}", v));
            }

            return b.Options;
        }
    }
}
