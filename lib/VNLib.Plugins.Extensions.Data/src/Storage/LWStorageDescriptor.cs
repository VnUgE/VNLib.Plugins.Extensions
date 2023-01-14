/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: LWStorageDescriptor.cs 
*
* LWStorageDescriptor.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.IO;
using System.Text.Json;
using System.Collections;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using VNLib.Utils;
using VNLib.Utils.Async;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Extensions.Data.Storage
{
    /// <summary>
    /// Represents an open storage object, that when released or disposed, will flush its changes to the underlying table 
    /// for which this descriptor represents
    /// </summary>
    public sealed class LWStorageDescriptor : AsyncUpdatableResource, IObjectStorage, IEnumerable<KeyValuePair<string, string>>, IIndexable<string, string>
    {

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.Strict,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyFields = true,
            DefaultBufferSize = Environment.SystemPageSize,
        };

        
        internal LWStorageEntry Entry { get; }
        
        private readonly Lazy<Dictionary<string, string>> StringStorage;

        /// <summary>
        /// The currnt descriptor's identifier string within its backing table. Usually the primary key.
        /// </summary>
        public string DescriptorID => Entry.Id;
        /// <summary>
        /// The identifier of the user for which this descriptor belongs to
        /// </summary>
        public string UserID => Entry.UserId!;
        /// <summary>
        /// The <see cref="DateTime"/> when the descriptor was created
        /// </summary>
        public DateTimeOffset Created => Entry.Created;
        /// <summary>
        /// The last time this descriptor was updated
        /// </summary>
        public DateTimeOffset LastModified => Entry.LastModified;

        ///<inheritdoc/>
        protected override AsyncUpdateCallback UpdateCb { get; }
        ///<inheritdoc/>
        protected override AsyncDeleteCallback DeleteCb { get; }
        ///<inheritdoc/>
        protected override JsonSerializerOptions JSO => SerializerOptions;

        internal LWStorageDescriptor(LWStorageManager manager, LWStorageEntry entry)
        {
            Entry = entry;
            UpdateCb = manager.UpdateDescriptorAsync;
            DeleteCb = manager.RemoveDescriptorAsync;
            StringStorage = new(OnStringStoreLoad);
        }

        internal Dictionary<string, string> OnStringStoreLoad()
        {
            if(Entry.Data == null || Entry.Data.Length == 0)
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                //Calc and alloc decode buffer
                int bufferSize = (int)(Entry.Data.Length * 1.75);
                
                using UnsafeMemoryHandle<byte> decodeBuffer = MemoryUtil.UnsafeAlloc<byte>(bufferSize);

                //Decode and deserialize the data
                return BrotliDecoder.TryDecompress(Entry.Data, decodeBuffer, out int written)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(Entry.Data, SerializerOptions) ?? new(StringComparer.OrdinalIgnoreCase)
                    : throw new InvalidDataException("Failed to decompress data");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public T? GetObject<T>(string key)
        {
            Check();
            //De-serialize and return object
            return StringStorage.Value.TryGetValue(key, out string? val) ? val.AsJsonObject<T>(SerializerOptions) : default;
        }
        
        /// <inheritdoc/>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void SetObject<T>(string key, T obj)
        {
            //Remove the object from storage if its null
            if (obj == null)
            {
                SetStringValue(key, null);
            }
            else
            {
                //Serialize the object to a string
                string value = obj.ToJsonString(SerializerOptions)!;
                //Attempt to store string in storage
                SetStringValue(key, value);
            }
        }

        
        /// <summary>
        /// Gets a string value from string storage matching a given key
        /// </summary>
        /// <param name="key">Key for storage</param>
        /// <returns>Value associaetd with key if exists, <see cref="string.Empty"/> otherwise</returns>
        /// <exception cref="ArgumentNullException">If key is null</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public string GetStringValue(string key)
        {
            Check();
            return StringStorage.Value.TryGetValue(key, out string? val) ? val : string.Empty;
        }

        /// <summary>
        /// Creates, overwrites, or removes a string value identified by key.
        /// </summary>
        /// <param name="key">Entry key</param>
        /// <param name="value">String to store or overwrite, set to null or string.Empty to remove a property</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentNullException">If key is null</exception>
        public void SetStringValue(string key, string? value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }
            Check();
            //If the value is null, see if the the properties are null
            if (string.IsNullOrWhiteSpace(value))
            {
                //If the value is null and properies exist, remove the entry
                StringStorage.Value.Remove(key);
                Modified |= true;
            }
            else
            {
                //Set the value
                StringStorage.Value[key] = value;
                //Set modified flag
                Modified |= true;
            }
        }
        
        /// <summary>
        /// Gets or sets a string value from string storage matching a given key
        /// </summary>
        /// <param name="key">Key for storage</param>
        /// <returns>Value associaetd with key if exists, <seealso cref="string.Empty "/> otherwise</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentNullException">If key is null</exception>
        public string this[string key]
        {
            get => GetStringValue(key);
            set => SetStringValue(key, value);
        }

        /// <summary>
        /// Flushes all pending changes to the backing store asynchronously
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public ValueTask WritePendingChangesAsync()
        {
            Check();
            return Modified ? (new(FlushPendingChangesAsync())) : ValueTask.CompletedTask;
        }

        ///<inheritdoc/>
        public override async ValueTask ReleaseAsync()
        {
            await base.ReleaseAsync();
            //Cleanup dict on exit
            if (StringStorage.IsValueCreated)
            {
                StringStorage.Value.Clear();
            }
        }

        ///<inheritdoc/>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => StringStorage.Value.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        ///<inheritdoc/>
        protected override object GetResource() => StringStorage.Value;
    }
}