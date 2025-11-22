/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ConfigurationNameAttribute.cs 
*
* ConfigurationNameAttribute.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

/*
 *   TODO:
 *     This class was originally exposed in the VNLib.Plugins.Extensions.Loading
 *     even though the file has been moved to the Configuration directory. To maintain 
 *     backwards compatibility with existing user code, the namespace has not been changed.
 */
namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Specifies a configuration variable name in the plugin's configuration 
    /// containing data specific to the type
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="ConfigurationNameAttribute"/>
    /// </remarks>
    /// <param name="configVarName">The name of the configuration variable for the class</param>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigurationNameAttribute(string configVarName) : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        public string ConfigVarName { get; } = configVarName;

        /// <summary>
        /// When true or not configured, signals that the type requires a configuration scope
        /// when loaded. When false and configuration is not found, signals to the service loading
        /// system to continue without configuration
        /// </summary>
        public bool Required { get; init; } = true;
    }
}
