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
    /*
     * This secret reader is a "built-in" reader that attempts
     * to read secrets from local files based on a file path 
     * set by the user in the secrets path.
     * 
     * file://<absolute file path>
     */
    internal sealed class FileSecretReader : ISecretReader
    {
        /// <inheritdoc/>
        public string Scheme => "file";

        /// <inheritdoc/>
        public ISecretResult? GetSecret(string secretPath)
        {
            ArgumentNullException.ThrowIfNull(secretPath);

            byte[] fileData = File.ReadAllBytes(secretPath);

            return GetResultFromFileData(fileData);
        }

        /// <inheritdoc/>
        public async Task<ISecretResult?> GetSecretAsync(string secretPath, CancellationToken cancellation)
        {
            ArgumentNullException.ThrowIfNull(secretPath);

            byte[] fileData = await File.ReadAllBytesAsync(secretPath, cancellation)
                                .ConfigureAwait(false);

            return GetResultFromFileData(fileData);
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
