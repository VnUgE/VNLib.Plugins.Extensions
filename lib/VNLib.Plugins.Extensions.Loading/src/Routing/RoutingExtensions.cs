/*
* Copyright (c) 2026 Vaughn Nugent
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
using VNLib.Plugins.Extensions.Loading.Configuration;

// TODO: TEMPORARY v0.2.0
using VNLib.Plugins.Essentials.Runtime;

namespace VNLib.Plugins.Extensions.Loading.Routing
{
    using static PluginHostExtensions;

    /// <summary>
    /// Provides advanced quality-of-life features for plugin loading.
    /// </summary>
    public static partial class RoutingExtensions
        {
        private static readonly ConditionalWeakTable<IEndpoint, PluginBase?> _pluginRefs = [];

        [GeneratedRegex("{{\\s*(.*?)\\s*}}", RegexOptions.Compiled)]
        private static partial Regex ParserRegex();
        private static readonly Regex ConfigSyntaxParser = ParserRegex();

        /// <summary>
        /// Creates a new <see cref="EndpointRouter"/> for the current plugin.
        /// </summary>
        /// <param name="host">The plugin host container for the current plugin.</param>
        /// <returns>A new <see cref="EndpointRouter"/> for the current plugin.</returns>
        /// <remarks>
        /// The router enables advanced features such as automatic path and logger configuration
        /// based on attributes and config values.
        /// </remarks>
        public static EndpointRouter Routes(this in PluginHostContainer host) => new(host.Plugin);

        /// <summary>
        /// Gets the plugin that loaded the current endpoint.
        /// </summary>
        /// <param name="ep">The endpoint to get the plugin for.</param>
        /// <returns>The plugin that loaded the current endpoint.</returns>
        /// <exception cref="InvalidOperationException">The endpoint was not dynamically routed.</exception>
        public static PluginBase GetPlugin(this IEndpoint ep)
        {
            _ = _pluginRefs.TryGetValue(ep, out PluginBase? pBase);
            return pBase ?? throw new InvalidOperationException("Endpoint was not dynamically routed");
        }

        /// <summary>
        /// Constructs and routes the specific endpoint type for the current plugin.
        /// </summary>
        /// <typeparam name="T">The <see cref="IEndpoint"/> type to route.</typeparam>
        /// <param name="plugin">The plugin for which to create the endpoint.</param>
        /// <returns>The routed endpoint.</returns>
        /// <remarks>
        /// This method is a convenience method that wraps the <see cref="EndpointRouter"/> functionality.
        /// </remarks>
        [Obsolete("Prefer using plugin.Host().Routes().Add<T>() instead.")]
        public static T Route<T>(this PluginBase plugin) where T : IEndpoint
            => plugin.Host().Routes().Add<T>();

        /// <summary>
        /// Routes a single endpoint for the current plugin and exports the collection to the service pool.
        /// </summary>
        /// <param name="plugin">The plugin for which to route the endpoint.</param>
        /// <param name="endpoint">The endpoint to route.</param>
        [Obsolete("Prefer using plugin.Host().Routes().Add(endpoint) instead.")]
        public static void Route(this PluginBase plugin, IEndpoint endpoint)
            => plugin.Host().Routes().Add(endpoint);

        [return: NotNullIfNotNull(nameof(@default))]
        internal static string? SubstituteConfigStringValue(IConfigScope? config, string pathVar, string? @default)
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

                string scopeName = SubstituteConfigStringValue(config, attr.LogName, attr.DefaultName);
                logger = plugin.Log.CreateScope(scopeName);
            }

            return logger;
        }

        /// <summary>
        /// Provides advanced routing features for endpoints.
        /// </summary>
        /// <param name="plugin">The plugin for which routes will be registered.</param>
        /// <remarks>
        /// Supports automatic path and logger configuration based on attributes and config values.
        /// </remarks>
        public readonly ref struct EndpointRouter(PluginBase plugin)
        {
            private static readonly ConditionalWeakTable<PluginBase, EndpointCollection> _pluginEndpoints = [];

            /// <summary>
            /// Gets the plugin for which the current router is routing endpoints.
            /// </summary>
            public readonly PluginBase Plugin => plugin;

            /// <summary>
            /// Routes a single endpoint for the current plugin and exports the collection to the service pool.
            /// </summary>
            /// <param name="endpoint">The endpoint to add to the collection.</param>
            public readonly void Add(IEndpoint endpoint)
            {
                ArgumentNullException.ThrowIfNull(endpoint);

                ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Path);

                if (!Regex.IsMatch(endpoint.Path, @"^\/\S*$"))
                {
                    throw new ArgumentException($"Endpoint path '{endpoint.Path}' is not a valid path. It must start with a '/' and contain no whitespace.", nameof(endpoint));
                }

                //Get the endpoint collection for the current plugin
                _pluginEndpoints
                    .GetValue(plugin, OnCreate)
                    .Endpoints
                    .Add(endpoint.Path, endpoint);

                /*
                * Export the new collection to the service pool in the constructor
                * function to ensure it's only exported once per plugin
                */
                static EndpointCollection OnCreate(PluginBase plugin)
                {
                    EndpointCollection collection = new();

                    plugin.Host()
                        .Services()
                        .Export<IVirtualEndpointDefinition>(collection);

                    return collection;
                }
            }

            /// <summary>
            /// Constructs and routes the specific endpoint type for the current plugin.
            /// </summary>
            /// <typeparam name="T">The <see cref="IEndpoint"/> type to route.</typeparam>
            /// <exception cref="TargetInvocationException">The endpoint constructor throws an exception.</exception>
            public readonly T Add<T>() where T : IEndpoint
            {
                //Create the endpoint service, then route it
                T endpoint = plugin.Deps().Create<T>();

                //Function that initializes the endpoint's path and logging variables
                InitEndpointSettings(plugin, endpoint);

                Add(endpoint);

                //Store ref to plugin for endpoint
                _pluginRefs.Add(endpoint, plugin);

                return endpoint;
            }

            private delegate void InitFunc(string path, ILogProvider log);

            private static void InitEndpointSettings<T>(PluginBase plugin, T endpoint) where T : IEndpoint
            {
                Type endpointType = endpoint.GetType();

                //Load optional config
                IConfigScope? config = plugin.Config().TryGetForType<T>();

                EndpointPathAttribute? pathAttr = endpointType.GetCustomAttribute<EndpointPathAttribute>();

                /*
                 * gets the protected function for assigning the endpoint path
                 * and logger instance.
                 */
                InitFunc? initPathAndLog = ManagedLibrary.TryGetMethod<InitFunc>(
                    endpoint,
                    methodName: "InitEndpoint",     // NOTE! must match ResourceEndpointBase.InitEndpoint signature
                    BindingFlags.NonPublic          // It's an internal method
                );

                // If the method is not defined, or the attribute is not defined, then skip the
                // initialization step and assume the endpoint will handle it itself
                if (pathAttr is null || initPathAndLog is null)
                {
                    return;
                }

                ILogProvider logger = ConfigureLogger<T>(plugin, config);

                try
                {
                    string? endpointPath = SubstituteConfigStringValue(config, pathAttr.Path, @default: null);
                    Validate.NotNull(endpointPath, $"Endpoint '{endpointType.Name}' pathname is null or an empty string '{endpointPath}'");
                    Validate.Matches(
                        endpointPath,
                        pattern: @"^\/\S*$",
                        message: $"Endpoint '{endpointType.Name}' path '{endpointPath}' is not a valid path. It must start with a '/' and contain no whitespace."
                    );

                    //Invoke init function and pass in variable names
                    initPathAndLog(endpointPath, logger);
                }
                catch (ConfigurationException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new ConfigurationException($"Failed to initialize endpoint {endpointType.Name}", e);
                }
            }

            private sealed class EndpointCollection : IVirtualEndpointDefinition
            {
                public Dictionary<string, IEndpoint> Endpoints { get; } = [];

                ///<inheritdoc/>
                IEnumerable<IEndpoint> IVirtualEndpointDefinition.GetEndpoints() => Endpoints.Values;
        }
    }
}
}
