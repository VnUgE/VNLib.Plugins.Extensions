/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: Extensions.cs 
*
* Extensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils;
using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data
{
    public static class Extensions
    {

        public static int GetPageOrDefault(this IReadOnlyDictionary<string, string> queryArgs, int @default, int minClamp = 0, int maxClamp = int.MaxValue)
        {
            return queryArgs.TryGetValue("page", out string? pageStr) && int.TryParse(pageStr, out int page) ? Math.Clamp(page, minClamp, maxClamp) : @default;
        }

        public static int GetLimitOrDefault(this IReadOnlyDictionary<string, string> queryArgs, int @default, int minClamp = 0, int maxClamp = int.MaxValue)
        {
            return queryArgs.TryGetValue("limit", out string? limitStr) && int.TryParse(limitStr, out int limit) ? Math.Clamp(limit, minClamp, maxClamp) : @default;
        }

        public static async Task<ERRNO> AddBulkAsync<TEntity>(this DbStore<TEntity> store, IEnumerable<TEntity> records, string userId, bool overwriteTime = true) 
            where TEntity : class, IDbModel, IUserEntity
        {
            //Open context and transaction
            await using TransactionalDbContext database = store.NewContext();
            await database.OpenTransactionAsync();
            //Get the entity set
            DbSet<TEntity> set = database.Set<TEntity>();
            //Generate random ids for the feeds and set user-id
            foreach (TEntity entity in records)
            {
                entity.Id = store.RecordIdBuilder;
                //Explicitly assign the user-id
                entity.UserId = userId;
                //If the entity has the default created time, update it, otherwise leave it as is
                if (overwriteTime || entity.Created == default)
                {
                    entity.Created = DateTime.UtcNow;
                }
                //Update last-modified time
                entity.LastModified = DateTime.UtcNow;
            }
            //Add feeds to database
            set.AddRange(records);
            //Commit changes
            ERRNO count = await database.SaveChangesAsync();
            //Commit transaction and exit
            await database.CommitTransactionAsync();
            return count;
        }
    }
}
