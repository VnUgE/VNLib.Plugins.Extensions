/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: IDbTable.cs 
*
* IDbTable.cs is part of VNLib.Plugins.Extensions.Loading.Sql which is part 
* of the larger VNLib collection of libraries and utilities.
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

using System.Text;

namespace VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder
{
    interface IDbTable
    {
        /// <summary>
        /// Requests the table build the table creation statment using the <see cref="IDBCommandGenerator"/>
        /// instance and write the statment to the string builder instance
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> instance to write the statment to</param>
        /// <param name="commandBuilder">The abstract command builder used to create the statment</param>
        void WriteCommand(StringBuilder sb, IDBCommandGenerator commandBuilder);
    }
}
