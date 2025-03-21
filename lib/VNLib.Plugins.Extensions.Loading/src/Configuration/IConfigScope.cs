/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IConfigScope.cs 
*
* IConfigScope.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Text.Json;
using System.Collections.Generic;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// A top-level scoped configuration element
    /// </summary>
    public interface IConfigScope : IReadOnlyDictionary<string, JsonElement>
    {
        /// <summary>
        /// The root level name of the configuration element
        /// </summary>
        string ScopeName { get; }

        /// <summary>
        /// Json deserialzes the current config scope to the desired type
        /// </summary>
        /// <typeparam name="T">The type to deserialze the current config to</typeparam>
        /// <returns>The instance created from the current scope</returns>
        T Deserialize<T>();

        /// <summary>
        /// Json deserialzes the current config scope to the desired type
        /// </summary>
        /// <typeparam name="T">The type to deserialze the current config to</typeparam>
        /// <returns>The instance created from the current scope</returns>
        [Obsolete("Use the correct spelling of Deserialize")]
        public virtual T Deserialze<T>() => Deserialize<T>();
    }
}
