using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
    public abstract class DbStore<T> : IDataStore<T>, IPaginatedDataStore<T> where T: class, IDbModel
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
        public virtual async Task<ERRNO> AddOrUpdateAsync(T record)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
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
            T? entry = await query.SingleOrDefaultAsync();
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
            //Save changes
            ERRNO result = await ctx.SaveChangesAsync();
            if (result)
            {
                //commit transaction if update was successful
                await ctx.CommitTransactionAsync();
            }
            return result;
        }
        ///<inheritdoc/>
        public virtual async Task<ERRNO> UpdateAsync(T record)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the application
            IQueryable<T> query = UpdateQueryBuilder(ctx, record);
            //Using single to make sure only one app is in the db (should never be an issue)
            T? oldEntry = await query.SingleOrDefaultAsync();
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
                await ctx.CommitTransactionAsync();
                return true;
            }
            //Save changes
            ERRNO result = await ctx.SaveChangesAsync();
            if (result)
            {
                //commit transaction if update was successful
                await ctx.CommitTransactionAsync();
            }
            return result;
        }
        ///<inheritdoc/>
        public virtual async Task<ERRNO> CreateAsync(T record)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Create a new template id
            record.Id = RecordIdBuilder;
            //Update the created/last modified time of the record
            record.Created = record.LastModified = DateTime.UtcNow;
            //Add the new template
            ctx.Add(record);
            //save changes
            ERRNO result = await ctx.SaveChangesAsync();
            if (result)
            {
                //Commit transaction
                await ctx.CommitTransactionAsync();
            }
            return result;
        }  

        /// <summary>
        /// Builds a query that attempts to get a single entry from the 
        /// store based on the specified record if it does not have a 
        /// valid <see cref="DbModelBase.Id"/> property
        /// </summary>
        /// <param name="context">The active context to query</param>
        /// <param name="record">The record to search for</param>
        /// <returns>A query that yields a single record if it exists in the store</returns>
        protected virtual IQueryable<T> AddOrUpdateQueryBuilder(TransactionalDbContext context, T record)
        {
            //default to get single of the specific record
            return GetSingleQueryBuilder(context, record);
        }
        /// <summary>
        /// Builds a query that attempts to get a single entry from the 
        /// store to update based on the specified record 
        /// </summary>
        /// <param name="context">The active context to query</param>
        /// <param name="record">The record to search for</param>
        /// <returns>A query that yields a single record to update if it exists in the store</returns>
        protected virtual IQueryable<T> UpdateQueryBuilder(TransactionalDbContext context, T record)
        {
            //default to get single of the specific record
            return GetSingleQueryBuilder(context, record);
        }
        /// <summary>
        /// Updates the current record (if found) to the new record before
        /// storing the updates.
        /// </summary>
        /// <param name="newRecord">The new record to capture data from</param>
        /// <param name="currentRecord">The current record to be updated</param>
        protected abstract void OnRecordUpdate(T newRecord, T currentRecord);
        #endregion

        #region Delete
        ///<inheritdoc/>
        public virtual async Task<ERRNO> DeleteAsync(string key)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the template by its id
            IQueryable<T> query = (from temp in ctx.Set<T>()
                                    where temp.Id == key
                                    select temp);
            T? record = await query.SingleOrDefaultAsync();
            if (record == null)
            {
                return false;
            }
            //Add the new application
            ctx.Remove(record);
            //Save changes
            ERRNO result = await ctx.SaveChangesAsync();
            if (result)
            {
                //Commit transaction
                await ctx.CommitTransactionAsync();
            }
            return result;
        }
        ///<inheritdoc/>
        public virtual async Task<ERRNO> DeleteAsync(T record)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get a query for a a single item
            IQueryable<T> query = GetSingleQueryBuilder(ctx, record);
            //Get the entry
            T? entry = await query.SingleOrDefaultAsync();
            if (entry == null)
            {
                return false;
            }
            //Add the new application
            ctx.Remove(entry);
            //Save changes
            ERRNO result = await ctx.SaveChangesAsync();
            if (result)
            {
                //Commit transaction
                await ctx.CommitTransactionAsync();
            }
            return result;
        }
        ///<inheritdoc/>
        public virtual async Task<ERRNO> DeleteAsync(params string[] specifiers)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the template by its id
            IQueryable<T> query = DeleteQueryBuilder(ctx, specifiers);
            T? entry = await query.SingleOrDefaultAsync();
            if (entry == null)
            {
                return false;
            }
            //Add the new application
            ctx.Remove(entry);
            //Save changes
            ERRNO result = await ctx.SaveChangesAsync();
            if (result)
            {
                //Commit transaction
                await ctx.CommitTransactionAsync();
            }
            return result;
        }

        /// <summary>
        /// Builds a query that results in a single entry to delete from the 
        /// constraint arguments
        /// </summary>
        /// <param name="context">The active context</param>
        /// <param name="constraints">A variable length parameter array of query constraints</param>
        /// <returns>A query that yields a single record (or no record) to delete</returns>
        protected virtual IQueryable<T> DeleteQueryBuilder(TransactionalDbContext context, params string[] constraints)
        {
            //default use the get-single method, as the implementation is usually identical
            return GetSingleQueryBuilder(context, constraints);
        }
        #endregion

        #region Get Collection
        ///<inheritdoc/>
        public virtual async Task<ERRNO> GetCollectionAsync(ICollection<T> collection, string specifier, int limit)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the single template by its id
            IAsyncEnumerable<T> entires = GetCollectionQueryBuilder(ctx, specifier).Take(limit).AsAsyncEnumerable();
            int count = 0;
            //Enumrate the template and add them to collection
            await foreach (T entry in entires)
            {
                collection.Add(entry);
                count++;
            }
            //close db and transaction
            await ctx.CommitTransactionAsync();
            //Return the number of elements add to the collection
            return count;
        }
        ///<inheritdoc/>
        public virtual async Task<ERRNO> GetCollectionAsync(ICollection<T> collection, int limit, params string[] args)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the single template by its id
            IAsyncEnumerable<T> entires = GetCollectionQueryBuilder(ctx, args).Take(limit).AsAsyncEnumerable();
            int count = 0;
            //Enumrate the template and add them to collection
            await foreach (T entry in entires)
            {
                collection.Add(entry);
                count++;
            }
            //close db and transaction
            await ctx.CommitTransactionAsync();
            //Return the number of elements add to the collection
            return count;
        }

        /// <summary>
        /// Builds a query to get a count of records constrained by the specifier
        /// </summary>
        /// <param name="context">The active context to run the query on</param>
        /// <param name="specifier">The specifier constrain</param>
        /// <returns>A query that can be counted</returns>
        protected virtual IQueryable<T> GetCollectionQueryBuilder(TransactionalDbContext context, string specifier)
        {
            return GetCollectionQueryBuilder(context, new string[] { specifier });
        }

        /// <summary>
        /// Builds a query to get a collection of records based on an variable length array of parameters
        /// </summary>
        /// <param name="context">The active context to run the query on</param>
        /// <param name="constraints">An arguments array to constrain the results of the query</param>
        /// <returns>A query that returns a collection of records from the store</returns>
        protected abstract IQueryable<T> GetCollectionQueryBuilder(TransactionalDbContext context, params string[] constraints);

        #endregion

        #region Get Count
        ///<inheritdoc/>
        public virtual async Task<long> GetCountAsync()
        {
            //Open db connection
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Async get the number of records of the given entity type
            long count = await ctx.Set<T>().LongCountAsync();                              
            //close db and transaction
            await ctx.CommitTransactionAsync();
            return count;
        }
        ///<inheritdoc/>
        public virtual async Task<long> GetCountAsync(string specifier)
        {
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Async get the number of records of the given entity type
            long count = await GetCountQueryBuilder(ctx, specifier).LongCountAsync();
            //close db and transaction
            await ctx.CommitTransactionAsync();
            return count;
        }

        /// <summary>
        /// Builds a query to get a count of records constrained by the specifier
        /// </summary>
        /// <param name="context">The active context to run the query on</param>
        /// <param name="specifier">The specifier constrain</param>
        /// <returns>A query that can be counted</returns>
        protected virtual IQueryable<T> GetCountQueryBuilder(TransactionalDbContext context, string specifier)
        {
            //Default use the get collection and just call the count method
            return GetCollectionQueryBuilder(context, specifier);
        }
        #endregion

        #region Get Single
        ///<inheritdoc/>
        public virtual async Task<T?> GetSingleAsync(string key)
        {
            //Open db connection
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the single template by its id
            T? record = await (from entry in ctx.Set<T>()
                              where entry.Id == key
                              select entry)
                              .SingleOrDefaultAsync();
            //close db and transaction
            await ctx.CommitTransactionAsync();
            return record;
        }
        ///<inheritdoc/>
        public virtual async Task<T?> GetSingleAsync(T record)
        {
            //Open db connection
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the single template by its id
            T? entry = await GetSingleQueryBuilder(ctx, record).SingleOrDefaultAsync();
            //close db and transaction
            await ctx.CommitTransactionAsync();
            return record;
        }
        ///<inheritdoc/>
        public virtual async Task<T?> GetSingleAsync(params string[] specifiers)
        {
            //Open db connection
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the single template by its id
            T? record = await GetSingleQueryBuilder(ctx, specifiers).SingleOrDefaultAsync();
            //close db and transaction
            await ctx.CommitTransactionAsync();
            return record;
        }
        /// <summary>
        /// Builds a query to get a single record from the variable length parameter arguments
        /// </summary>
        /// <param name="context">The context to execute query against</param>
        /// <param name="constraints">Arguments to constrain the results of the query to a single record</param>
        /// <returns>A query that yields a single record</returns>
        protected abstract IQueryable<T> GetSingleQueryBuilder(TransactionalDbContext context, params string[] constraints);
        /// <summary>
        /// <para>
        /// Builds a query to get a single record from the specified record.
        /// </para>
        /// <para>
        /// Unless overridden, performs an ID based query for a single entry
        /// </para>
        /// </summary>
        /// <param name="context">The context to execute query against</param>
        /// <param name="record">A record to referrence the lookup</param>
        /// <returns>A query that yields a single record</returns>
        protected virtual IQueryable<T> GetSingleQueryBuilder(TransactionalDbContext context, T record)
        {
            return from entry in context.Set<T>()
                   where entry.Id == record.Id
                   select entry;
        }
        #endregion

        #region Get Page
        ///<inheritdoc/>
        public virtual async Task<int> GetPageAsync(ICollection<T> collection, int page, int limit)
        {
            //Open db connection
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get a page offset and a limit for the 
            IAsyncEnumerable<T> records = ctx.Set<T>()
                                            .Skip(page * limit)
                                            .Take(limit)
                                            .AsAsyncEnumerable();
            int count = 0;
            //Enumrate the template and add them to collection
            await foreach (T record in records)
            {
                collection.Add(record);
                count++;
            }
            //close db and transaction
            await ctx.CommitTransactionAsync();
            //Return the number of elements add to the collection
            return count;
        }
        ///<inheritdoc/>
        public virtual async Task<int> GetPageAsync(ICollection<T> collection, int page, int limit, params string[] constraints)
        {
            //Open new db context
            await using TransactionalDbContext ctx = NewContext();
            //Open transaction
            await ctx.OpenTransactionAsync();
            //Get the single template by its id
            IAsyncEnumerable<T> entires = GetPageQueryBuilder(ctx, constraints)
                                            .Skip(page * limit)
                                            .Take(limit)
                                            .AsAsyncEnumerable();
            int count = 0;
            //Enumrate the template and add them to collection
            await foreach (T entry in entires)
            {
                collection.Add(entry);
                count++;
            }
            //close db and transaction
            await ctx.CommitTransactionAsync();
            //Return the number of elements add to the collection
            return count;
        }
        /// <summary>
        /// Builds a query to get a collection of records based on an variable length array of parameters
        /// </summary>
        /// <param name="context">The active context to run the query on</param>
        /// <param name="constraints">An arguments array to constrain the results of the query</param>
        /// <returns>A query that returns a paginated collection of records from the store</returns>
        protected virtual IQueryable<T> GetPageQueryBuilder(TransactionalDbContext context, params string[] constraints)
        {
            //Default to getting the entire collection and just selecting a single page
            return GetCollectionQueryBuilder(context, constraints);
        }
        #endregion
    }
}
