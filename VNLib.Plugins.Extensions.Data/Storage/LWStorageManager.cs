/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: LWStorageManager.cs 
*
* LWStorageManager.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Data. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Data;
using System.Threading;
using System.Data.Common;
using System.Threading.Tasks;

using VNLib.Utils;

using VNLib.Plugins.Extensions.Data.SQL;

namespace VNLib.Plugins.Extensions.Data.Storage
{

    /// <summary>
    /// Provides single table database object storage services
    /// </summary>
    public sealed class LWStorageManager : EnumerableTable<LWStorageDescriptor>
    {
        const int DTO_SIZE = 7;
        const int MAX_DATA_SIZE = 8000;

        //Mssql statments
        private const string GET_DESCRIPTOR_STATMENT_ID_MSQQL = "SELECT TOP 1\r\n[Id],\r\n[UserID],\r\n[Data],\r\n[Created],\r\n[LastModified]\r\nFROM @table\r\nWHERE Id=@Id;";
        private const string GET_DESCRIPTOR_STATMENT_UID_MSQL = "SELECT TOP 1\r\n[Id],\r\n[UserID],\r\n[Data],\r\n[Created],\r\n[LastModified]\r\nFROM @table\r\nWHERE UserID=@UserID;";

        private const string GET_DESCRIPTOR_STATMENT_ID = "SELECT\r\n[Id],\r\n[UserID],\r\n[Data],\r\n[Created],\r\n[LastModified]\r\nFROM @table\r\nWHERE Id=@Id\r\nLIMIT 1;";
        private const string GET_DESCRIPTOR_STATMENT_UID = "SELECT\r\n[Id],\r\n[UserID],\r\n[Data],\r\n[Created],\r\n[LastModified]\r\nFROM @table\r\nWHERE UserID=@UserID\r\nLIMIT 1;";

        private const string CREATE_DESCRIPTOR_STATMENT = "INSERT INTO @table\r\n(UserID,Id,Created,LastModified)\r\nVALUES (@UserID,@Id,@Created,@LastModified);";

        private const string UPDATE_DESCRIPTOR_STATMENT = "UPDATE @table\r\nSET [Data]=@Data\r\n,[LastModified]=@LastModified\r\nWHERE Id=@Id;";
        private const string REMOVE_DESCRIPTOR_STATMENT = "DELETE FROM @table\r\nWHERE Id=@Id";
        private const string CLEANUP_STATEMENT = "DELETE FROM @table\r\nWHERE [created]<@timeout;";
        private const string ENUMERATION_STATMENT = "SELECT [Id],\r\n[UserID],\r\n[Data],\r\n[LastModified],\r\n[Created]\r\nFROM @table;";

        private readonly string GetFromUD;
        private readonly string Cleanup;
        private readonly int keySize;

        /// <summary>
        /// The generator function that is invoked when a new <see cref="LWStorageDescriptor"/> is to 
        /// be created without an explicit id
        /// </summary>
        public Func<string> NewDescriptorIdGenerator { get; init; } = static () => Guid.NewGuid().ToString("N");

        /// <summary>
        /// Creates a new <see cref="LWStorageManager"/> with 
        /// </summary>
        /// <param name="factory">A <see cref="DbConnection"/> factory function that will generate and open connections to a database</param>
        /// <param name="tableName">The name of the table to operate on</param>
        /// <param name="pkCharSize">The maximum number of characters of the DescriptorID and </param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public LWStorageManager(Func<DbConnection> factory, string tableName, int pkCharSize) : base(factory, tableName)
        {
            //Compile statments with specified tableid
            Insert = CREATE_DESCRIPTOR_STATMENT.Replace("@table", tableName);

            //Test connector type to compile MSSQL statments vs Sqlite/Mysql
            using (DbConnection testConnection = GetConnection())
            {
                //Determine if MSSql connections are being used
                bool isMsSql = testConnection.GetType().FullName!.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase);

                if (isMsSql)
                {
                    GetFromUD = GET_DESCRIPTOR_STATMENT_UID_MSQL.Replace("@table", tableName);
                    Select = GET_DESCRIPTOR_STATMENT_ID_MSQQL.Replace("@table", tableName);
                }
                else
                {
                    Select = GET_DESCRIPTOR_STATMENT_ID.Replace("@table", tableName);
                    GetFromUD = GET_DESCRIPTOR_STATMENT_UID.Replace("@table", tableName);
                }
            }

            Update = UPDATE_DESCRIPTOR_STATMENT.Replace("@table", tableName);
            Delete = REMOVE_DESCRIPTOR_STATMENT.Replace("@table", tableName);
            Cleanup = CLEANUP_STATEMENT.Replace("@table", tableName);
            //Set key size
            keySize = pkCharSize;
            //Set default generator
            Enumerate = ENUMERATION_STATMENT.Replace("@table", tableName);
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
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentNullException(nameof(userId));
            }
            //If no override id was specified, generate a new one
            descriptorIdOverride ??= NewDescriptorIdGenerator();
            //Set created time
            DateTimeOffset now = DateTimeOffset.UtcNow;
            //Open a new sql client
            await using DbConnection Database = GetConnection();
            await Database.OpenAsync(cancellation);
            //Setup transaction with repeatable read iso level
            await using DbTransaction transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellation);
            //Create command for text command 
            await using DbCommand cmd = Database.CreateTextCommand(Insert, transaction);
            //add parameters
            _ = cmd.AddParameter("@Id", descriptorIdOverride, DbType.String, keySize);
            _ = cmd.AddParameter("@UserID", userId, DbType.String, keySize);
            _ = cmd.AddParameter("@Created", now, DbType.DateTimeOffset, DTO_SIZE);
            _ = cmd.AddParameter("@LastModified", now, DbType.DateTimeOffset, DTO_SIZE);
            //Prepare operation
            await cmd.PrepareAsync(cancellation);
            //Exec and if successful will return > 0, so we can properly return a descriptor
            int result = await cmd.ExecuteNonQueryAsync(cancellation);
            //Commit transaction
            await transaction.CommitAsync(cancellation);
            if (result <= 0)
            {
                throw new LWDescriptorCreationException("Failed to create the new descriptor because the database retuned an invalid update row count");
            }
            //Rent new descriptor
            LWStorageDescriptor desciptor = new(this)
            {
                DescriptorID = descriptorIdOverride,
                UserID = userId,
                Created = now,
                LastModified = now
            };
            //Set data to null
            await desciptor.PrepareAsync(null);
            return desciptor;
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
            if (string.IsNullOrWhiteSpace(userid))
            {
                throw new ArgumentNullException(nameof(userid));
            }
            //Open a new sql client
            await using DbConnection Database = GetConnection();
            await Database.OpenAsync(cancellation);
            //Setup transaction with repeatable read iso level
            await using DbTransaction transaction = await Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellation);
            //Create a new command based on the command text
            await using DbCommand cmd = Database.CreateTextCommand(GetFromUD, transaction);
            //Add userid parameter
            _ = cmd.AddParameter("@UserID", userid, DbType.String, keySize);
            //Prepare operation
            await cmd.PrepareAsync(cancellation);
            //Get the reader
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellation);
            try
            {
                //Make sure the record was found
                if (!await reader.ReadAsync(cancellation))
                {
                    return null;
                }
                return await GetItemAsync(reader, CancellationToken.None);
            }
            finally
            {
                //Close the reader
                await reader.CloseAsync();
                //Commit the transaction
                await transaction.CommitAsync(cancellation);
            }
        }
        /// <summary>
        /// Attempts to retrieve the <see cref="LWStorageDescriptor"/> for the given descriptor id. The caller is responsible for 
        /// consitancy state of the descriptor
        /// </summary>
        /// <param name="descriptorId">Unique identifier for the descriptor</param>
        /// <returns>The descriptor belonging to the user, or null if not found or error occurs</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<LWStorageDescriptor?> GetDescriptorFromIDAsync(string descriptorId, CancellationToken cancellation = default)
        {
            //Allow null/empty entrys to just return null
            if (string.IsNullOrWhiteSpace(descriptorId))
            {
                throw new ArgumentNullException(nameof(descriptorId));
            }
            //Open a new sql client
            await using DbConnection Database = GetConnection();
            await Database.OpenAsync(cancellation);
            //Setup transaction with repeatable read iso level
            await using DbTransaction transaction = await Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellation);
            //We dont have the routine stored 
            await using DbCommand cmd = Database.CreateTextCommand(Select, transaction);
            //Set userid (unicode length)
            _ = cmd.AddParameter("@Id", descriptorId, DbType.String, keySize);
            //Prepare operation
            await cmd.PrepareAsync(cancellation);
            //Get the reader
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellation);
            try
            {
                if (!await reader.ReadAsync(cancellation))
                {
                    return null;
                }
                return await GetItemAsync(reader, CancellationToken.None);
            }
            finally
            {
                //Close the reader
                await reader.CloseAsync();
                //Commit the transaction
                await transaction.CommitAsync(cancellation);
            }
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
            //Open a new sql client
            await using DbConnection Database = GetConnection();
            await Database.OpenAsync(cancellation);
            //Begin a new transaction
            await using DbTransaction transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellation);
            //Setup the cleanup command for the current database
            await using DbCommand cmd = Database.CreateTextCommand(Cleanup, transaction);
            //Setup timeout parameter as a datetime
            cmd.AddParameter("@timeout", compareTime, DbType.DateTime);
            await cmd.PrepareAsync(cancellation);
            //Exec and if successful will return > 0, so we can properly return a descriptor
            int result = await cmd.ExecuteNonQueryAsync(cancellation);
            //Commit transaction
            await transaction.CommitAsync(cancellation);
            return result;
        }

        /// <summary>
        /// Updates a descriptor's data field
        /// </summary>
        /// <param name="descriptorObj">Descriptor to update</param>
        /// <param name="data">Data string to store to descriptor record</param>
        /// <exception cref="LWStorageUpdateFailedException"></exception>
        internal async Task UpdateDescriptorAsync(object descriptorObj, Stream data)
        {
            LWStorageDescriptor descriptor = (descriptorObj as LWStorageDescriptor)!;
            int result = 0;
            try
            {
                //Open a new sql client
                await using DbConnection Database = GetConnection();
                await Database.OpenAsync();
                //Setup transaction with repeatable read iso level
                await using DbTransaction transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable);
                //Create command for stored procedure
                await using DbCommand cmd = Database.CreateTextCommand(Update, transaction);
                //Add parameters
                _ = cmd.AddParameter("@Id", descriptor.DescriptorID, DbType.String, keySize);
                _ = cmd.AddParameter("@Data", data, DbType.Binary, MAX_DATA_SIZE);
                _ = cmd.AddParameter("@LastModified", DateTime.UtcNow, DbType.DateTime2, DTO_SIZE);
                //Prepare operation
                await cmd.PrepareAsync();
                //exec and store result
                result = await cmd.ExecuteNonQueryAsync();
                //Commit 
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                throw new LWStorageUpdateFailedException("", ex);
            }
            //If the result is 0 then the update failed
            if (result <= 0)
            {
                throw new LWStorageUpdateFailedException($"Descriptor {descriptor.DescriptorID} failed to update", null);
            }
        }
        /// <summary>
        /// Function to remove the specified descriptor 
        /// </summary>
        /// <param name="descriptorObj">The active descriptor to remove from the database</param>
        /// <exception cref="LWStorageRemoveFailedException"></exception>
        internal async Task RemoveDescriptorAsync(object descriptorObj)
        {
            LWStorageDescriptor descriptor = (descriptorObj as LWStorageDescriptor)!;
            try
            {
                //Open a new sql client
                await using DbConnection Database = GetConnection();
                await Database.OpenAsync();
                //Setup transaction with repeatable read iso level
                await using DbTransaction transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable);
                //Create sql command
                await using DbCommand cmd = Database.CreateTextCommand(Delete, transaction);
                //set descriptor id
                _ = cmd.AddParameter("@Id", descriptor.DescriptorID, DbType.String, keySize);
                //Prepare operation
                await cmd.PrepareAsync();
                //Execute (the descriptor my already be removed, as long as the transaction doesnt fail we should be okay)
                _ = await cmd.ExecuteNonQueryAsync();
                //Commit 
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                throw new LWStorageRemoveFailedException("", ex);
            }
        }

        ///<inheritdoc/>
        protected async override Task<LWStorageDescriptor> GetItemAsync(DbDataReader reader, CancellationToken cancellationToken)
        {
            //Open binary stream for the data column
            await using Stream data = reader.GetStream("Data");
            //Create new descriptor
            LWStorageDescriptor desciptor = new(this)
            {
                //Set desctiptor data
                DescriptorID = reader.GetString("Id"),
                UserID = reader.GetString("UserID"),
                Created = reader.GetDateTime("Created"),
                LastModified = reader.GetDateTime("LastModified")
            };
            //Load the descriptor's data
            await desciptor.PrepareAsync(data);
            return desciptor;
        }
        ///<inheritdoc/>
        protected override ValueTask CleanupItemAsync(LWStorageDescriptor item, CancellationToken cancellationToken)
        {
            return item.ReleaseAsync();
        }
    }
}