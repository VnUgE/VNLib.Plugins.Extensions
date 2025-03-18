/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: RoutingExtensions.cs 
*
* RoutingExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Plugins.Essentials.Runtime;
using VNLib.Plugins.Extensions.Loading.Configuration;

namespace VNLib.Plugins.Extensions.Loading.Routing
{

    /// <summary>
    /// Provides advanced QOL features to plugin loading
    /// </summary>
    public static partial class RoutingExtensions
    {
        private static readonly ConditionalWeakTable<IEndpoint, PluginBase?> _pluginRefs = [];
        private static readonly ConditionalWeakTable<PluginBase, EndpointCollection> _pluginEndpoints = [];

        /// <summary>
        /// Constructs and routes the specific endpoint type for the current plugin
        /// </summary>
        /// <typeparam name="T">The <see cref="IEndpoint"/> type</typeparam>
        /// <param name="plugin"></param>
        /// <exception cref="TargetInvocationException"></exception>
        public static T Route<T>(this PluginBase plugin) where T : IEndpoint
        {
            //Create the endpoint service, then route it
            T endpoint = plugin.CreateService<T>();

            //Function that initalizes the endpoint's path and logging variables
            InitEndpointSettings(plugin, endpoint);

            Route(plugin, endpoint);

            //Store ref to plugin for endpoint
            _pluginRefs.Add(endpoint, plugin);

            return endpoint;
        }

        /// <summary>
        /// Routes a single endpoint for the current plugin and exports the collection to the 
        /// service pool
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="endpoint">The endpoint to add to the collection</param>
        public static void Route(this PluginBase plugin, IEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            if (string.IsNullOrWhiteSpace(endpoint.Path))
            {
                throw new ArgumentException($"Endpoint '{endpoint.GetType().Name}' pathname is null or an empty string '{endpoint.Path}'");
            }

            if (!endpoint.Path.StartsWith('/'))
            {
                throw new ArgumentException($"Endpoint '{endpoint.GetType().Name}' path must start with a '/'");
            }

            //Get the endpoint collection for the current plugin
            _pluginEndpoints
                .GetValue(plugin, OnCreate)
                .Endpoints
                .Add(endpoint.Path, endpoint);

            /*
            * Export the new collection to the service pool in the constructor
            * function to ensure it's only export once per plugin
            */
            static EndpointCollection OnCreate(PluginBase plugin)
            {
                EndpointCollection collection = new();
                plugin.ExportService<IVirtualEndpointDefinition>(collection);
                return collection;
            }
        }

        /// <summary>
        /// Gets the plugin that loaded the current endpoint
        /// </summary>
        /// <param name="ep"></param>
        /// <returns>The plugin that loaded the current endpoint</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static PluginBase GetPlugin(this IEndpoint ep)
        {
            _ = _pluginRefs.TryGetValue(ep, out PluginBase? pBase);
            return pBase ?? throw new InvalidOperationException("Endpoint was not dynamically routed");
        }

        private static readonly Regex ConfigSyntaxParser = ParserRegex();
        private delegate void InitFunc(string path, ILogProvider log);

        [GeneratedRegex("{{\\s*(.*?)\\s*}}", RegexOptions.Compiled)]
        private static partial Regex ParserRegex();

        private static void InitEndpointSettings<T>(PluginBase plugin, T endpoint) where T : IEndpoint
        {
            //Load optional config
            IConfigScope? config = plugin.TryGetConfigForType<T>();

            EndpointPathAttribute? pathAttr = typeof(T).GetCustomAttribute<EndpointPathAttribute>();

            /*
            * gets the protected function for assigning the endpoint path 
            * and logger instance.
            */
            InitFunc? initPathAndLog = ManagedLibrary.TryGetMethod<InitFunc>(
                endpoint,
                methodName: "InitPathAndLog",
                BindingFlags.NonPublic
            );

            if (pathAttr is null || initPathAndLog is null)
            {
                return;
            }

            ILogProvider logger = ConfigureLogger<T>(plugin, config);

            try
            {
                string? endpointPath = SubsituteConfigStringValue(config, pathAttr.Path, @default: null);
                Validate.NotNull(endpointPath, $"Endpoint '{endpoint.GetType().Name}' pathname is null or an empty string '{endpointPath}'");
                Validate.Assert(endpointPath.StartsWith('/'), $"Endpoint '{endpoint.GetType().Name}' path must start with a '/'");

                //Invoke init function and pass in variable names
                initPathAndLog(endpointPath, logger);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ConfigurationException($"Failed to initalize endpoint {endpoint.GetType().Name}", e);
            }
        }

        [return: NotNullIfNotNull(nameof(@default))]
        internal static string? SubsituteConfigStringValue(IConfigScope? config, string pathVar, string? @default)
        {
            if (config is null)
            {
                return @default;
            }

            // Replace the matched pattern with the corresponding value from the configuration
            return ConfigSyntaxParser.Replace(pathVar, match =>
            {
                string varName = match.Groups[1].Value;

                if (@default is null)
                {
                    //If no default value is provided, throw an exception if the variable is not found
                    return config.GetRequiredProperty(varName, static p => p.GetString()!);
                }
                else
                {
                    //If a default value is provided, return the default value if the variable is not found
                    return config.GetValueOrDefault(varName, @default);
                }
            });
        }

        internal static ILogProvider ConfigureLogger<T>(PluginBase plugin, IConfigScope? config)
        {
            Type t = typeof(T);
            ILogProvider logger = plugin.Log;

            EndpointLogNameAttribute? attr = t.GetCustomAttribute<EndpointLogNameAttribute>();
            if (!string.IsNullOrWhiteSpace(attr?.LogName))
            {
                attr.DefaultName ??= t.Name;

                string scopeName = SubsituteConfigStringValue(config, attr.LogName, attr.DefaultName);
                logger = plugin.Log.CreateScope(scopeName);
            }

            return logger;
        }

        private sealed class EndpointCollection : IVirtualEndpointDefinition
        {
            public Dictionary<string, IEndpoint> Endpoints { get; } = [];

            ///<inheritdoc/>
            IEnumerable<IEndpoint> IVirtualEndpointDefinition.GetEndpoints() => Endpoints.Values;
        }
    }
}
