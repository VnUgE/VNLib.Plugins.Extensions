﻿/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: LWDecriptorCreationException.cs 
*
* LWDecriptorCreationException.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Plugins.Extensions.Data.Storage
{
    /// <summary>
    /// Raised when an operation to create a new <see cref="LWStorageDescriptor"/>
    /// fails
    /// </summary>
    public class LWDescriptorCreationException : Exception
    { 
        ///<inheritdoc/>
        public LWDescriptorCreationException()
        {}
        ///<inheritdoc/>
        public LWDescriptorCreationException(string? message) : base(message)
        {}
        ///<inheritdoc/>
        public LWDescriptorCreationException(string? message, Exception? innerException) : base(message, innerException)
        {}
    }
}