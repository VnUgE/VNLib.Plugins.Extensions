/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DbStoreExtensions.cs 
*
* DbStoreExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils;
using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IDataStore{T}"/> to add additional functionality
    /// </summary>
    public static class DbStoreExtensions
    {
        /// <summary>
        /// Updates an entry in the store if it exists, or creates a new entry if one does not already exist
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to add to the store</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task the resolves the result of the operation</returns>
        public static async Task<ERRNO> AddOrUpdateAsync<T>(this IDataStore<T> store, T record, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            IQueryable<T> query;

            if (string.IsNullOrWhiteSpace(record.Id))
            {
                //Get the application
                query = store.QueryTable.AddOrUpdateQueryBuilder(ctx, record);
            }
            else
            {
                //Get the application
                query = (from et in ctx.Set<T>()
                         where et.Id == record.Id
                         select et);
            }

            //Using single
            T? entry = await query.SingleOrDefaultAsync(cancellation);

            //Check if creted
            if (entry is null)
            {
                //Create a new template id
                record.Id = store.GetNewRecordId();
                //Set the created/lm times
                record.Created = record.LastModified = DateTime.UtcNow;
                //Add the new template to the ctx
                ctx.Add(record);
            }
            else
            {
                store.OnRecordUpdate(record, entry);
            }

            return await ctx.SaveAndCloseAsync(true, cancellation);
        }

        /// <summary>
        /// Updates an entry in the store with the specified record
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to update</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        public static async Task<ERRNO> UpdateAsync<T>(this IDataStore<T> store, T record, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the application
            IQueryable<T> query = store.QueryTable.UpdateQueryBuilder(ctx, record);

            //Using single to make sure only one app is in the db (should never be an issue)
            T? oldEntry = await query.SingleOrDefaultAsync(cancellation);

            if (oldEntry == null)
            {
                return false;
            }

            //Update the template meta-data
            store.OnRecordUpdate(record, oldEntry);

            return await ctx.SaveAndCloseAsync(true, cancellation);
        }

        /// <summary>
        /// Creates a new entry in the store representing the specified record
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to add to the store</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task the resolves an error code (should evaluate to false on failure, and true on success)</returns>
        public static async Task<ERRNO> CreateAsync<T>(this IDataStore<T> store, T record, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Create a new template id
            record.Id = store.GetNewRecordId();

            //Update the created/last modified time of the record
            record.Created = record.LastModified = DateTime.UtcNow;

            //Add the new template
            ctx.Add(record);

            return await ctx.SaveAndCloseAsync(true, cancellation);
        }


        /// <summary>
        /// Gets the total number of records in the current store
        /// </summary>
        /// <param name="store"></param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task that resolves the number of records in the store</returns>
        public static async Task<long> GetCountAsync<T>(this IDataStore<T> store, CancellationToken cancellation = default)
            where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Async get the number of records of the given entity type
            long count = await ctx.Set<T>().LongCountAsync(cancellation);

            //close db and transaction
            await ctx.SaveAndCloseAsync(true, cancellation);

            return count;
        }

        /// <summary>
        /// Gets the number of records that belong to the specified constraint
        /// </summary>
        /// <param name="store"></param>
        /// <param name="specifier">A specifier to constrain the reults</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>The number of records that belong to the specifier</returns>
        public static async Task<long> GetCountAsync<T>(this IDataStore<T> store, string specifier, CancellationToken cancellation = default)
            where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Async get the number of records of the given entity type
            long count = await store.QueryTable.GetCountQueryBuilder(ctx, specifier).LongCountAsync(cancellation);

            //close db and transaction
            await ctx.SaveAndCloseAsync(true, cancellation);

            return count;
        }


        /// <summary>
        /// Gets a record from its key
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key">The key identifying the unique record</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A promise that resolves the record identified by the specified key</returns>
        public static async Task<T?> GetSingleAsync<T>(this IDataStore<T> store, string key, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the single template by its id
            T? record = await (from entry in ctx.Set<T>()
                               where entry.Id == key
                               select entry)
                              .AsNoTracking()
                              .SingleOrDefaultAsync(cancellation);

            //close db and transaction
            await ctx.SaveAndCloseAsync(true, cancellation);
            return record;
        }

        /// <summary>
        /// Gets a record identified by it's id
        /// </summary>
        /// <param name="store"></param>
        /// <param name="specifiers">A variable length specifier arguemnt array for retreiving a single application</param>
        /// <returns>A task that resolves the entity if it exists</returns>
        public static async Task<T?> GetSingleAsync<T>(this IDataStore<T> store, params string[] specifiers)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the single item by specifiers
            T? record = await store.QueryTable.GetSingleQueryBuilder(ctx, specifiers).SingleOrDefaultAsync();

            //close db and transaction
            await ctx.SaveAndCloseAsync(true);

            return record;
        }

        /// <summary>
        /// Gets a record from the store with a partial model, intended to complete the model
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The partial model used to query the store</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task the resolves the completed data-model</returns>
        public static async Task<T?> GetSingleAsync<T>(this IDataStore<T> store, T record, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the single template by its id
            T? entry = await store.QueryTable.GetSingleQueryBuilder(ctx, record).SingleOrDefaultAsync(cancellation);

            //close db and transaction
            await ctx.SaveAndCloseAsync(true, cancellation);

            return record;
        }


        /// <summary>
        /// Fills a collection with enires retireved from the store using the specifer
        /// </summary>
        /// <param name="store"></param>
        /// <param name="collection">The collection to add entires to</param>
        /// <param name="specifier">A specifier argument to constrain results</param>
        /// <param name="limit">The maximum number of elements to retrieve</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A Task the resolves to the number of items added to the collection</returns>
        public static async Task<ERRNO> GetCollectionAsync<T>(this IDataStore<T> store, ICollection<T> collection, string specifier, int limit, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            int previous = collection.Count;

            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the single template by its id
            await store.QueryTable.GetCollectionQueryBuilder(ctx, specifier)
                .Take(limit)
                .Select(static e => e)
                .AsNoTracking()
                .ForEachAsync(collection.Add, cancellation);

            //close db and transaction
            _ = await ctx.SaveAndCloseAsync(true, cancellation);

            //Return the number of elements add to the collection
            return collection.Count - previous;
        }

        /// <summary>
        /// Fills a collection with enires retireved from the store using a variable length specifier
        /// parameter
        /// </summary>
        /// <param name="store"></param>
        /// <param name="collection">The collection to add entires to</param>
        /// <param name="limit">The maximum number of elements to retrieve</param>
        /// <param name="args"></param>
        /// <returns>A Task the resolves to the number of items added to the collection</returns>
        public static async Task<ERRNO> GetCollectionAsync<T>(this IDataStore<T> store, ICollection<T> collection, int limit, params string[] args)
              where T : class, IDbModel
        {
            int previous = collection.Count;

            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the single template by its id
            await store.QueryTable.GetCollectionQueryBuilder(ctx, args)
                .Take(limit)
                .Select(static e => e)
                .AsNoTracking()
                .ForEachAsync(collection.Add);

            //close db and transaction
            _ = await ctx.SaveAndCloseAsync(true);

            //Return the number of elements add to the collection
            return collection.Count - previous;
        }


        /// <summary>
        /// Deletes one or more entrires from the store matching the specified record
        /// </summary>
        /// <param name="store"></param>
        /// <param name="record">The record to remove from the store</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task the resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        public static async Task<ERRNO> DeleteAsync<T>(this IDataStore<T> store, T record, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get a query for a a single item
            IQueryable<T> query = store.QueryTable.GetSingleQueryBuilder(ctx, record);

            //Get the entry if it exists
            T? entry = await query.SingleOrDefaultAsync(cancellation);

            if (entry is null)
            {
                await ctx.SaveAndCloseAsync(false, cancellation);
                return false;
            }
            else
            {
                //Remove the entry
                ctx.Remove(entry);
                return await ctx.SaveAndCloseAsync(true, cancellation);
            }
        }

        /// <summary>
        /// Deletes one or more entires from the store matching the specified unique key
        /// </summary>
        /// <param name="store"></param>
        /// <param name="key">The unique key that identifies the record</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task the resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        public static async Task<ERRNO> DeleteAsync<T>(this IDataStore<T> store, string key, CancellationToken cancellation = default)
              where T : class, IDbModel
        {
            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get a query for a a single item
            IQueryable<T> query = store.QueryTable.GetSingleQueryBuilder(ctx, key);

            //Get the entry if it exists
            T? entry = await query.SingleOrDefaultAsync(cancellation);

            if (entry is null)
            {
                await ctx.SaveAndCloseAsync(false, cancellation);
                return false;
            }
            else
            {
                //Remove the entry then exit
                ctx.Remove(entry);
                return await ctx.SaveAndCloseAsync(true, cancellation);
            }
        }

        /// <summary>
        /// Deletes one or more entires from the store matching the supplied specifiers
        /// </summary>
        /// <param name="store"></param>
        /// <param name="specifiers">A variable length array of specifiers used to delete one or more entires</param>
        /// <returns>A task the resolves the number of records removed(should evaluate to false on failure, and deleted count on success)</returns>
        public static async Task<ERRNO> DeleteAsync<T>(this IDataStore<T> store, params string[] specifiers)
              where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get the template by its id
            IQueryable<T> query = store.QueryTable.DeleteQueryBuilder(ctx, specifiers);

            T? entry = await query.SingleOrDefaultAsync();

            if (entry is null)
            {
                return false;
            }

            //Add the new application
            ctx.Remove(entry);

            return await ctx.SaveAndCloseAsync(true);
        }


        /// <summary>
        /// Gets a collection of records using a pagination style query, and adds the records to the collecion
        /// </summary>
        /// <param name="store"></param>
        /// <param name="collection">The collection to add records to</param>
        /// <param name="page">Pagination page to get records from</param>
        /// <param name="limit">The maximum number of items to retrieve from the store</param>
        /// <param name="cancellation">A cancellation token to cancel the operation</param>
        /// <returns>A task that resolves the number of items added to the collection</returns>
        public static async Task<int> GetPageAsync<T>(this IDataStore<T> store, ICollection<T> collection, int page, int limit, CancellationToken cancellation = default)
            where T : class, IDbModel
        {
            //Store preivous count
            int previous = collection.Count;

            ArgumentNullException.ThrowIfNull(store);

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get a page offset and a limit for the 
            await ctx.Set<T>()
                .Skip(page * limit)
                .Take(limit)
                .Select(static p => p)
                .AsNoTracking()
                .ForEachAsync(collection.Add, cancellation);

            //close db and transaction
            await ctx.SaveAndCloseAsync(true, cancellation);

            //Return the number of records added
            return collection.Count - previous;
        }

        /// <summary>
        /// Gets a collection of records using a pagination style query with constraint arguments, and adds the records to the collecion
        /// </summary>
        /// <param name="store"></param>
        /// <param name="collection">The collection to add records to</param>
        /// <param name="page">Pagination page to get records from</param>
        /// <param name="limit">The maximum number of items to retrieve from the store</param>
        /// <param name="constraints">A params array of strings to constrain the result set from the db</param>
        /// <returns>A task that resolves the number of items added to the collection</returns>
        public static async Task<int> GetPageAsync<T>(this IDataStore<T> store, ICollection<T> collection, int page, int limit, params string[] constraints)
            where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(collection);

            //Store preivous count
            int previous = collection.Count;

            //Open new db context
            await using IDbContextHandle ctx = store.GetNewContext();

            //Get a page of records constrained by the given arguments
            await store.QueryTable.GetPageQueryBuilder(ctx, constraints)
                .Skip(page * limit)
                .Take(limit)
                .Select(static e => e)
                .AsNoTracking()
                .ForEachAsync(collection.Add);

            //close db and transaction
            await ctx.SaveAndCloseAsync(true);

            //Return the number of records added
            return collection.Count - previous;
        }

        /// <summary>
        /// Specifies the user-id for each record in collection of user entities
        /// during enumeration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="current"></param>
        /// <param name="userId">The user-id to specify</param>
        /// <returns>A new enumeration instance that will add the new user-id to each record upon enumerating</returns>
        public static IEnumerable<T> WithUserId<T>(this IEnumerable<T> current, string? userId)
             where T : IUserEntity
        {
            return current.Select(p =>
            {
                p.UserId = userId;
                return p;
            });
        }

        /// <summary>
        /// Updates the created and last modified times for each record in the collection
        /// with the current time in UTC.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="current"></param>
        /// <param name="overwriteTime">A value that indicates if the record's created time should be overwritten or ignored</param>
        /// <returns>A new enumeration that will updated record times upon enumerating</returns>
        public static IEnumerable<T> WithCurrentUtcTime<T>(this IEnumerable<T> current, bool overwriteTime) where T : IDbModel
        {
            return WithTime(current, DateTime.UtcNow, overwriteTime);
        }

        /// <summary>
        /// Updates the created and last modified times for each record in the collection
        /// with the current system local time.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="current"></param>
        /// <param name="overwriteTime">A value that indicates if the record's created time should be overwritten or ignored</param>
        /// <returns>A new enumeration that will updated record times upon enumerating</returns>
        public static IEnumerable<T> WithCurrentLocalTime<T>(this IEnumerable<T> current, bool overwriteTime) where T : IDbModel
        {
            return WithTime(current, DateTime.Now, overwriteTime);
        }

        /// <summary>
        /// Updates the created and last modified times for each record in the collection
        /// with the specified time.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="current"></param>
        /// <param name="now">A structure to the time to set seach record's last modifed, and optionally created time</param>
        /// <param name="overwriteTime">A value that indicates if the record's created time should be overwritten or ignored</param>
        /// <returns>A new enumeration that will updated record times upon enumerating</returns>
        public static IEnumerable<T> WithTime<T>(this IEnumerable<T> current, DateTime now, bool overwriteTime)
            where T : IDbModel
        {
            return current.Select(p =>
            {
                //Only overwrite the created time if it is the default value or the overwrite flag is set
                if (overwriteTime || p.Created == default)
                {
                    p.Created = now;
                }

                //Always update the last modified time
                p.LastModified = now;
                return p;
            });
        }

        /// <summary>
        /// Adds a collection of records directly to the store without any additional processing
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="store"></param>
        /// <param name="records">The collection of records to add</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A task the complets with the number of records update</returns>
        public static async Task<ERRNO> AddBulkAsync<T>(this IDataStore<T> store, IEnumerable<T> records, CancellationToken cancellation = default)
          where T : class, IDbModel
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(records);

            //Open new db context
            await using IDbContextHandle database = store.GetNewContext();

            //Generate random ids for the feeds and set user-id
            foreach (T entity in records)
            {
                entity.Id = store.GetNewRecordId();
            }

            //Add bulk items to database
            database.AddRange(records);
            return await database.SaveAndCloseAsync(true, cancellation);
        }
    }
}
