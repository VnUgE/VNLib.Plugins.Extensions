﻿/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: MissingDependencyException.cs 
*
* MissingDependencyException.cs is part of VNLib.Plugins.Extensions.Loading 
* which is part of the larger VNLib collection of libraries and utilities.
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
    /// <summary>
    /// Raised when a required dependency is missing and the program cannot continue
    /// </summary>
    public sealed class MissingDependencyException : Exception
    {
        public MissingDependencyException(string message) : base(message)
        { }
        public MissingDependencyException(string message, Exception innerException) : base(message, innerException)
        { }
        public MissingDependencyException()
        { }
    }
}
