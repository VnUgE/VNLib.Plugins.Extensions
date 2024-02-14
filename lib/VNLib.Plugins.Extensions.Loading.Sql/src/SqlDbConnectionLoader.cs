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
using System.Linq;
using System.Data;
using System.Text;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
        public const string SQL_PROVIDER_DLL_KEY = "provider";     

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
            plugin.ThrowIfUnloaded();

            //Get the provider singleton
            DbProvider provider = LoadingExtensions.GetOrCreateSingleton(plugin, GetDbPovider);

            return provider.ConnectionFactory.Value.AsLazy();
        }

        private static DbProvider GetDbPovider(PluginBase plugin)
        {
            //Get the sql configuration scope
            IConfigScope sqlConf = plugin.GetConfig(SQL_CONFIG_KEY);
         
            //Get the provider dll path
            string dllPath = sqlConf.GetRequiredProperty(SQL_PROVIDER_DLL_KEY, k => k.GetString()!);
           
            /*
             * I am loading a bare object here and dynamically resolbing the required methods
             * insead of forcing a shared interface. This allows the external library to be
             * more flexible and slimmer.
             */
            object instance = plugin.CreateServiceExternal<object>(dllPath);

            return new(instance, sqlConf);
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
            plugin.ThrowIfUnloaded();

            //Get the provider singleton
            DbProvider provider = LoadingExtensions.GetOrCreateSingleton(plugin, GetDbPovider);

            return provider.OptionsFactory.Value.AsLazy();
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

            //Invoke ontbCreating to setup the dbBuilder
            dbCreator.OnDatabaseCreating(builder, state);

            //Get the abstract database from the connection type
            IDBCommandGenerator cb = GetCmdGenerator(plugin);

            //Wait for the connection factory to load
            Func<DbConnection> dbConFactory = await GetConnectionFactoryAsync(plugin);

            //Create a new db connection
            await using DbConnection connection = dbConFactory();

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

        private static IDBCommandGenerator GetCmdGenerator(PluginBase plugin)
        {
            //Get the provider singleton
            DbProvider provider = LoadingExtensions.GetOrCreateSingleton(plugin, GetDbPovider);

            //See if the provider has a command builder function, otherwise try to use known defaults
            if (provider.HasCommandBuilder)
            {
                return provider.CommandGenerator;
            }
            else if (string.Equals(provider.ProviderName, "sqlserver", StringComparison.OrdinalIgnoreCase))
            {
                return new MsSqlDb();
            }
            else if (string.Equals(provider.ProviderName, "mysql", StringComparison.OrdinalIgnoreCase))
            {
                return new MySqlDb();
            }
            else if (string.Equals(provider.ProviderName, "sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return new SqlLiteDb();
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

        internal sealed class DbProvider(object instance, IConfigScope sqlConfig)
        {
            public delegate Task<Func<DbConnection>> AsynConBuilderDelegate(IConfigScope sqlConf);
            public delegate Func<DbConnection> SyncConBuilderDelegate(IConfigScope sqlConf);
            public delegate DbContextOptions SyncOptBuilderDelegate(IConfigScope sqlConf);
            public delegate Task<DbContextOptions> AsynOptBuilderDelegate(IConfigScope sqlConf);
            public delegate void BuildTableStringDelegate(StringBuilder builder, DataTable table);
            public delegate string ProviderNameDelegate();
            

            public object Provider { get; } = instance;

            public IConfigScope SqlConfig { get; } = sqlConfig;

            /// <summary>
            /// A lazy async connection factory. When called, may cause invocation in the external library, 
            /// but only once.
            /// </summary>
            public readonly Lazy<Task<Func<DbConnection>>> ConnectionFactory = new(() => GetConnections(instance, sqlConfig));

            /// <summary>
            /// A lazy async options factory. When called, may cause invocation in the external library,
            /// but only once.
            /// </summary>
            public readonly Lazy<Task<DbContextOptions>> OptionsFactory = new(() => GetOptions(instance, sqlConfig));

            /// <summary>
            /// Gets the extern command generator for the external library
            /// </summary>
            public readonly IDBCommandGenerator CommandGenerator = new ExternCommandGenerator(instance);

            /// <summary>
            /// Gets the provider name from the external library
            /// </summary>
            public readonly ProviderNameDelegate ProviderNameFunc = ManagedLibrary.GetMethod<ProviderNameDelegate>(instance, "GetProviderName");

            /// <summary>
            /// Gets a value indicating if the external library has a command builder
            /// </summary>
            public bool HasCommandBuilder => (CommandGenerator as ExternCommandGenerator)!.BuildTableString is not null;

            /// <summary>
            /// Gets the provider name from the external library
            /// </summary>
            public string ProviderName => ProviderNameFunc.Invoke();

            /*
             * Methods below are designed to be called within a lazy/defered context and possible awaited
             * by mutliple threads. This causes data to be only loaded once, and then cached for future calls.
             */

            private static Task<Func<DbConnection>> GetConnections(object instance, IConfigScope sqlConfig)
            {
                //Connection builder functions
                SyncConBuilderDelegate? SyncBuilder = ManagedLibrary.TryGetMethod<SyncConBuilderDelegate>(instance, "GetDbConnection");

                //try sync first
                if (SyncBuilder is not null)
                {
                    return Task.FromResult(SyncBuilder.Invoke(sqlConfig));
                }

                //If no sync function force call async, but try to schedule it on a new thread
                AsynConBuilderDelegate? AsynConnectionBuilder = ManagedLibrary.GetMethod<AsynConBuilderDelegate>(instance, "GetDbConnectionAsync");
                return Task.Run(() => AsynConnectionBuilder.Invoke(sqlConfig));
            }

            private static Task<DbContextOptions> GetOptions(object instance, IConfigScope sqlConfig)
            {
                //Options builder functions
                SyncOptBuilderDelegate? SyncBuilder = ManagedLibrary.TryGetMethod<SyncOptBuilderDelegate>(instance, "GetDbOptions");

                //try sync first
                if (SyncBuilder is not null)
                {
                    return Task.FromResult(SyncBuilder.Invoke(sqlConfig));
                }

                //If no sync function force call async, but try to schedule it on a new thread
                AsynOptBuilderDelegate? AsynOptionsBuilder = ManagedLibrary.GetMethod<AsynOptBuilderDelegate>(instance, "GetDbOptionsAsync");
                return Task.Run(() => AsynOptionsBuilder.Invoke(sqlConfig));
            }

            private sealed class ExternCommandGenerator(object instance) : IDBCommandGenerator
            {
                public BuildTableStringDelegate? BuildTableString = ManagedLibrary.TryGetMethod<BuildTableStringDelegate>(instance, "BuildCreateStatment");
                

                public void BuildCreateStatment(StringBuilder builder, DataTable table)
                {
                    if(BuildTableString is not null)
                    {
                        BuildTableString.Invoke(builder, table);
                    }
                    else
                    {
                        throw new NotSupportedException("The external library does not support table creation");
                    }
                }
            }
        }
    }
}
