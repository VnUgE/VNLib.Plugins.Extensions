/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: LWStorageUpdateFailedException.cs 
*
* LWStorageUpdateFailedException.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Data. If not, see http://www.gnu.org/licenses/.
*/

using System;
using VNLib.Utils;

namespace VNLib.Plugins.Extensions.Data.Storage
{
    /// <summary>
    /// The exception raised when an open <see cref="LWStorageDescriptor"/> update operation fails. The 
    /// <see cref="Exception.InnerException"/> property may contain any nested exceptions that caused the update to fail.
    /// </summary>
    public class LWStorageUpdateFailedException : ResourceUpdateFailedException
    {
        internal LWStorageUpdateFailedException(string error, Exception inner) : base(error, inner) { }
    }
}