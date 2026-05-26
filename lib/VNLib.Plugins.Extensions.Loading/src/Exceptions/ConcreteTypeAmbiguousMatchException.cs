/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ConcreteTypeAmbiguousMatchException.cs 
*
* ConcreteTypeAmbiguousMatchException.cs is part of VNLib.Plugins.Extensions.Loading
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
    /// The exception that is thrown when a concrete type is ambiguous because more than one
    /// type implements the desired abstract type.
    /// </summary>
    public sealed class ConcreteTypeAmbiguousMatchException : ConcreteTypeException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcreteTypeAmbiguousMatchException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that describes the exception.</param>
        public ConcreteTypeAmbiguousMatchException(string message) : base(message)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcreteTypeAmbiguousMatchException"/> class with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The error message that describes the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ConcreteTypeAmbiguousMatchException(string message, Exception innerException) : base(message, innerException)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcreteTypeAmbiguousMatchException"/> class.
        /// </summary>
        public ConcreteTypeAmbiguousMatchException()
        { }
    }
}
