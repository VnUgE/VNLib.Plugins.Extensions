/*
* Copyright (c) 2022 Vaughn Nugent
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
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Plugins.Extensions.Loading.Events;

namespace VNLib.Plugins.Extensions.Loading.Routing
{
    /// <summary>
    /// Provides advanced QOL features to plugin loading
    /// </summary>
    public static class RoutingExtensions
    {
        private static readonly ConditionalWeakTable<IEndpoint, PluginBase?> _pluginRefs = new();

        /// <summary>
        /// Constructs and routes the specific endpoint type for the current plugin
        /// </summary>
        /// <typeparam name="T">The <see cref="IEndpoint"/> type</typeparam>
        /// <param name="plugin"></param>
        /// <param name="pluginConfigPathName">The path to the plugin sepcific configuration property</param>
        /// <exception cref="TargetInvocationException"></exception>
        public static T Route<T>(this PluginBase plugin, string? pluginConfigPathName) where T : IEndpoint
        {
            Type endpointType = typeof(T);
            //If the config attribute is not set, then ignore the config variables
            if (string.IsNullOrWhiteSpace(pluginConfigPathName))
            {
                ConstructorInfo? constructor = endpointType.GetConstructor(new Type[] { typeof(PluginBase) });
                _ = constructor ?? throw new EntryPointNotFoundException($"No constructor found for {endpointType.Name}");
                //Create the new endpoint and pass the plugin instance
                T endpoint = (T)constructor.Invoke(new object[] { plugin });
                //Register event handlers for the endpoint
                ScheduleIntervals(plugin, endpoint, endpointType, null);
                //Route the endpoint
                plugin.Route(endpoint);

                //Store ref to plugin for endpoint
                _pluginRefs.Add(endpoint, plugin);

                return endpoint;
            }
            else
            {
                ConstructorInfo? constructor = endpointType.GetConstructor(new Type[] { typeof(PluginBase), typeof(IReadOnlyDictionary<string, JsonElement>) });
                //Make sure the constructor exists
                _ = constructor ?? throw new EntryPointNotFoundException($"No constructor found for {endpointType.Name}");
                //Get config variables for the endpoint
                IReadOnlyDictionary<string, JsonElement> conf = plugin.GetConfig(pluginConfigPathName);
                //Create the new endpoint and pass the plugin instance along with the configuration object
                T endpoint = (T)constructor.Invoke(new object[] { plugin, conf });
                //Register event handlers for the endpoint
                ScheduleIntervals(plugin, endpoint, endpointType, conf);
                //Route the endpoint
                plugin.Route(endpoint);

                //Store ref to plugin for endpoint
                _pluginRefs.Add(endpoint, plugin);

                return endpoint;
            }
        }

        /// <summary>
        /// Constructs and routes the specific endpoint type for the current plugin
        /// </summary>
        /// <typeparam name="T">The <see cref="IEndpoint"/> type</typeparam>
        /// <param name="plugin"></param>
        /// <exception cref="TargetInvocationException"></exception>
        public static T Route<T>(this PluginBase plugin) where T : IEndpoint
        {
            Type endpointType = typeof(T);
            //Get config name attribute
            ConfigurationNameAttribute? configAttr = endpointType.GetCustomAttribute<ConfigurationNameAttribute>();
            //Route using attribute
            return plugin.Route<T>(configAttr?.ConfigVarName);
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

        private static void ScheduleIntervals<T>(PluginBase plugin, T endpointInstance, Type epType, IReadOnlyDictionary<string, JsonElement>? endpointLocalConfig) where T : IEndpoint
        {
            //Get all methods that have the configureable async interval attribute specified
            IEnumerable<Tuple<ConfigurableAsyncIntervalAttribute, AsyncSchedulableCallback>> confIntervals = epType.GetMethods()
                    .Where(m => m.GetCustomAttribute<ConfigurableAsyncIntervalAttribute>() != null)
                    .Select(m => new Tuple<ConfigurableAsyncIntervalAttribute, AsyncSchedulableCallback>
                    (m.GetCustomAttribute<ConfigurableAsyncIntervalAttribute>()!, m.CreateDelegate<AsyncSchedulableCallback>(endpointInstance)));

            //If the endpoint has a local config, then use it to find the interval
            if (endpointLocalConfig != null)
            {

                //Schedule event handlers on the current plugin
                foreach (Tuple<ConfigurableAsyncIntervalAttribute, AsyncSchedulableCallback> interval in confIntervals)
                {
                    int value = endpointLocalConfig[interval.Item1.IntervalPropertyName].GetInt32();
                    //Get the timeout from its resolution variable
                    TimeSpan timeout = interval.Item1.Resolution switch
                    {
                        IntervalResultionType.Seconds => TimeSpan.FromSeconds(value),
                        IntervalResultionType.Minutes => TimeSpan.FromMinutes(value),
                        IntervalResultionType.Hours => TimeSpan.FromHours(value),
                        _ => TimeSpan.FromMilliseconds(value),
                    };
                    //Schedule
                    plugin.ScheduleInterval(interval.Item2, timeout);
                }
            }

            //Get all methods that have the async interval attribute specified
            IEnumerable<Tuple<AsyncIntervalAttribute, AsyncSchedulableCallback>> intervals = epType.GetMethods()
                    .Where(m => m.GetCustomAttribute<AsyncIntervalAttribute>() != null)
                    .Select(m => new Tuple<AsyncIntervalAttribute, AsyncSchedulableCallback>(
                        m.GetCustomAttribute<AsyncIntervalAttribute>()!, m.CreateDelegate<AsyncSchedulableCallback>(endpointInstance))
                    );

            //Schedule event handlers on the current plugin
            foreach (Tuple<AsyncIntervalAttribute, AsyncSchedulableCallback> interval in intervals)
            {
                plugin.ScheduleInterval(interval.Item2, interval.Item1.Interval);
            }
        }
    }
}
