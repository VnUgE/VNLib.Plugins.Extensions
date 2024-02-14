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
using System.Text.Json;
using System.Data.Common;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Microsoft.Data.Sqlite;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Extensions.Sql
{

    [ServiceExport]
    [ConfigurationName("sql", Required = true)]
    public sealed class SQLiteExport(PluginBase plugin, IConfigScope config)
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

                SqliteConnectionStringBuilder b = value.Deserialize<SqliteConnectionStringBuilder>(opt)!;

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
                return new SqliteConnectionStringBuilder()
                {
                    DataSource = config["source"].GetString(),
                    Pooling = true,
                    Cache = SqliteCacheMode.Default,
                    RecursiveTriggers = config.GetValueOrDefault("recursive_triggers", p => p.GetBoolean(), false),
                    DefaultTimeout = config.GetValueOrDefault("timeout", p => p.GetInt32(), 30),
                    Mode = config.GetValueOrDefault("mode", p => (SqliteOpenMode)p.GetInt32(), SqliteOpenMode.ReadWriteCreate),

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

        public string GetProviderName() => "sqlite";        //Use default handler for sqlite db creation

        public async Task<Func<DbConnection>> GetDbConnectionAsync(IConfigScope sqlConfig)
        { 
            //Store local copy of the connection string, probably not the best idea because of the password, but best for now
            string connString = await BuildConnStringAsync();

            return () => new SqliteConnection(connString);
        }

        public async Task<DbContextOptions> GetDbOptionsAsync(IConfigScope sqlConfig)
        {
            //Get the connection string from the configuration
            string connString = await BuildConnStringAsync();

            DbContextOptionsBuilder b = new();
            b.UseSqlite(connString);

            //Write debug loggin to the debug log if the user has it enabled or the plugin is in debug mode
            if (sqlConfig.GetValueOrDefault("debug", p => p.GetBoolean(), false) || plugin.IsDebug())
            {
                //Write the SQL to the debug log
                b.LogTo((v) => plugin.Log.Debug("SQLite: {v}", v));
            }

            return b.Options;
        }
    }
}
