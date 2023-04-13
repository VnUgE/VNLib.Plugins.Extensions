/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IDbTableDefinition.cs 
*
* IDbTableDefinition.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is
* part of the larger VNLib collection of libraries and utilities.
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

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    /// <summary>
    /// When implemented by a <see cref="Microsoft.EntityFrameworkCore.DbContext"/> allows 
    /// for the custom creation of database tables for any given entity
    /// </summary>
    public interface IDbTableDefinition
    {
        /// <summary>
        /// Invoked when the model is being evaluated and the database tables are being created. You will define 
        /// your database tables on your entities.
        /// </summary>
        /// <param name="builder">The <see cref="IDbTableDefinition"/> used to define the tables and columns in your database</param>
        /// <param name="userState">An optional user-supplied state instace passed from the creation method</param>
        void OnDatabaseCreating(IDbContextBuilder builder, object? userState);
    }
}
