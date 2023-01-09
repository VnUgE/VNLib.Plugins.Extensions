/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: Blob.cs 
*
* Blob.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Threading.Tasks;
using System.Runtime.Versioning;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Async;

namespace VNLib.Plugins.Extensions.Data.Storage
{
    /// <summary>
    /// Represents a stream of arbitrary binary data
    /// </summary>
    public class Blob : BackingStream<FileStream>, IObjectStorage, IAsyncExclusiveResource
    {         
        protected readonly LWStorageDescriptor Descriptor;

        /// <summary>
        /// The current blob's unique ID
        /// </summary>
        public string BlobId => Descriptor.DescriptorID;        
        /// <summary>
        /// A value indicating if the <see cref="Blob"/> has been modified
        /// </summary>
        public bool Modified { get; protected set; }
        /// <summary>
        /// A valid indicating if the blob was flagged for deletiong
        /// </summary>
        public bool Deleted { get; protected set; }

        /// <summary>
        /// The name of the file (does not change the actual file system name)
        /// </summary>
        public string Name
        {
            get => Descriptor.GetName();
            set => Descriptor.SetName(value);
        }
        /// <summary>
        /// The UTC time the <see cref="Blob"/> was last modified
        /// </summary>
        public DateTimeOffset LastWriteTimeUtc => Descriptor.LastModified;
        /// <summary>
        /// The UTC time the <see cref="Blob"/> was created
        /// </summary>
        public DateTimeOffset CreationTimeUtc => Descriptor.Created;

        internal Blob(LWStorageDescriptor descriptor, in FileStream file)
        {
            this.Descriptor = descriptor;
            base.BaseStream = file;
        }

        /// <summary>
        /// Prevents other processes from reading from or writing to the <see cref="Blob"/>
        /// </summary>
        /// <param name="position">The begining position of the range to lock</param>
        /// <param name="length">The range to be locked</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [UnsupportedOSPlatform("ios")]
        [UnsupportedOSPlatform("macos")]
        [UnsupportedOSPlatform("tvos")]
        public void Lock(long position, long length) => BaseStream.Lock(position, length);
        /// <summary>
        /// Prevents other processes from reading from or writing to the <see cref="Blob"/>
        /// </summary>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [UnsupportedOSPlatform("ios")]
        [UnsupportedOSPlatform("macos")]
        [UnsupportedOSPlatform("tvos")]
        public void Lock() => BaseStream.Lock(0, BaseStream.Length);
        /// <summary>
        /// Allows access by other processes to all or part of the <see cref="Blob"/> that was previously locked
        /// </summary>
        /// <param name="position">The begining position of the range to unlock</param>
        /// <param name="length">The range to be unlocked</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [UnsupportedOSPlatform("ios")]
        [UnsupportedOSPlatform("macos")]
        [UnsupportedOSPlatform("tvos")]
        public void Unlock(long position, long length) => BaseStream.Unlock(position, length);
        /// <summary>
        /// Allows access by other processes to the entire <see cref="Blob"/>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [UnsupportedOSPlatform("ios")]
        [UnsupportedOSPlatform("macos")]
        [UnsupportedOSPlatform("tvos")]
        public void Unlock() => BaseStream.Unlock(0, BaseStream.Length);
        ///<inheritdoc/>
        public override void SetLength(long value)
        {
            base.SetLength(value);
            //Set modified flag
            Modified |= true;
        }

        /*
         * Capture on-write calls to set the modified flag
         */
        ///<inheritdoc/>
        protected override void OnWrite(int count) => Modified |= true;

        T IObjectStorage.GetObject<T>(string key) => ((IObjectStorage)Descriptor).GetObject<T>(key);
        void IObjectStorage.SetObject<T>(string key, T obj) => ((IObjectStorage)Descriptor).SetObject(key, obj);

        public string this[string index]
        {
            get => Descriptor[index];
            set => Descriptor[index] = value;
        }


        /// <summary>
        /// Marks the file for deletion and will be deleted when the <see cref="Blob"/> is disposed
        /// </summary>
        public void Delete()
        {
            //Set deleted flag
            Deleted |= true;
            Descriptor.Delete();
        }
        ///<inheritdoc/>
        public bool IsReleased => Descriptor.IsReleased;
       

        /// <summary>
        /// <para>
        /// If the <see cref="Blob"/> was opened with writing enabled, 
        /// and file was modified, changes are flushed to the backing store
        /// and the stream is set to readonly. 
        /// </para>
        /// <para>
        /// If calls to this method succeed the stream is placed into a read-only mode
        /// which will cause any calls to Write to throw a <see cref="NotSupportedException"/>
        /// </para>
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that may be awaited until the operation completes</returns>
        /// <remarks>
        /// This method may be called to avoid flushing changes to the backing store
        /// when the <see cref="Blob"/> is disposed (i.e. lifetime is manged outside of the desired scope)
        /// </remarks>
        /// <exception cref="IOException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async ValueTask FlushChangesAndSetReadonlyAsync()
        {
            if (Deleted)
            {
                throw new InvalidOperationException("The blob has been deleted and must be closed!");
            }
            if (Modified)
            {
                //flush the base stream
                await BaseStream.FlushAsync();
                //Update the file length in the store
                Descriptor.SetLength(BaseStream.Length);
            }
            //flush changes, this will cause the dispose method to complete synchronously when closing
            await Descriptor.WritePendingChangesAsync();
            //Clear modified flag
            Modified = false;
            //Set to readonly mode
            base.ForceReadOnly = true;
        }
     

        /*
         * Override the dispose async to manually dispose the 
         * base stream and avoid the syncrhonous (OnClose) 
         * method and allow awaiting the descriptor release
         */
        ///<inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
            GC.SuppressFinalize(this);
        }
        ///<inheritdoc/>
        public async ValueTask ReleaseAsync()
        {
            try
            {
                //Check for deleted
                if (Deleted)
                {
                    //Dispose the base stream explicitly
                    await BaseStream.DisposeAsync();
                    //Try to delete the file
                    File.Delete(BaseStream.Name);
                }
                //Check to see if the file was modified
                else if (Modified)
                {
                    //Set the file size in bytes
                    Descriptor.SetLength(BaseStream.Length);
                }
            }
            catch
            {
                //Set the error flag
                Descriptor.IsError(true);
                //propagate the exception
                throw;
            }
            finally
            {
                //Dispose the stream
                await BaseStream.DisposeAsync();
                //Release the descriptor
                await Descriptor.ReleaseAsync();
            }
        }
    }
}