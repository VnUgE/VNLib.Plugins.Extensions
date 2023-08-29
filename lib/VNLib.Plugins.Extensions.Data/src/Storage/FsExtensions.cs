/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: FsExtensions.cs 
*
* FsExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part 
* of the larger VNLib collection of libraries and utilities.
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

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Data.Storage
{
    /// <summary>
    /// Contains filesystem extension methods
    /// </summary>
    public static class FsExtensions
    {
        /// <summary>
        /// Creates a new scope for the given filesystem. All operations will be offset by the given path
        /// within the parent filesystem.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="offsetPath">The base path to prepend to all requests</param>
        /// <returns>A new <see cref="ISimpleFilesystem"/> with a new filesystem directory scope</returns>
        public static ISimpleFilesystem CreateNewScope(this ISimpleFilesystem fs, string offsetPath) => new FsScope(fs, offsetPath);

        private sealed record class FsScope(ISimpleFilesystem Parent, string OffsetPath) : ISimpleFilesystem
        {
            public Task DeleteFileAsync(string filePath, CancellationToken cancellation)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.DeleteFileAsync(path, cancellation);
            }

            public string GetExternalFilePath(string filePath)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.GetExternalFilePath(path);
            }

            public Task<long> ReadFileAsync(string filePath, Stream output, CancellationToken cancellation)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.ReadFileAsync(path, output, cancellation);
            }

            public Task SetFileAsync(string filePath, Stream data, string contentType, CancellationToken cancellation)
            {
                string path = Path.Combine(OffsetPath, filePath);
                return Parent.SetFileAsync(path, data, contentType, cancellation);
            }
        }
    }
}
