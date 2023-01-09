/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IntervalResultionType.cs 
*
* IntervalResultionType.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// The configurable event interval resulution type
    /// </summary>
    public enum IntervalResultionType
    {
        /// <summary>
        /// Specifies event interval resolution in milliseconds
        /// </summary>
        Milliseconds,
        /// <summary>
        /// Specifies event interval resolution in seconds
        /// </summary>
        Seconds,
        /// <summary>
        /// Specifies event interval resolution in minutes
        /// </summary>
        Minutes,
        /// <summary>
        /// Specifies event interval resolution in hours
        /// </summary>
        Hours
    }
}
