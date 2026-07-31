/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginHostServices.cs 
*
* PluginHostServices.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Loading
{
    using static PluginHostExtensions;

    public static class PluginHostServices
    {
        /// <summary>
        /// Gets the service export manager for the current host.
        /// </summary>
        /// <param name="host">The host container to retrieve services from.</param>
        /// <returns>A <see cref="HostServiceContainer"/> used to export services to the host application.</returns>
        public static HostServiceContainer Services(this in PluginHostContainer host)
            => new(host.Plugin);

        /// <summary>
        /// Initializes a new instance of the <see cref="HostServiceContainer"/> struct.
        /// </summary>
        /// <param name="plugin">The plugin instance associated with this service container.</param>
        public readonly ref struct HostServiceContainer(PluginBase plugin)
        {
            /// <summary>
            /// Exports a service of the specified type to the host application.
            /// </summary>
            /// <param name="serviceType">The type of the service to export.</param>
            /// <param name="instance">The service instance to export to the host.</param>
            /// <param name="flags">The optional export flags to pass to the host.</param>
            /// <returns>This <see cref="HostServiceContainer"/> for continued fluent chaining.</returns>
            /// <exception cref="ArgumentNullException">The <paramref name="serviceType"/> or <paramref name="instance"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            /// <remarks>
            /// <para>
            /// Avoid mutating the service instance after the plugin has been loaded,
            /// especially when using factory methods to create the service.
            /// </para>
            /// </remarks>
            public readonly HostServiceContainer Export(Type serviceType, object instance, ExportFlags flags = ExportFlags.None)
            {
                plugin.ThrowIfUnloaded();

                ArgumentNullException.ThrowIfNull(instance);
                ArgumentNullException.ThrowIfNull(serviceType);

                //Init new service wrapper
                ServiceExport export = new(serviceType, instance, flags);
                plugin.Services.Add(export);
                return this;
            }

            /// <summary>
            /// Exports a service of the specified generic type to the host application.
            /// </summary>
            /// <typeparam name="T">The type of the service to export.</typeparam>
            /// <param name="instance">The service instance to export to the host.</param>
            /// <param name="flags">The optional export flags to pass to the host.</param>
            /// <returns>This <see cref="HostServiceContainer"/> for continued fluent chaining.</returns>
            /// <exception cref="ArgumentNullException">The <paramref name="instance"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            /// <remarks>
            /// <para>
            /// Avoid mutating the service instance after the plugin has been loaded,
            /// especially when using factory methods to create the service.
            /// </para>
            /// </remarks>
            public readonly HostServiceContainer Export<T>(T instance, ExportFlags flags = ExportFlags.None)
                where T : class => Export(typeof(T), instance, flags);
    }
}
}

