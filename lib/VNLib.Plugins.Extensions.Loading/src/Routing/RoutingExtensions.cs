﻿/*
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
using System.Threading.Tasks;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Plugins.Essentials.Runtime;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Extensions.Loading.Routing.Mvc;

namespace VNLib.Plugins.Extensions.Loading.Routing
{

    /// <summary>
    /// Provides advanced QOL features to plugin loading
    /// </summary>
    public static partial class RoutingExtensions
    {
        private static readonly ConditionalWeakTable<IEndpoint, PluginBase?> _pluginRefs = new();
        private static readonly ConditionalWeakTable<PluginBase, EndpointCollection> _pluginEndpoints = new();      

        /// <summary>
        /// Constructs and routes the specific endpoint type for the current plugin
        /// </summary>
        /// <typeparam name="T">The <see cref="IEndpoint"/> type</typeparam>
        /// <param name="plugin"></param>
        /// <exception cref="TargetInvocationException"></exception>
        public static T Route<T>(this PluginBase plugin) where T : IEndpoint
        {
            //Create the endpoint service, then route it
            T endpoint =  plugin.CreateService<T>();

            //Route the endpoint
            Route(plugin, endpoint);

            //Store ref to plugin for endpoint
            _pluginRefs.Add(endpoint, plugin);

            //Function that initalizes the endpoint's path and logging variables
            InitEndpointSettings(plugin, endpoint);

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

            //Get the endpoint collection for the current plugin
            EndpointCollection endpoints = _pluginEndpoints.GetValue(plugin, OnCreate);
            
            //Add the endpoint to the collection
            endpoints.Endpoints.Add(endpoint);
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

        [GeneratedRegex("{{(.*?)}}", RegexOptions.Compiled)]
        private static partial Regex ParserRegex();

        private static void InitEndpointSettings<T>(PluginBase plugin, T endpoint) where T : IEndpoint
        {
            //Load optional config
            IConfigScope config = plugin.GetConfigForType<T>();

            ILogProvider logger = plugin.Log;

            EndpointPathAttribute? pathAttr = typeof(T).GetCustomAttribute<EndpointPathAttribute>();

            /*
            * gets the protected function for assigning the endpoint path 
            * and logger instance.
            */
            InitFunc? initPathAndLog = ManagedLibrary.TryGetMethod<InitFunc>(endpoint, "InitPathAndLog", BindingFlags.NonPublic);

            if (pathAttr is null || initPathAndLog is null)
            {
                return;
            }

            string? logName = typeof(T).GetCustomAttribute<EndpointLogNameAttribute>()?.LogName;

            if (!string.IsNullOrWhiteSpace(logName))
            {
                logger = plugin.Log.CreateScope(SubsituteValue(logName, config));
            }
            try
            {

                //Invoke init function and pass in variable names
                initPathAndLog(
                    path: SubsituteValue(pathAttr.Path, config),
                    logger
                );
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch(Exception e)
            {
                throw new ConfigurationException($"Failed to initalize endpoint {endpoint.GetType().Name}", e);
            }

            static string SubsituteValue(string pathVar, IConfigScope? config)
            {
                if (config is null)
                {
                    return pathVar;
                }

                // Replace the matched pattern with the corresponding value from the dictionary
                return ConfigSyntaxParser.Replace(pathVar, match =>
                {
                    string varName = match.Groups[1].Value;

                    //Get the value from the config scope or return the original variable unmodified
                    return config.GetValueOrDefault(varName, varName);
                });
            }
        }

        private sealed class EndpointCollection : IVirtualEndpointDefinition
        {
            public List<IEndpoint> Endpoints { get; } = new();

            ///<inheritdoc/>
            IEnumerable<IEndpoint> IVirtualEndpointDefinition.GetEndpoints() => Endpoints;
        }


        private delegate ValueTask<VfReturnType> EndpointWorkFunc(HttpEntity entity);

        sealed record class HttpControllerEndpoint(MethodInfo MethodInfo, HttpEndpointAttribute Attr)
        {
            public string Path => Attr.Path;

            public HttpMethod Method => Attr.Method;

            public EndpointWorkFunc Func { get; } = MethodInfo.CreateDelegate<EndpointWorkFunc>();
        }

        private sealed class EndpointWrapper 
            : ResourceEndpointBase
        {

            private readonly FrozenDictionary<HttpMethod, EndpointWorkFunc> _wrappers;

            public EndpointWrapper(FrozenDictionary<HttpMethod, EndpointWorkFunc> table, string path, ILogProvider log)
            {
                _wrappers = table;
                InitPathAndLog(path, log);
            }

            protected override ValueTask<VfReturnType> OnProcessAsync(HttpEntity entity)
            {
                ref readonly EndpointWorkFunc func = ref _wrappers.GetValueRefOrNullRef(entity.Server.Method);

                if (Unsafe.IsNullRef(in func))
                {
                    return ValueTask.FromResult(VfReturnType.ProcessAsFile);
                }

                return func(entity);
            }
        }
    }
}
