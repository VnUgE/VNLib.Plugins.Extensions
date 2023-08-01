/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DbStore.cs 
*
* DbStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Threading;
using System.Transactions;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using VNLib.Utils;
using VNLib.Utils.Memory.Caching;
using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data
{

    /// <summary>
    /// Implements basic data-store functionality with abstract query builders
    /// </summary>
    /// <typeparam name="T">A <see cref="DbModelBase"/> implemented type</typeparam>
    public abstract partial class DbStore<T> : IDataStore<T>, IPaginatedDataStore<T> where T: class, IDbModel
    {
        /// <summary>
        /// Gets a unique ID for a new record being added to the store
        /// </summary>
        public abstract string RecordIdBuilder { get; }

        /// <summary>
        /// Gets a new <see cref="TransactionalDbContext"/> ready for use
        /// </summary>
        /// <returns></returns>
        public abstract TransactionalDbContext NewContext();
    
        /// <summary>
        /// An object rental for entity collections
        /// </summary>
        public ObjectRental<List<T>> ListRental { get; } = ObjectRental.Create<List<T>>(null, static ret => ret.Clear());

        #region Add Or Update
        ///<inheritdoc/>
        public virtual async Task<ERRNO> AddOrUpdateAsync(T record, CancellationToken cancellation = default)
        {
            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadCommitted, cancellation);

            IQueryable<T> query;

            if (string.IsNullOrWhiteSpace(record.Id))
            {
                //Get the application
                query = AddOrUpdateQueryBuilder(ctx, record);
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
            if (entry == null)
            {
                //Create a new template id
                record.Id = RecordIdBuilder;
                //Set the created/lm times
                record.Created = record.LastModified = DateTime.UtcNow;
                //Add the new template to the ctx
                ctx.Add(record);
            }
            else
            {
                OnRecordUpdate(record, entry);
            }           

            return await ctx.SaveAndCloseAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual async Task<ERRNO> UpdateAsync(T record, CancellationToken cancellation = default)
        {
            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.Serializable, cancellation);

            //Get the application
            IQueryable<T> query = UpdateQueryBuilder(ctx, record);

            //Using single to make sure only one app is in the db (should never be an issue)
            T? oldEntry = await query.SingleOrDefaultAsync(cancellation);

            if (oldEntry == null)
            {
                return false;
            }

            //Update the template meta-data
            OnRecordUpdate(record, oldEntry);

            //Only publish update if changes happened
            if (!ctx.ChangeTracker.HasChanges())
            {
                //commit transaction if no changes need to be made
                await ctx.CommitTransactionAsync(cancellation);
                return true;
            }

            return await ctx.SaveAndCloseAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual async Task<ERRNO> CreateAsync(T record, CancellationToken cancellation = default)
        {
            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);
            
            //Create a new template id
            record.Id = RecordIdBuilder;

            //Update the created/last modified time of the record
            record.Created = record.LastModified = DateTime.UtcNow;

            //Add the new template
            ctx.Add(record);
         
            return await ctx.SaveAndCloseAsync(cancellation);
        }  
      
        #endregion

        #region Delete

        ///<inheritdoc/>
        public virtual async Task<ERRNO> DeleteAsync(string key, CancellationToken cancellation = default)
        {
            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.RepeatableRead, cancellation);

            //Get the template by its id
            IQueryable<T> query = (from temp in ctx.Set<T>()
                                    where temp.Id == key
                                    select temp);

            T? record = await query.SingleOrDefaultAsync(cancellation);

            if (record == null)
            {
                return false;
            }

            //Add the new application
            ctx.Remove(record);

            return await ctx.SaveAndCloseAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual async Task<ERRNO> DeleteAsync(T record, CancellationToken cancellation = default)
        {
            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.RepeatableRead, cancellation);

            //Get a query for a a single item
            IQueryable<T> query = GetSingleQueryBuilder(ctx, record);

            //Get the entry
            T? entry = await query.SingleOrDefaultAsync(cancellation);

            if (entry == null)
            {
                return false;
            }

            //Add the new application
            ctx.Remove(entry);

            return await ctx.SaveAndCloseAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual async Task<ERRNO> DeleteAsync(params string[] specifiers)
        {
            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.RepeatableRead);
            
            //Get the template by its id
            IQueryable<T> query = DeleteQueryBuilder(ctx, specifiers);

            T? entry = await query.SingleOrDefaultAsync();

            if (entry == null)
            {
                return false;
            }

            //Add the new application
            ctx.Remove(entry);
            
            return await ctx.SaveAndCloseAsync();
        }
      
        #endregion

        #region Get Collection

        ///<inheritdoc/>
        public virtual async Task<ERRNO> GetCollectionAsync(ICollection<T> collection, string specifier, int limit, CancellationToken cancellation = default)
        {
            int previous = collection.Count;

            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);

            //Get the single template by its id
            await GetCollectionQueryBuilder(ctx, specifier)
                .Take(limit)
                .Select(static e => e)
                .ForEachAsync(collection.Add, cancellation);

            //close db and transaction
            await ctx.CommitTransactionAsync(cancellation);

            //Return the number of elements add to the collection
            return collection.Count - previous;
        }

        ///<inheritdoc/>
        public virtual async Task<ERRNO> GetCollectionAsync(ICollection<T> collection, int limit, params string[] args)
        {
            int previous = collection.Count;

            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted);

            //Get the single template by the supplied user arguments
            await GetCollectionQueryBuilder(ctx, args)
                .Take(limit)
                .Select(static e => e)
                .ForEachAsync(collection.Add);

            //close db and transaction
            await ctx.CommitTransactionAsync();

            //Return the number of elements add to the collection
            return collection.Count - previous;
        }

        #endregion

        #region Get Count

        ///<inheritdoc/>
        public virtual async Task<long> GetCountAsync(CancellationToken cancellation = default)
        {
            //Open db connection
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);
            
            //Async get the number of records of the given entity type
            long count = await ctx.Set<T>().LongCountAsync(cancellation);
            
            //close db and transaction
            await ctx.CommitTransactionAsync(cancellation);

            return count;
        }

        ///<inheritdoc/>
        public virtual async Task<long> GetCountAsync(string specifier, CancellationToken cancellation)
        {
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);

            //Async get the number of records of the given entity type
            long count = await GetCountQueryBuilder(ctx, specifier).LongCountAsync(cancellation);

            //close db and transaction
            await ctx.CommitTransactionAsync(cancellation);

            return count;
        }

       
        #endregion

        #region Get Single

        ///<inheritdoc/>
        public virtual async Task<T?> GetSingleAsync(string key, CancellationToken cancellation = default)
        {
            //Open db connection
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);
            
            //Get the single template by its id
            T? record = await (from entry in ctx.Set<T>()
                              where entry.Id == key
                              select entry)
                              .SingleOrDefaultAsync(cancellation);

            //close db and transaction
            await ctx.CommitTransactionAsync(cancellation);
            return record;
        }

        ///<inheritdoc/>
        public virtual async Task<T?> GetSingleAsync(T record, CancellationToken cancellation = default)
        {
            //Open db connection
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);

            //Get the single template by its id
            T? entry = await GetSingleQueryBuilder(ctx, record).SingleOrDefaultAsync(cancellation);

            //close db and transaction
            await ctx.CommitTransactionAsync(cancellation);

            return record;
        }

        ///<inheritdoc/>
        public virtual async Task<T?> GetSingleAsync(params string[] specifiers)
        {
            //Open db connection
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted);
            
            //Get the single template by its id
            T? record = await GetSingleQueryBuilder(ctx, specifiers).SingleOrDefaultAsync();

            //close db and transaction
            await ctx.CommitTransactionAsync();

            return record;
        }

        #endregion

        #region Get Page

        ///<inheritdoc/>
        public virtual async Task<int> GetPageAsync(ICollection<T> collection, int page, int limit, CancellationToken cancellation = default)
        {
            //Store preivous count
            int previous = collection.Count;

            //Open db connection
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted, cancellation);
         
            //Get a page offset and a limit for the 
            await ctx.Set<T>()
                .Skip(page * limit)
                .Take(limit)
                .Select(static p => p)
                .ForEachAsync(collection.Add, cancellation);
           
            //close db and transaction
            await ctx.CommitTransactionAsync(cancellation);

            //Return the number of records added
            return collection.Count - previous;
        }

        ///<inheritdoc/>
        public virtual async Task<int> GetPageAsync(ICollection<T> collection, int page, int limit, params string[] constraints)
        {
            //Store preivous count
            int previous = collection.Count;

            //Open new db context
            await using TransactionalDbContext ctx = await this.OpenAsync(IsolationLevel.ReadUncommitted);
           
            //Get a page of records constrained by the given arguments
            await GetPageQueryBuilder(ctx, constraints)
                .Skip(page * limit)
                .Take(limit)
                .Select(static e => e)
                .ForEachAsync(collection.Add);
            
            //close db and transaction
            await ctx.CommitTransactionAsync();

            //Return the number of records added
            return collection.Count - previous;
        }

        #endregion
    }
}
