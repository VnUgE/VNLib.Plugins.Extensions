/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DbStoreQueries.cs 
*
* DbStoreQueries.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Data
{

    public partial class DbStore<T>
    {

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

        /// <summary>
        /// Updates the current record (if found) to the new record before
        /// storing the updates.
        /// </summary>
        /// <param name="newRecord">The new record to capture data from</param>
        /// <param name="currentRecord">The current record to be updated</param>
        protected abstract void OnRecordUpdate(T newRecord, T currentRecord);

        /// <summary>
        /// Builds a query to get a single record from the variable length parameter arguments
        /// </summary>
        /// <param name="context">The context to execute query against</param>
        /// <param name="constraints">Arguments to constrain the results of the query to a single record</param>
        /// <returns>A query that yields a single record</returns>
        protected abstract IQueryable<T> GetSingleQueryBuilder(TransactionalDbContext context, params string[] constraints);

        /// <summary>
        /// Builds a query to get a collection of records based on an variable length array of parameters
        /// </summary>
        /// <param name="context">The active context to run the query on</param>
        /// <param name="constraints">An arguments array to constrain the results of the query</param>
        /// <returns>A query that returns a collection of records from the store</returns>
        protected abstract IQueryable<T> GetCollectionQueryBuilder(TransactionalDbContext context, params string[] constraints);
    }
}
