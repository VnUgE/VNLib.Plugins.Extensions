/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: SecretResult.cs 
*
* SecretResult.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
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

using VNLib.Utils;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// The result of a secret fetch operation
    /// </summary>
    public sealed class SecretResult : VnDisposeable, ISecretResult
    {
        private readonly char[] _secretChars;

        ///<inheritdoc/>
        public ReadOnlySpan<char> Result => _secretChars;

        private SecretResult(char[] secretChars) => _secretChars = secretChars;

        ///<inheritdoc/>
        protected override void Free() => MemoryUtil.InitializeBlock(_secretChars);

        /// <summary>
        /// Copies the data from the provided string into a new secret result
        /// then erases the original string
        /// </summary>
        /// <param name="result">The secret string to read</param>
        /// <returns>The <see cref="SecretResult"/> wrapper</returns>
        internal static SecretResult ToSecret(string? result)
        {
            if (result == null)
            {
                return new SecretResult([]);
            }

            //Copy string data into a new char array
            SecretResult res = new(result.ToCharArray());
            
            //PrivateStringManager will safely erase the original string if it is able to
            PrivateStringManager.EraseString(result);
           
            return res;
        }

        /// <summary>
        /// Copies the data from the provided span into a new secret result
        /// by allocating a new array internally
        /// </summary>
        /// <param name="secretChars">The array of characters to copy</param>
        /// <returns>The wrapped secret</returns>
        internal static SecretResult ToSecret(ReadOnlySpan<char> secretChars) => new(secretChars.ToArray());

        internal static SecretResult ToSecret(char[] result) => new(result);
    }
}
