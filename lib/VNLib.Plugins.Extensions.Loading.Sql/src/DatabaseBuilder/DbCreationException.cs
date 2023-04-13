/*
* Copyright (c) 2023 Vaughn Nugent
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
    class DbCreationException : DbException
    {
        public DbCreationException()
        { }

        public DbCreationException(string? message) : base(message)
        { }

        public DbCreationException(string? message, Exception? innerException) : base(message, innerException)
        { }

        public DbCreationException(string? message, int errorCode) : base(message, errorCode)
        { }
    }
}
