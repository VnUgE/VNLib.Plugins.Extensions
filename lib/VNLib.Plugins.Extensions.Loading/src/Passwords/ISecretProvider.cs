/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ISecretProvider.cs 
*
* ISecretProvider.cs is part of VNLib.Plugins.Extensions.Loading which is part 
* of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Plugins.Extensions.Loading.Passwords
{
    /// <summary>
    /// Provides a password hashing secret, also known as a pepper.
    /// </summary>
    internal interface ISecretProvider
    {
        /// <summary>
        /// Gets the secret bytes used as a pepper for password hashing.
        /// </summary>
        ReadOnlySpan<byte> Secret { get; }
    }
}
