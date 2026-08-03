/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: FileSecretReader.cs 
*
* FileSecretReader.cs is part of VNLib.Plugins.Extensions.Loading which is 
* part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Memory;

namespace VNLib.Plugins.Extensions.Loading.Secrets.Readers
{
    /// <summary>
    /// A built-in secret reader that reads secrets from local files.
    /// Secrets are referenced using the <c>file://</c> scheme prefix in configuration
    /// (e.g., <c>file:///path/to/secret</c>). The scheme prefix is stripped before
    /// the path is passed to this reader.
    /// </summary>
    internal sealed class FileSecretReader : ISecretReader
    {
        /// <inheritdoc/>
        public string Scheme => "file";


        /*
        * Returns null for not-found as common with environment variables or vault when 
        * values are missing. Let permission and IO errors propagate so the user is informed. 
        */

        /// <inheritdoc/>
        public ISecretResult? GetSecret(string secretPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretPath);

            try
            {
                byte[] fileData = File.ReadAllBytes(secretPath);

                return GetResultFromFileData(fileData);
            }           
            catch (FileNotFoundException)
            { }
            catch (DirectoryNotFoundException)
            { }

            return null;
        }

        /// <inheritdoc/>
        public async Task<ISecretResult?> GetSecretAsync(string secretPath, CancellationToken cancellation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretPath);

            try
            {
                byte[] fileData = await File.ReadAllBytesAsync(secretPath, cancellation)
                                    .ConfigureAwait(false);

                return GetResultFromFileData(fileData);
            }
            catch (FileNotFoundException)
            { }
            catch (DirectoryNotFoundException)
            { }

            return null;
        }           

        private static SecretResult GetResultFromFileData(byte[] secretFileData)
        {
            //recover the character data from the file data
            int chars = Encoding.UTF8.GetCharCount(secretFileData);

            char[] secretFileChars = new char[chars];

            Encoding.UTF8.GetChars(secretFileData, secretFileChars);

            //Clear file data buffer
            MemoryUtil.InitializeBlock(secretFileData);

            //Keep the char array as a secret
            return SecretResult.ToSecret(secretFileChars);
        }
    }
}
