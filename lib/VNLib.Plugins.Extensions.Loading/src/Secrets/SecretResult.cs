/*
* Copyright (c) 2023 Vaughn Nugent
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
using VNLib.Utils.Extensions;
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


        internal SecretResult(ReadOnlySpan<char> value) : this(value.ToArray())
        { }

        private SecretResult(char[] secretChars)
        {
            _secretChars = secretChars;
        }


        ///<inheritdoc/>
        protected override void Free()
        {
            MemoryUtil.InitializeBlock(_secretChars);
        }

        internal static SecretResult ToSecret(string? result)
        {
            SecretResult res = new(result.AsSpan());
            MemoryUtil.UnsafeZeroMemory<char>(result);
            return res;
        }

        internal static SecretResult ToSecret(char[] result) => new(result);
    }
}
