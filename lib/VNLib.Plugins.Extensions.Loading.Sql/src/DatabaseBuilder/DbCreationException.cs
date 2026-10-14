/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: DbCreationException.cs 
*
* DbCreationException.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Data.Common;

namespace VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder
{
    /// <summary>
    /// The exception that is thrown when the creation of a database or its tables fails.
    /// </summary>
    /// <remarks>
    /// An exception thrown when the database builder is unable to create the backing
    /// database or the tables required by an <see cref="IDbTableDefinition"/>.
    /// </remarks>
    class DbCreationException : DbException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DbCreationException"/> class.
        /// </summary>
        public DbCreationException()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbCreationException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public DbCreationException(string? message) : base(message)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbCreationException"/> class with a
        /// specified error message and a reference to the inner exception that is the cause
        /// of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or a <see langword="null"/>
        /// reference if no inner exception is specified.
        /// </param>
        public DbCreationException(string? message, Exception? innerException) : base(message, innerException)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbCreationException"/> class with a
        /// specified error message and error code.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorCode">The error code for the exception.</param>
        public DbCreationException(string? message, int errorCode) : base(message, errorCode)
        { }
    }
}
