/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;

namespace VNLib.Plugins.Extensions.Loading.Sql
{

    /// <summary>
    /// Provides common basic SQL loading extensions for plugins
    /// </summary>
    public static class SqlDbConnectionLoader
    {
        public const string SQL_CONFIG_KEY = "sql";
        public const string SQL_PROVIDER_DLL_KEY = "provider";

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
            IRuntimeDbProvider provider = plugin.GetDbProvider();
            return provider.GetDbConnectionAsync().AsLazy();
        }

        private static IRuntimeDbProvider GetDbProvider(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            return LoadingExtensions.GetOrCreateSingleton(plugin, LoadDbProvider);
        }

        private static IRuntimeDbProvider LoadDbProvider(PluginBase plugin)
        {
            //Get the sql configuration scope
            IConfigScope sqlConf = plugin.Config().Get(SQL_CONFIG_KEY);

            //Get the provider dll path
            string dllPath = sqlConf.GetRequiredProperty(SQL_PROVIDER_DLL_KEY, k => k.GetString()!);

            /*
             * I am loading a bare object here and dynamically resolving the required methods
             * insead of forcing a shared interface. This allows the external library to be
             * more flexible and slimmer.
             */
            return plugin.CreateServiceExternal<IRuntimeDbProvider>(dllPath);
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
            IRuntimeDbProvider provider = plugin.GetDbProvider();
            return provider.GetDbOptionsAsync().AsLazy();
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
            ArgumentNullException.ThrowIfNull(plugin);
            ArgumentNullException.ThrowIfNull(dbCreator);

            DbBuilder builder = new();

            //Invoke onDbCreating to setup the dbBuilder and table's for the context
            dbCreator.OnDatabaseCreating(builder, state);

            //Get the abstract database from the connection type
            IRuntimeDbProvider dbp = plugin.GetDbProvider();
            IDBCommandGenerator cb = dbp.GetCommandGenerator();

            //Compile the db command as a text Sql command
            string[] createComands = builder.BuildCreateCommand(cb);

            //Wait for the connection factory to load
            Func<DbConnection> dbConFactory = await dbp.GetDbConnectionAsync();

            //Create a new db connection
            await using DbConnection connection = dbConFactory();

            //begin connection
            await connection.OpenAsync(plugin.UnloadToken);

            //Transaction
            await using DbTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, plugin.UnloadToken);

            //Init new text command
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.Text;

            foreach (string createCmd in createComands)
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

        /// <summary>
        /// A helper method to define a table for a <see cref="IDbContextBuilder"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="tableName">The optional name of the table to create</param>
        /// <param name="callback">The table creation callback function</param>
        /// <returns>The original context builder instance</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IDbContextBuilder DefineTable<T>(this IDbContextBuilder builder, string tableName, Action<IDbTableBuilder<T>> callback)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(callback);

            callback(builder.DefineTable<T>(tableName));
            return builder;
        }

        /// <summary>
        /// A helper method to define a table for a <see cref="IDbContextBuilder"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="callback">The table creation callback function</param>
        /// <returns>The original context builder instance</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IDbContextBuilder DefineTable<T>(this IDbContextBuilder builder, Action<IDbTableBuilder<T>> callback)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(callback);

            callback(builder.DefineTable<T>());
            return builder;
        }
    }
}
