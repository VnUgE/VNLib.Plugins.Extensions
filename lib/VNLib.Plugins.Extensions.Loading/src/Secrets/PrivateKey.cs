/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PrivateKey.cs 
*
* PrivateKey.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Text;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// A container for a PKSC#8 encoed private key
    /// </summary>
    public sealed class PrivateKey : VnDisposeable
    {
        private readonly byte[] _utf8RawData;
        
        /// <summary>
        /// Decodes the PKCS#8 encoded private key from a secret, as an EC private key
        /// and recovers the ECDsa algorithm from the key
        /// </summary>
        /// <returns>The <see cref="ECDsa"/> algoritm from the private key</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="CryptographicException"></exception>
        public ECDsa GetECDsa()
        {
            //Alloc buffer
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(_utf8RawData.Length);

            //Get base64 bytes from utf8
            ERRNO count = VnEncoding.Base64UrlDecode(_utf8RawData, buffer.Span);
            
            //Parse the private key
            ECDsa alg = ECDsa.Create();
            
            alg.ImportPkcs8PrivateKey(buffer.Span[..(int)count], out _);
            
            //Wipe the buffer
            MemoryUtil.InitializeBlock(buffer.Span);
            
            return alg;
        }

        /// <summary>
        /// Decodes the PKCS#8 encoded private key from a secret, as an RSA private key
        /// </summary>
        /// <returns>The <see cref="RSA"/> algorithm from the private key</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="CryptographicException"></exception>
        public RSA GetRSA()
        {
            //Alloc buffer
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(_utf8RawData.Length);
            
            //Get base64 bytes from utf8
            ERRNO count = VnEncoding.Base64UrlDecode(_utf8RawData, buffer.Span);

            //Parse the private key
            RSA alg = RSA.Create();
            
            alg.ImportPkcs8PrivateKey(buffer.Span[..(int)count], out _);
            
            //Wipe the buffer
            MemoryUtil.InitializeBlock(buffer.Span);
            
            return alg;
        }

        internal PrivateKey(ISecretResult secret)
        {
            //Alloc and get utf8
            byte[] buffer = new byte[secret.Result.Length];
            
            int count = Encoding.UTF8.GetBytes(secret.Result, buffer);

            //Verify length
            if(count != buffer.Length)
            {
                throw new FormatException("UTF8 deocde failed");
            }
            
            //Store
            _utf8RawData = buffer;
        }

        protected override void Free()
        {
            MemoryUtil.InitializeBlock(_utf8RawData.AsSpan());
        }
    }
}
