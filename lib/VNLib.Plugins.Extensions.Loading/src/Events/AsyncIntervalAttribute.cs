/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: AsyncIntervalAttribute.cs 
*
* AsyncIntervalAttribute.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// When added to a method schedules it as a callback on a specified interval when 
    /// the plugin is loaded, and stops when unloaded
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AsyncIntervalAttribute : Attribute
    {
        internal readonly TimeSpan Interval;

        /// <summary>
        /// Initializes a new <see cref="AsyncIntervalAttribute"/> with allowing 
        /// a configurable
        /// </summary>
        public AsyncIntervalAttribute()
        {}

        /// <summary>
        /// Gets or sets the interval in seconds. Choose only ONE internval resolution
        /// </summary>
        public int Seconds
        {
            get => (int)Interval.TotalSeconds;
            init => Interval = TimeSpan.FromSeconds(value);
        }

        /// <summary>
        /// Gets or sets the interval in milliseconds. Choose only ONE internval resolution
        /// </summary>
        public int MilliSeconds
        {
            get => (int)Interval.TotalMilliseconds;
            init => Interval = TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        /// Gets or sets the interval in minutes. Choose only ONE internval resolution
        /// </summary>
        public int Minutes
        {
            get => (int)Interval.TotalMinutes; 
            init => Interval = TimeSpan.FromMinutes(value);
        }

        /// <summary>
        /// Gets or sets the interval in hours. Choose only ONE internval resolution
        /// </summary>
        public int Hours
        {
            get => (int)Interval.TotalMinutes;
            init => Interval = TimeSpan.FromHours(value);
        }
    }
}
