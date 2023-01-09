/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: LWStorageContext.cs 
*
* LWStorageContext.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using Microsoft.EntityFrameworkCore;

namespace VNLib.Plugins.Extensions.Data.Storage
{
#nullable disable
    internal sealed class LWStorageContext : TransactionalDbContext
    {
        private readonly string TableName;
        public DbSet<LWStorageEntry> Descriptors { get; set; }

        public LWStorageContext(DbContextOptions options, string tableName)
            :base(options)
        {
            TableName = tableName;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Set table name
            modelBuilder.Entity<LWStorageEntry>()
                .ToTable(TableName);
        }
    }
}