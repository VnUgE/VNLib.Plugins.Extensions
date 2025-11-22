/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql.Tests
* File: TestConfigExtensions.cs 
*
* TestConfigExtensions.cs is part of VNLib.Plugins.Extensions.Loading.Sql.Tests which is part of 
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
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Text;
using System.Text.Json;

using VNLib.Plugins;
using VNLib.Plugins.Essentials.ServiceStack.Testing;

namespace VNLib.Plugins.Extensions.Loading.Sql.Tests.Config
{
    /// <summary>
    /// Provides extension methods for configuring the <see cref="TestPluginLoader{T}"/> 
    /// with plain C# objects that are serialized to JSON configuration data.
    /// </summary>
    internal static class TestConfigExtensions
    {
        /// <summary>
        /// Sets the host configuration using a plain C# object that will be serialized to JSON.
        /// </summary>
        /// <typeparam name="T">The plugin type</typeparam>
        /// <param name="loader">The test plugin loader instance</param>
        /// <param name="hostConfig">The host configuration object to serialize</param>
        /// <returns>The current loader instance for chaining</returns>
        public static TestPluginLoader<T> WithHostConfig<T>(this TestPluginLoader<T> loader, object hostConfig) 
            where T : class, IPlugin, new()
        {
            ArgumentNullException.ThrowIfNull(loader);
            ArgumentNullException.ThrowIfNull(hostConfig);

            byte[] configData = SerializeToJson(hostConfig);
            return loader.WithHostConfigData(configData);
        }

        /// <summary>
        /// Sets the plugin configuration using a plain C# object that will be serialized to JSON.
        /// </summary>
        /// <typeparam name="T">The plugin type</typeparam>
        /// <param name="loader">The test plugin loader instance</param>
        /// <param name="pluginConfig">The plugin configuration object to serialize</param>
        /// <returns>The current loader instance for chaining</returns>
        public static TestPluginLoader<T> WithPluginConfig<T>(this TestPluginLoader<T> loader, object pluginConfig) 
            where T : class, IPlugin, new()
        {
            ArgumentNullException.ThrowIfNull(loader);
            ArgumentNullException.ThrowIfNull(pluginConfig);

            byte[] configData = SerializeToJson(pluginConfig);
            return loader.WithPluginConfigData(configData);
        }

        private static byte[] SerializeToJson(object obj)
        {
            JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
            {
                WriteIndented           = false,
                AllowTrailingCommas     = true,
                PropertyNamingPolicy    = JsonNamingPolicy.SnakeCaseLower,
            };

            return JsonSerializer.SerializeToUtf8Bytes(obj, options);
        }
    }
}
