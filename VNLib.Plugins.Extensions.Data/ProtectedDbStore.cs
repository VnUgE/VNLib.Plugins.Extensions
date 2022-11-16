using System;
using System.Linq;

using VNLib.Plugins.Extensions.Data.Abstractions;

namespace VNLib.Plugins.Extensions.Data
{
#nullable enable
    /// <summary>
    /// A data store that provides unique identities and protections based on an entity that has an owner <see cref="IUserEntity"/>
    /// </summary>
    public abstract class ProtectedDbStore<TEntity> : DbStore<TEntity> where TEntity : class, IDbModel, IUserEntity
    {
        ///<inheritdoc/>
        protected override IQueryable<TEntity> GetCollectionQueryBuilder(TransactionalDbContext context, params string[] constraints)
        {
            string userId = constraints[0];
            //Query items for the user and its id
            return from item in context.Set<TEntity>()
                   where item.UserId == userId
                   orderby item.Created descending
                   select item;
        }

        /// <summary>
        /// Gets a single item contrained by a given user-id and item id
        /// </summary>
        /// <param name="context"></param>
        /// <param name="constraints"></param>
        /// <returns></returns>
        protected override IQueryable<TEntity> GetSingleQueryBuilder(TransactionalDbContext context, params string[] constraints)
        {
            string key = constraints[0];
            string userId = constraints[1];
            //Query items for the user and its id
            return from item in context.Set<TEntity>()
                   where item.Id == key && item.UserId == userId
                   select item;
        }
        ///<inheritdoc/>
        protected override IQueryable<TEntity> GetSingleQueryBuilder(TransactionalDbContext context, TEntity record)
        {
            return this.GetSingleQueryBuilder(context, record.Id, record.UserId);
        }
    }
}
