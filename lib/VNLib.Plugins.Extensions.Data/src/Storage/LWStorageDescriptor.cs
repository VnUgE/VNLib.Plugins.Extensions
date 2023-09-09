/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Text.Json;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using VNLib.Utils;
using VNLib.Utils.Async;

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

        protected override IAsyncResourceStateHandler AsyncHandler { get; }

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

        internal LWStorageDescriptor(IAsyncResourceStateHandler handler, LWStorageEntry entry)
        {
            Entry = entry;
            AsyncHandler = handler;
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
                //Decode and deserialize the data
                return JsonSerializer.Deserialize<Dictionary<string, string>>(Entry.Data, SerializerOptions) ?? new(StringComparer.OrdinalIgnoreCase);
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
            return StringStorage.Value.TryGetValue(key, out string? val) ? JsonSerializer.Deserialize<T>(val, SerializerOptions) : default;
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
                string value = JsonSerializer.Serialize(obj, SerializerOptions);
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
        public override async ValueTask ReleaseAsync(CancellationToken cancellation = default)
        {
            await base.ReleaseAsync(cancellation);

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
        protected override object GetResource()
        {
            //Serlaize the state data and store it in the data entry
            Entry.Data = JsonSerializer.SerializeToUtf8Bytes(StringStorage.Value, SerializerOptions);
            return Entry;
        }
    }
}