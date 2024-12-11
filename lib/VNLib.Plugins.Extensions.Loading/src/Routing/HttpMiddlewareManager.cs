/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: HttpMiddlewareManager.cs 
*
* HttpMiddlewareManager.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Extensions.Loading.Routing
{

    // Not necessary since a new structure is created on demand, there is no reason for equality checks
#pragma warning disable CA1815 // Override equals and operator equals on value types

    /// <summary>
    /// Provides a manager for http middleware
    /// </summary>
    /// <param name="plugin">The plugin instance to manage middleware for</param>
    public readonly struct HttpMiddlewareManager(PluginBase plugin)
    {
        private static readonly ConditionalWeakTable<PluginBase, List<IHttpMiddleware>> _pluginMiddlewareList = [];

        /*
         * The runtime accepts an enumeration of IHttpMiddleware instances, so 
         * a list can just be exported as an enumerable instance
         */
        private static List<IHttpMiddleware> OnCreate(PluginBase plugin)
        {
            List<IHttpMiddleware> collection = new(capacity: 1);
            plugin.ExportService<IEnumerable<IHttpMiddleware>>(collection);
            return collection;
        }

        /// <summary>
        /// Exports a single middlware instance to the collection for the plugin. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instances">A params array of middleware instances to export to the plugin</param>
        /// <remarks>
        /// WARNING: Adding middleware arrays explicitly to the plugin service pool will override
        /// this function. All instances must be exposed though this function
        /// </remarks>
        public readonly void Add<T>(params T[] instances) where T : IHttpMiddleware
        {
            List<IHttpMiddleware> middlewares = _pluginMiddlewareList.GetValue(plugin, OnCreate);

            //Add the endpoint to the collection
            instances.ForEach(mw => middlewares.Add(mw));
        }
    }

#pragma warning restore CA1815 // Override equals and operator equals on value types
}
