/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginHostExtensions.cs 
*
* PluginHostExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
    public static class PluginHostExtensions
    {
        /// <summary>
        /// Creates a <see cref="PluginHostContainer"/> from a <see cref="PluginBase"/> instance.
        /// </summary>
        /// <param name="plugin">The plugin instance to wrap in a <see cref="PluginHostContainer"/>.</param>
        /// <returns>A new <see cref="PluginHostContainer"/> wrapping the provided plugin instance.</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="plugin"/> is <see langword="null"/>.</exception>
        public static PluginHostContainer Host(this PluginBase plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            return new(plugin);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginHostContainer"/> struct.
        /// </summary>
        /// <param name="plugin">The plugin instance to wrap.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="plugin"/> is <see langword="null"/>.</exception>
        public readonly ref struct PluginHostContainer(PluginBase plugin)
        {
            /// <summary>
            /// Gets the underlying plugin instance.
            /// </summary>
            public readonly PluginBase Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
    }   
}

