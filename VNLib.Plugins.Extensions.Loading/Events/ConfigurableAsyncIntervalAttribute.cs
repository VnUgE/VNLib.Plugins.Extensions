﻿/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ConfigurableAsyncIntervalAttribute.cs 
*
* ConfigurableAsyncIntervalAttribute.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Loading. If not, see http://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// When added to a method schedules it as a callback on a specified interval when 
    /// the plugin is loaded, and stops when unloaded
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ConfigurableAsyncIntervalAttribute : Attribute
    {
        internal readonly string IntervalPropertyName;
        internal readonly IntervalResultionType Resolution;

        /// <summary>
        /// Initializes a <see cref="ConfigurableAsyncIntervalAttribute"/> with the specified
        /// interval property name
        /// </summary>
        /// <param name="configPropName">The configuration property name for the event interval</param>
        /// <param name="resolution">The time resoltion for the event interval</param>
        public ConfigurableAsyncIntervalAttribute(string configPropName, IntervalResultionType resolution)
        {
            IntervalPropertyName = configPropName;
            Resolution = resolution;
        }
    }
}
