/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: MiddlewareHelpers.cs 
*
* MiddlewareHelpers.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
    /// <summary>
    /// Provides helper extensions for http middleware
    /// </summary>
    public static class MiddlewareHelpers
    {
        private static readonly ConditionalWeakTable<PluginBase, List<IHttpMiddleware>> _pluginMiddlewareList = new();

        /// <summary>
        /// Exports a single middlware instance to the collection for the plugin. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <param name="instances">A params array of middleware instances to export to the plugin</param>
        /// <remarks>
        /// WARNING: Adding middleware arrays explicitly to the plugin service pool will override
        /// this function. All instances must be exposed though this function
        /// </remarks>
        public static void ExportMiddleware<T>(this PluginBase plugin, params T[] instances) where T : IHttpMiddleware
        {
            /*
              * The runtime accepts an enumeration of IHttpMiddleware instances, so 
              * a list can just be exported as an enumerable instance
              */
            static List<IHttpMiddleware> OnCreate(PluginBase plugin)
            {
                List<IHttpMiddleware> collection = new(1);
                plugin.ExportService<IEnumerable<IHttpMiddleware>>(collection);
                return collection;
            }

            //Get the endpoint collection for the current plugin
            List<IHttpMiddleware> middlewares = _pluginMiddlewareList.GetValue(plugin, OnCreate);

            //Add the endpoint to the collection
            instances.ForEach(mw => middlewares.Add(mw));
        }
    }
}
