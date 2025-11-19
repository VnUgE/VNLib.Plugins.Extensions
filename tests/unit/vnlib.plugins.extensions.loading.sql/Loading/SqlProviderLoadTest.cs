/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.Tests
* File: SqlProviderLoadTest.cs 
*
* SqlProviderLoadTest.cs is part of VNLib.Plugins.Extensions.Loading.Sql.Tests which is part of 
* the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program. If not, see https://www.gnu.org/licenses/.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Data.Common;
using System.IO;

using Microsoft.EntityFrameworkCore;

using VNLib.Plugins.Essentials.ServiceStack.Testing;
using VNLib.Plugins.Extensions.Loading.Sql.Tests.Config;

namespace VNLib.Plugins.Extensions.Loading.Sql.Tests.Loading
{ 

    [TestClass()]
    public class SqlProviderIntegrationTest
    {

        private static readonly object s_hostConfig = new
        {
            plugins = new
            {
                enabled     = true,
                hot_reload  = false,
                path        = AppContext.BaseDirectory,
                assets      = $"{AppContext.BaseDirectory}/assets",
            }
        };

        [TestMethod()]
        public void LoadSQLiteProvider()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"test-sqlite-{Guid.NewGuid()}.db");

            try
            {
                object pluginConfig = new
                {
                    debug   = true,
                    sql     = new
                    {
                        debug       = true,
                        provider    = "VNLib.Plugins.Extensions.Sql.SQLite.dll",
                        source      = dbPath,
                        pooling     = true,
                        mode        = 6, // ReadWriteCreate
                        cache       = 1  // Default cache mode
                    }
                };

                new TestPluginLoader<SqlProviderLoadTestPlugin>()
                    .WithCliArgs(["--verbose"])
                    .WithHostConfig(s_hostConfig)
                    .WithPluginConfig(pluginConfig)
                    .Load()
                    .GetServices(services =>
                    {
                        //Verify the integration service was exported
                        Assert.IsTrue(services.HasService<SqlProviderIntegrationService>());
                        Assert.AreEqual(1, services.Count);

                        SqlProviderIntegrationService? service = services.GetService<SqlProviderIntegrationService>();
                        Assert.IsNotNull(service);

                        //Test connection factory
                        using (DbConnection connection = service.CreateConnectionAsync().GetAwaiter().GetResult())
                        {
                            Assert.IsNotNull(connection);
                            Assert.IsInstanceOfType<Microsoft.Data.Sqlite.SqliteConnection>(connection);

                            //Verify connection opens successfully
                            connection.Open();

                            Assert.AreEqual(System.Data.ConnectionState.Open, connection.State);

                            connection.Close();
                        }                     

                        //Test context options
                        DbContextOptions options = service.GetDbContextOptionsAsync().GetAwaiter().GetResult();
                        Assert.IsNotNull(options);

                    })
                    .Unload(delayMilliseconds: 1000)
                    .TryDispose();
            }
            finally
            {
                //Cleanup test database
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
        }

        [TestMethod()]
        public void LoadMySQLProvider()
        {
            object pluginConfig = new
            {
                debug   = true,
                sql     = new
                {
                    debug           = true,
                    provider        = "VNLib.Plugins.Extensions.Sql.MySQL.dll",
                    hostname        = "localhost",
                    catalog         = "test_database",
                    username        = "test_user",
                    min_pool_size   = 5u,
                    max_pool_size   = 20u,
                    pooling         = true
                },

                secrets = new
                {
                    db_password = "test_password"
                }
            };

            new TestPluginLoader<SqlProviderLoadTestPlugin>()
                .WithCliArgs(["--verbose"])
                .WithHostConfig(s_hostConfig)
                .WithPluginConfig(pluginConfig)
                .Load()
                .GetServices(services =>
                {
                    //Verify the integration service was exported
                    Assert.IsTrue(services.HasService<SqlProviderIntegrationService>());
                    Assert.AreEqual(1, services.Count);

                    SqlProviderIntegrationService? service = services.GetService<SqlProviderIntegrationService>();
                    Assert.IsNotNull(service);

                    //Test connection factory creation (don't open connection, as MySQL may not be available)
                    using (DbConnection connection = service.CreateConnectionAsync().GetAwaiter().GetResult())
                    {
                        Assert.IsNotNull(connection);
                        Assert.IsInstanceOfType<MySqlConnector.MySqlConnection>(connection);
                    }

                    //FIXME: Mysql attempts to connect to the server to get version information when
                    // getting context options, which can't be tested here in uint testing.

                    //DbContextOptions options = service.GetDbContextOptionsAsync().GetAwaiter().GetResult();
                    //Assert.IsNotNull(options);
                })
                .Unload(delayMilliseconds: 1000)
                .TryDispose();
        }

        [TestMethod()]
        public void LoadSqlServerProvider()
        {
            object pluginConfig = new
            {
                debug   = true,
                sql     = new
                {
                    debug           = true,
                    provider        = "VNLib.Plugins.Extensions.Sql.SqlServer.dll",
                    hostname        = "test_server",
                    catalog         = "test_database",
                    username        = "test_user",
                    password        = "test_password",
                    pooling         = true,
                    min_pool_size   = 0,
                    max_pool_size   = 100,
                    connect_timeout = 15,
                    encrypt         = false
                },

                secrets = new
                {
                    db_password = "test_password"
                }
            };

            new TestPluginLoader<SqlProviderLoadTestPlugin>()
                .WithCliArgs(["--verbose"])
                .WithHostConfig(s_hostConfig)
                .WithPluginConfig(pluginConfig)
                .Load()
                .GetServices(services =>
                {
                    //Verify the integration service was exported
                    Assert.IsTrue(services.HasService<SqlProviderIntegrationService>());
                    Assert.AreEqual(1, services.Count);

                    SqlProviderIntegrationService? service = services.GetService<SqlProviderIntegrationService>();
                    Assert.IsNotNull(service);

                    //Test connection factory creation (don't open connection, as SQL Server may not be available)
                    using (DbConnection connection = service.CreateConnectionAsync().GetAwaiter().GetResult())
                    {
                        Assert.IsNotNull(connection);
                        Assert.IsInstanceOfType<Microsoft.Data.SqlClient.SqlConnection>(connection);
                    }

                    //Test context options
                    DbContextOptions options = service.GetDbContextOptionsAsync().GetAwaiter().GetResult();
                    Assert.IsNotNull(options);
                })
                .Unload(delayMilliseconds: 1000)
                .TryDispose();
        }
    }
}
