/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: SqlDbConnectionLoader.cs 
*
* SqlDbConnectionLoader.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part of the larger 
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
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using MySqlConnector;

using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;
using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder.Helpers;

namespace VNLib.Plugins.Extensions.Loading.Sql
{

    /// <summary>
    /// Provides common basic SQL loading extensions for plugins
    /// </summary>
    public static class SqlDbConnectionLoader
    {
        public const string SQL_CONFIG_KEY = "sql";
        public const string DB_PASSWORD_KEY = "db_password";
        public const string EXTERN_SQL_LIB_KEY = "custom_assembly";

        public const string EXTERN_LIB_GET_CONN_FUNC_NAME = "GetDbConnections";

        private const string MAX_LEN_BYPASS_KEY = "MaxLen";
        private const string TIMESTAMP_BYPASS = "TimeStamp";        
  

        /// <summary>
        /// Gets (or loads) the ambient sql connection factory for the current plugin 
        /// and synchronously blocks the current thread until the connection is ready.
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="DbConnection"/> factory</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Func<DbConnection> GetConnectionFactory(this PluginBase plugin)
        {
            //Get the async factory
            IAsyncLazy<Func<DbConnection>> async = plugin.GetConnectionFactoryAsync();

            //Block the current thread until the connection is ready
            return async.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets (or loads) the ambient sql connection factory for the current plugin
        /// asynchronously
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="DbConnection"/> factory</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IAsyncLazy<Func<DbConnection>> GetConnectionFactoryAsync(this PluginBase plugin)
        {
            static IAsyncLazy<Func<DbConnection>> FactoryLoader(PluginBase plugin)
            {
                return Task.Run(() => GetFactoryLoaderAsync(plugin)).AsLazy();
            }

            plugin.ThrowIfUnloaded();

            //Get or load
            return LoadingExtensions.GetOrCreateSingleton(plugin, FactoryLoader);
        }

        private async static Task<Func<DbConnection>> GetFactoryLoaderAsync(PluginBase plugin)
        {
            IConfigScope sqlConf = plugin.GetConfig(SQL_CONFIG_KEY);

            //See if the user wants to use a custom assembly
            if (sqlConf.ContainsKey(EXTERN_SQL_LIB_KEY))
            {
                string dllPath = sqlConf.GetRequiredProperty(EXTERN_SQL_LIB_KEY, k => k.GetString()!);

                //Load the library and get instance
                object dbProvider = plugin.CreateServiceExternal<object>(dllPath);

                return ManagedLibrary.GetMethod<Func<DbConnection>>(dbProvider, EXTERN_LIB_GET_CONN_FUNC_NAME);
            }
            
            //Get the db-type
            string? type = sqlConf.GetPropString("db_type");

            //Try to get the password and always dispose the secret value
            using ISecretResult? password = await plugin.TryGetSecretAsync(DB_PASSWORD_KEY);

            DbConnectionStringBuilder sqlBuilder;

            if ("sqlite".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                //Use connection builder
                sqlBuilder = new SqliteConnectionStringBuilder()
                {
                    DataSource = sqlConf["source"].GetString(),
                    Password = password?.Result.ToString(),
                    Pooling = true,
                    Mode = SqliteOpenMode.ReadWriteCreate
                };

                string connectionString = sqlBuilder.ToString();
                return () => new SqliteConnection(connectionString);
            }
            else if("mysql".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                sqlBuilder = new MySqlConnectionStringBuilder()
                {
                    Server = sqlConf["hostname"].GetString(),
                    Database = sqlConf["catalog"].GetString(),
                    UserID = sqlConf["username"].GetString(),
                    Password = password?.Result.ToString(),
                    Pooling = true,
                    LoadBalance = MySqlLoadBalance.LeastConnections,
                    MinimumPoolSize = sqlConf["min_pool_size"].GetUInt32(),
                };

                string connectionString = sqlBuilder.ToString();
                return () => new MySqlConnection(connectionString);
            }
            //Default to mssql
            else
            {
                //Use connection builder
                sqlBuilder = new SqlConnectionStringBuilder()
                {
                    DataSource = sqlConf["hostname"].GetString(),
                    UserID = sqlConf["username"].GetString(),
                    Password = password?.Result.ToString(),
                    InitialCatalog = sqlConf["catalog"].GetString(),
                    IntegratedSecurity = sqlConf["ms_security"].GetBoolean(),
                    Pooling = true,
                    MinPoolSize = sqlConf["min_pool_size"].GetInt32(),
                    Replication = true,
                    TrustServerCertificate = sqlConf["trust_cert"].GetBoolean(),
                };

                string connectionString = sqlBuilder.ToString();
                return () => new SqlConnection(connectionString);
            }
        }

        /// <summary>
        /// Gets (or loads) the ambient <see cref="DbContextOptions"/> configured from 
        /// the ambient sql factory and blocks the current thread until the options are ready
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="DbContextOptions"/> for the current plugin</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <remarks>If plugin is in debug mode, writes log data to the default log</remarks>
        public static DbContextOptions GetContextOptions(this PluginBase plugin)
        {
            //Get the async factory
            IAsyncLazy<DbContextOptions> async = plugin.GetContextOptionsAsync();

            //Block the current thread until the connection is ready
            return async.GetAwaiter().GetResult();
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
        public static IAsyncLazy<DbContextOptions> GetContextOptionsAsync(this PluginBase plugin)
        {
            static IAsyncLazy<DbContextOptions> LoadOptions(PluginBase plugin)
            {
                //Wrap in a lazy options
                return GetDbOptionsAsync(plugin).AsLazy();
            }

            plugin.ThrowIfUnloaded();
            return LoadingExtensions.GetOrCreateSingleton(plugin, LoadOptions);
        }

        private async static Task<DbContextOptions> GetDbOptionsAsync(PluginBase plugin)
        {
            try
            {
                //Get a db connection object, we must wait synchronously tho
                await using DbConnection connection = (await plugin.GetConnectionFactoryAsync()).Invoke();

                DbContextOptionsBuilder builder = new();

                //Determine connection type
                if (connection is SqlConnection sql)
                {
                    //Use sql server from connection 
                    builder.UseSqlServer(sql.ConnectionString);
                }
                else if (connection is SqliteConnection slc)
                {
                    builder.UseSqlite(slc.ConnectionString);
                }
                else if (connection is MySqlConnection msconn)
                {
                    //Detect version
                    ServerVersion version = ServerVersion.AutoDetect(msconn);

                    builder.UseMySql(msconn.ConnectionString, version);
                }

                //Enable logging
                if (plugin.IsDebug())
                {
                    builder.LogTo(plugin.Log.Debug);
                }

                //Get context and freez it before returning
                DbContextOptions options = builder.Options;
                options.Freeze();
                return options;
            }
            catch(Exception ex)
            {
                plugin.Log.Error(ex, "DBContext options load error");
                throw;
            }
        }

        /// <summary>
        /// Ensures the tables that back your desired DbContext exist within the configured database, 
        /// or creates them if needed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pbase"></param>
        /// <param name="state">The state object to pass to the <see cref="IDbTableDefinition.OnDatabaseCreating(IDbContextBuilder, object?)"/></param>
        /// <returns>A task that resolves when the tables have been created</returns>
        public static Task EnsureDbCreatedAsync<T>(this PluginBase pbase, object? state) where T : IDbTableDefinition, new()
        {
            T creator = new ();
            return EnsureDbCreatedAsync(pbase, creator, state);
        }

        /// <summary>
        /// Ensures the tables that back your desired DbContext exist within the configured database, 
        /// or creates them if needed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <param name="dbCreator">The instance of the <see cref="IDbTableDefinition"/> to build the database from</param>
        /// <param name="state">The state object to pass to the <see cref="IDbTableDefinition.OnDatabaseCreating(IDbContextBuilder, object?)"/></param>
        /// <returns>A task that resolves when the tables have been created</returns>
        public static async Task EnsureDbCreatedAsync<T>(this PluginBase plugin, T dbCreator, object? state) where T : IDbTableDefinition
        {
            DbBuilder builder = new();

            //Invoke ontbCreating to setup the dbBuilder
            dbCreator.OnDatabaseCreating(builder, state);

            //Wait for the connection factory to load
            Func<DbConnection> dbConFactory = await GetConnectionFactoryAsync(plugin);

            //Create a new db connection
            await using DbConnection connection = dbConFactory();

            //Get the abstract database from the connection type
            IDBCommandGenerator cb = connection.GetCmGenerator();

            //Compile the db command as a text Sql command
            string[] createComamnds = builder.BuildCreateCommand(cb);

            //begin connection
            await connection.OpenAsync(plugin.UnloadToken);

            //Transaction
            await using DbTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, plugin.UnloadToken);

            //Init new text command
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.Text;

            foreach (string createCmd in createComamnds)
            {
                if (plugin.IsDebug())
                {
                    plugin.Log.Debug("Creating new table for {type} with command\n{cmd}", typeof(T).Name, createCmd);
                }

                //Set the command, were not using parameters, so we dont need to clear anyting
                command.CommandText = createCmd;

                //Excute the command, it may return 0 if the table's already exist
                _ = await command.ExecuteNonQueryAsync(plugin.UnloadToken);
            }

            //Commit transaction now were complete
            await transaction.CommitAsync(plugin.UnloadToken);

            //All done!
            plugin.Log.Debug("Successfully created tables for {type}", typeof(T).Name);
        }
      
        #region ColumnExtensions

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
            //Set the max-length
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

        #endregion

        private static IDBCommandGenerator GetCmGenerator(this IDbConnection connection)
        {
            //Determine connection type
            if (connection is SqlConnection)
            {
                //Return the abstract db from the db command type
                return new MsSqlDb();
            }
            else if (connection is SqliteConnection)
            {
                return new SqlLiteDb();
            }
            else if (connection is MySqlConnection)
            {
                return new MySqlDb();
            }
            else
            {
                throw new NotSupportedException("This library does not support the abstract databse backend");
            }
        }

        internal static bool IsPrimaryKey(this DataColumn col) => col.Table!.PrimaryKey.Contains(col);

        /*
         * I am bypassing the DataColumn.MaxLength property because it does more validation
         * than we need against the type and can cause unecessary issues, so im just bypassing it 
         * for now
         */

        internal static void MaxLength(this DataColumn column, int length) 
        {
            column.ExtendedProperties[MAX_LEN_BYPASS_KEY] = length;
        }

        internal static int MaxLength(this DataColumn column)
        {
            return column.ExtendedProperties.ContainsKey(MAX_LEN_BYPASS_KEY)
                ? (int)column.ExtendedProperties[MAX_LEN_BYPASS_KEY]
                : column.MaxLength;
        }

        internal static void SetTimeStamp(this DataColumn column)
        {
            //We just need to set the key
            column.ExtendedProperties[TIMESTAMP_BYPASS] = null;
        }

        internal static bool IsTimeStamp(this DataColumn column)
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
