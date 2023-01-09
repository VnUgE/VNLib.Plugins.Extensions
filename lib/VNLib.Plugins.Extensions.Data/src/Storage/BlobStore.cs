/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: BlobStore.cs 
*
* BlobStore.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Security.Cryptography;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Data.Storage
{

    /// <summary>
    /// Stores <see cref="Blob"/>s to the local file system backed with a single table <see cref="LWStorageManager"/>
    /// that tracks changes
    /// </summary>
    public class BlobStore 
    {
        /// <summary>
        /// The root directory all blob files are stored
        /// </summary>
        public DirectoryInfo RootDir { get; } 
        /// <summary>
        /// The backing store for blob meta-data
        /// </summary>
        protected LWStorageManager BlobTable { get; }
        /// <summary>
        /// Creates a new <see cref="BlobStore"/> that accesses files 
        /// within the specified root directory.
        /// </summary>
        /// <param name="rootDir">The root directory containing the blob file contents</param>
        /// <param name="blobStoreMan">The db backing store</param>
        public BlobStore(DirectoryInfo rootDir, LWStorageManager blobStoreMan)
        {
            RootDir = rootDir;
            BlobTable = blobStoreMan;
        }

        private string GetPath(string fileId) => Path.Combine(RootDir.FullName, fileId);

        /*
         * Creates a repeatable unique identifier for the file 
         * name and allows for lookups
         */
        internal static string CreateFileHash(string fileName)
        {
            throw new NotImplementedException();
            //return ManagedHash.ComputeBase64Hash(fileName, HashAlg.SHA1);
        }

        /// <summary>
        /// Opens an existing <see cref="Blob"/> from the current store
        /// </summary>
        /// <param name="fileId">The id of the file being requested</param>
        /// <param name="access">Access level of the file</param>
        /// <param name="share">The sharing option of the underlying file</param>
        /// <param name="bufferSize">The size of the file buffer</param>
        /// <returns>If found, the requested <see cref="Blob"/>, null otherwise. Throws exceptions if the file is opened in a non-sharable state</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="UndefinedBlobStateException"></exception>
        public virtual async Task<Blob> OpenBlobAsync(string fileId, FileAccess access, FileShare share, int bufferSize = 4096)
        {
            //Get the file's data descriptor
            LWStorageDescriptor fileDescriptor = await BlobTable.GetDescriptorFromIDAsync(fileId);
            //return null if not found
            if (fileDescriptor == null)
            {
                return null;
            }
            try
            {
                string fsSafeName = GetPath(fileDescriptor.DescriptorID);
                //try to open the file
                FileStream file = new(fsSafeName, FileMode.Open, access, share, bufferSize, FileOptions.Asynchronous);
                //Create the new blob
                return new Blob(fileDescriptor, file);
            }
            catch (FileNotFoundException)
            {
                //If the file was not found but the descriptor was, delete the descriptor from the db
                fileDescriptor.Delete();
                //Flush changes
                await fileDescriptor.ReleaseAsync();
                //return null since this is a desync issue and the file technically does not exist
                return null;
            }
            catch
            {
                //Release the descriptor and pass the exception
                await fileDescriptor.ReleaseAsync();
                throw;
            }
        }

        /// <summary>
        /// Creates a new <see cref="Blob"/> for the specified file sharing permissions
        /// </summary>
        /// <param name="name">The name of the original file</param>
        /// <param name="share">The blob sharing permissions</param>
        /// <param name="bufferSize"></param>
        /// <returns>The newly created <see cref="Blob"/></returns>
        /// <exception cref="IoExtensions"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public virtual async Task<Blob> CreateBlobAsync(string name, FileShare share = FileShare.None, int bufferSize = 4096)
        {
            //hash the file name to create a unique id for the file name
            LWStorageDescriptor newFile = await BlobTable.CreateDescriptorAsync(CreateFileHash(name));
            //if the descriptor was not created, return null
            if (newFile == null)
            {
                return null;
            }
            try
            {
                string fsSafeName = GetPath(newFile.DescriptorID);
                //Open/create the new file
                FileStream file = new(fsSafeName, FileMode.OpenOrCreate, FileAccess.ReadWrite, share, bufferSize, FileOptions.Asynchronous);
                //If the file already exists, make sure its zero'd 
                file.SetLength(0);
                //Save the original name of the file
                newFile.SetName(name);
                //Create and return the new blob
                return new Blob(newFile, file);
            }
            catch
            {
                //If an exception occurs, remove the descritor from the db
                newFile.Delete();
                await newFile.ReleaseAsync();
                //Pass exception
                throw;
            }
        }
    }
}