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

using System;

using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Extensions.Loading.Routing
{

    /// <summary>
    /// Provides helper extensions for http middleware
    /// </summary>
    public static class MiddlewareHelpers
    {
        /// <summary>
        /// Gets the http middleware manager for the plugin
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The HttpMiddlewareManager structure</returns>
        public static HttpMiddlewareManager Middleware(this PluginBase plugin) => new(plugin);

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
        [Obsolete("Use Middleware() extension helper instead")]
        public static void ExportMiddleware<T>(this PluginBase plugin, params T[] instances) where T : IHttpMiddleware
        {
            Middleware(plugin)
                .Add(instances);
        }

    }
}
