/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: SqlDbConnectionLoader.cs 
*
* SqlDbConnectionLoader.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Loading.Sql. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Text.Json;
using System.Data.Common;
using System.Runtime.CompilerServices;

using MySqlConnector;

using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// Provides common basic SQL loading extensions for plugins
    /// </summary>
    public static class SqlDbConnectionLoader
    {
        public const string SQL_CONFIG_KEY = "sql";
        public const string DB_PASSWORD_KEY = "db_password";

        private static readonly ConditionalWeakTable<PluginBase, Func<DbConnection>> LazyDbFuncTable = new();
        private static readonly ConditionalWeakTable<PluginBase, DbContextOptions> LazyCtxTable = new();
     

        /// <summary>
        /// Gets (or loads) the ambient sql connection factory for the current plugin
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="DbConnection"/> factory</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Func<DbConnection> GetConnectionFactory(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Get or load
            return LazyDbFuncTable.GetValue(plugin, FactoryLoader);
        }

        private static Func<DbConnection> FactoryLoader(PluginBase plugin)
        {
            IReadOnlyDictionary<string, JsonElement> sqlConf = plugin.GetConfig(SQL_CONFIG_KEY);
            
            //Get the db-type
            string? type = sqlConf.GetPropString("db_type");            

            if ("sqlite".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                //Use connection builder
                DbConnectionStringBuilder sqlBuilder = new SqliteConnectionStringBuilder()
                {
                    DataSource = sqlConf["source"].GetString(),
                };
                string connectionString = sqlBuilder.ToString();
                DbConnection DbFactory() => new SqliteConnection(connectionString);
                return DbFactory;
            }
            else if("mysql".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                using SecretResult? password = plugin.TryGetSecretAsync(DB_PASSWORD_KEY).Result;

                DbConnectionStringBuilder sqlBuilder = new MySqlConnectionStringBuilder()
                {
                    Server = sqlConf["hostname"].GetString(),
                    Database = sqlConf["database"].GetString(),
                    UserID = sqlConf["username"].GetString(),
                    Password = password?.Result.ToString(),
                    Pooling = true,
                    LoadBalance = MySqlLoadBalance.LeastConnections,
                    MinimumPoolSize = sqlConf["min_pool_size"].GetUInt32()
                };
                
                string connectionString = sqlBuilder.ToString();
                DbConnection DbFactory() => new MySqlConnection(connectionString);
                return DbFactory;
            }
            //Default to mssql
            else
            {
                using SecretResult? password = plugin.TryGetSecretAsync(DB_PASSWORD_KEY).Result;
                
                //Use connection builder
                DbConnectionStringBuilder sqlBuilder = new SqlConnectionStringBuilder()
                {
                    DataSource = sqlConf["hostname"].GetString(),
                    UserID = sqlConf["username"].GetString(),
                    Password = password?.Result.ToString(),
                    InitialCatalog = sqlConf["catalog"].GetString(),
                    IntegratedSecurity = sqlConf["ms_security"].GetBoolean(),
                    Pooling = true,
                    MinPoolSize = sqlConf["min_pool_size"].GetInt32(),
                    Replication = true
                };
                string connectionString = sqlBuilder.ToString();
                DbConnection DbFactory() => new SqlConnection(connectionString);
                return DbFactory;
            }           
        }

        /// <summary>
        /// Gets (or loads) the ambient <see cref="DbContextOptions"/> configured from 
        /// the ambient sql factory
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="DbContextOptions"/> for the current plugin</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <remarks>If plugin is in debug mode, writes log data to the default log</remarks>
        public static DbContextOptions GetContextOptions(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            return LazyCtxTable.GetValue(plugin, GetDbOptionsLoader);
        }

        private static DbContextOptions GetDbOptionsLoader(PluginBase plugin)
        {
            //Get a db connection object
            using DbConnection connection = plugin.GetConnectionFactory().Invoke();
            DbContextOptionsBuilder builder = new();
            
            //Determine connection type
            if(connection is SqlConnection sql)
            {
                //Use sql server from connection
                builder.UseSqlServer(sql.ConnectionString);
            }
            else if(connection is SqliteConnection slc)
            {
                builder.UseSqlite(slc.ConnectionString);
            }
            else if(connection is MySqlConnection msconn)
            {
                //Detect version
                ServerVersion version = ServerVersion.AutoDetect(msconn);

                builder.UseMySql(msconn.ConnectionString, version);
            }
            
            //Enable logging
            if(plugin.IsDebug())
            {
                builder.LogTo(plugin.Log.Debug);
            }
            
            //Get context and freez it before returning
            DbContextOptions options = builder.Options;
            options.Freeze();
            return options;
        }
    }
}
