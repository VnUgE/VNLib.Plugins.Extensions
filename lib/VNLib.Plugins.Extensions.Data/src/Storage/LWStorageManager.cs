/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: LWStorageManager.cs 
*
* LWStorageManager.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using Microsoft.EntityFrameworkCore;

using VNLib.Utils;
using VNLib.Utils.Async;

namespace VNLib.Plugins.Extensions.Data.Storage
{

    /// <summary>
    /// Provides single table database object storage services
    /// </summary>
    public sealed class LWStorageManager : IAsyncResourceStateHandler
    { 
       
        /// <summary>
        /// The generator function that is invoked when a new <see cref="LWStorageDescriptor"/> is to 
        /// be created without an explicit id
        /// </summary>
        public Func<string> NewDescriptorIdGenerator { get; init; } = static () => Guid.NewGuid().ToString("N");

        private readonly DbContextOptions DbOptions;
        private readonly string TableName;

        private LWStorageContext GetContext() => new(DbOptions, TableName);

        /// <summary>
        /// Creates a new <see cref="LWStorageManager"/> with 
        /// </summary>
        /// <param name="options">The db context options to create database connections with</param>
        /// <param name="tableName">The name of the table to operate on</param>
        /// <exception cref="ArgumentNullException"></exception>
        public LWStorageManager(DbContextOptions options, string tableName)
        {
            DbOptions = options ?? throw new ArgumentNullException(nameof(options));
            TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        /// <summary>
        /// Creates a new <see cref="LWStorageDescriptor"/> fror a given user
        /// </summary>
        /// <param name="userId">Id of user</param>
        /// <param name="descriptorIdOverride">An override to specify the new descriptor's id</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>A new <see cref="LWStorageDescriptor"/> if successfully created, null otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="LWDescriptorCreationException"></exception>
        public async Task<LWStorageDescriptor> CreateDescriptorAsync(string userId, string? descriptorIdOverride = null, CancellationToken cancellation = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            
            //If no override id was specified, generate a new one
            descriptorIdOverride ??= NewDescriptorIdGenerator();

            DateTime createdOrModifedTime = DateTime.UtcNow;

            await using LWStorageContext ctx = GetContext();

            //Make sure the descriptor doesnt exist only by its descriptor id
            if (await ctx.Descriptors.AnyAsync(d => d.Id == descriptorIdOverride, cancellation))
            {
                throw new LWDescriptorCreationException($"A descriptor with id {descriptorIdOverride} already exists");
            }

            //Cache time
            DateTime now = DateTime.UtcNow;

            //Create the new descriptor
            LWStorageEntry entry = new()
            {
                Created = now,
                LastModified = now,
                Id = descriptorIdOverride,
                UserId = userId,
            };

            //Add and save changes
            ctx.Descriptors.Add(entry);

            ERRNO result = await ctx.SaveAndCloseAsync(true, cancellation);

            return result
                ? new LWStorageDescriptor(this, entry)
                : throw new LWDescriptorCreationException("Failed to create descriptor, because changes could not be saved");
        }

        /// <summary>
        /// Attempts to retrieve <see cref="LWStorageDescriptor"/> for a given user-id. The caller is responsible for 
        /// consitancy state of the descriptor
        /// </summary>
        /// <param name="userid">User's id</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The descriptor belonging to the user, or null if not found or error occurs</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<LWStorageDescriptor?> GetDescriptorFromUIDAsync(string userid, CancellationToken cancellation = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userid);

            //Init db
            await using LWStorageContext db = GetContext();           
            
            //Get entry
            LWStorageEntry? entry = await (from s in db.Descriptors
                                           where s.UserId == userid
                                           select s)
                                           .SingleOrDefaultAsync(cancellation);

            await db.SaveAndCloseAsync(true, cancellation);

            //Close transactions and return
            return entry == null ? null : new (this, entry);
        }
        
        /// <summary>
        /// Attempts to retrieve the <see cref="LWStorageDescriptor"/> for the given descriptor id. The caller is responsible for 
        /// consitancy state of the descriptor
        /// </summary>
        /// <param name="descriptorId">Unique identifier for the descriptor</param>
        /// <param name="cancellation">A token to cancel the opreeaiton</param>
        /// <returns>The descriptor belonging to the user, or null if not found or error occurs</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<LWStorageDescriptor?> GetDescriptorFromIDAsync(string descriptorId, CancellationToken cancellation = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(descriptorId);
           
            //Init db
            await using LWStorageContext db = GetContext();           
            
            //Get entry
            LWStorageEntry? entry = await (from s in db.Descriptors
                                           where s.Id == descriptorId
                                           select s)
                                           .SingleOrDefaultAsync(cancellation);

            await db.SaveAndCloseAsync(true, cancellation);

            //Close transactions and return
            return entry == null ? null : new(this, entry);
        }
       
        /// <summary>
        /// Cleanup entries before the specified <see cref="TimeSpan"/>. Entires are store in UTC time
        /// </summary>
        /// <param name="compareTime">Time before <see cref="DateTime.UtcNow"/> to compare against</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The number of entires cleaned</returns>S
        public Task<ERRNO> CleanupTableAsync(TimeSpan compareTime, CancellationToken cancellation = default) => CleanupTableAsync(DateTime.UtcNow.Subtract(compareTime), cancellation);
        
        /// <summary>
        /// Cleanup entries before the specified <see cref="DateTime"/>. Entires are store in UTC time
        /// </summary>
        /// <param name="compareTime">UTC time to compare entires against</param>
        /// <param name="cancellation">A token to cancel the operation</param>
        /// <returns>The number of entires cleaned</returns>
        public async Task<ERRNO> CleanupTableAsync(DateTime compareTime, CancellationToken cancellation = default)
        {
            //Init db
            await using LWStorageContext db = GetContext();

            //Get all expired entires
            LWStorageEntry[] expired = await (from s in db.Descriptors
                                              where s.Created < compareTime
                                              select s)
                                              .ToArrayAsync(cancellation);

            //Delete
            db.Descriptors.RemoveRange(expired);

            //Commit transaction
            return await db.SaveAndCloseAsync(true, cancellation);
        }
       
        async Task IAsyncResourceStateHandler.UpdateAsync(AsyncUpdatableResource resource, object state, CancellationToken cancellation)
        {
            LWStorageEntry entry = (state as LWStorageEntry)!;
            ERRNO result = 0;
            try
            {
                await using LWStorageContext ctx = GetContext();

                //Begin tracking
                ctx.Descriptors.Attach(entry);
                
                //Update modified time
                entry.LastModified = DateTime.UtcNow;

                //Save changes
                result = await ctx.SaveAndCloseAsync(true, cancellation);
            }
            catch (Exception ex)
            {
                throw new LWStorageUpdateFailedException("", ex);
            }
            //If the result is 0 then the update failed
            if (!result)
            {
                throw new LWStorageUpdateFailedException($"Descriptor {entry.Id} failed to update");
            }
        }

        async Task IAsyncResourceStateHandler.DeleteAsync(AsyncUpdatableResource resource, CancellationToken cancellation)
        {
            LWStorageEntry descriptor = (resource as LWStorageDescriptor)!.Entry;
            ERRNO result;
            try
            {
                //Init db
                await using LWStorageContext db = GetContext();

                //Delete the user from the database
                db.Descriptors.Remove(descriptor);

                //Save changes and commit if successful
                result = await db.SaveAndCloseAsync(true, cancellation);
            }
            catch (Exception ex)
            {
                throw new LWStorageRemoveFailedException("", ex);
            }
            if (!result)
            {
                throw new LWStorageRemoveFailedException("Failed to delete the user account because of a database failure, the user may already be deleted");
            }
        }
    }
}