/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: Argon2HashProvider.cs 
*
* Argon2HashProvider.cs is part of VNLib.Plugins.Extensions.Loading which 
* is part of the larger VNLib collection of libraries and utilities.
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
using System.Security.Cryptography;

using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Plugins.Essentials.Users;

// TODO: TEMPORARY!
using VNLib.Plugins.Essentials.Accounts;

/*
 * Some stuff to note
 * 
 * Functions have explicit parameters to avoid accidental buffer mixup
 * when calling nested/overload functions. Please keep it that way for now
 * I really want to avoid a whoopsie in password hashing.
 */


namespace VNLib.Plugins.Extensions.Loading.Passwords
{

    /// <summary>
    /// Provides a structured password hashing system backed by the <see cref="VnArgon2"/> library.
    /// </summary>
    /// <remarks>Uses fixed time comparison for hash verification.</remarks>
    internal sealed class Argon2HashProvider : IPasswordHashingProvider
    {
        private readonly IUnmanagedHeap _bufferHeap;
        private readonly ISecretProvider? _secret;
        private readonly IArgon2Library _argon2;
        private readonly Argon2Config _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="Argon2HashProvider"/> class.
        /// </summary>
        /// <param name="library">The Argon2 library implementation.</param>
        /// <param name="setup">The Argon2 configuration.</param>
        /// <param name="bufferHeap">The unmanaged heap used for buffer allocations.</param>
        /// <param name="secret">The secret provider for pepper, or <see langword="null"/> if no pepper is used.</param>
        internal Argon2HashProvider(
            IArgon2Library library,
            Argon2Config setup,
            IUnmanagedHeap bufferHeap,
            ISecretProvider? secret
        )
        {
            _argon2 = library ?? throw new ArgumentNullException(nameof(library));
            _config = setup ?? throw new ArgumentNullException(nameof(setup));
            _bufferHeap = bufferHeap ?? throw new ArgumentNullException(nameof(bufferHeap));
            _secret = secret;
        }

        private Argon2CostParams GetCostParams()
        {
            checked
            {
                return new Argon2CostParams
                {
                    MemoryCost  = (uint)_config.MemoryCost,
                    TimeCost    = (uint)_config.TimeCost,
                    Parallelism = (uint)_config.Parallelism
                };
            }
        }

        ///<inheritdoc/>
        ///<exception cref="VnArgon2Exception"></exception>
        ///<exception cref="VnArgon2PasswordFormatException"></exception>
        public bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password)
        {
            if (passHash.IsEmpty || password.IsEmpty)
            {
                return false;
            }

            // Use secret buffer if not null
            return _argon2.Verify2id(
                rawPass: password,
                hash: passHash,
                secret: GetSecretOrDefault()
            );
        }

        /// <summary>
        /// Verifies a password against its hash using the Argon2 algorithm.
        /// </summary>
        /// <remarks>Partially exposes the Argon2 api.</remarks>
        /// <param name="hash">The previously hashed password.</param>
        /// <param name="salt">The salt used to hash the original password.</param>
        /// <param name="password">The password to verify.</param>
        /// <returns><see langword="true" /> if the password matches the hash; otherwise, <see langword="false" />.</returns>
        /// <exception cref="VnArgon2Exception">An error occurred during Argon2 hash computation.</exception>
        public bool Verify(ReadOnlySpan<byte> hash, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> password)
        {
            using UnsafeMemoryHandle<byte> hashBuf = MemoryUtil.UnsafeAlloc<byte>(_bufferHeap, hash.Length, zero: true);

            Hash(
                password: password,
                salt: salt,
                hashOutput: hashBuf.Span
            );

            //Compare the hashed password to the specified hash and return results
            return CryptographicOperations.FixedTimeEquals(hash, hashBuf.Span);
        }

        /// <inheritdoc/>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        public PrivateString Hash(ReadOnlySpan<char> password)
        {
            using UnsafeMemoryHandle<byte> saltBuf = GetSaltBuffer();

            // Salt is just cryptographically secure random data.
            RandomHash.GetRandomBytes(saltBuf.Span);

            try
            {
                Argon2CostParams costParams = GetCostParams();

                return (PrivateString)_argon2.Hash2id(
                    password: password,
                    salt: saltBuf.AsSpan(0, _config.SaltLen),  // Pass actual salt not the entire overallocated buffer
                    secret: GetSecretOrDefault(),
                    costParams: in costParams,
                    hashLen: _config.HashLen
                );
            }
            finally
            {
                MemoryUtil.InitializeBlock(
                    ref saltBuf.GetReference(),
                    saltBuf.IntLength
                );
            }
        }

        /// <inheritdoc/>
        /// <exception cref="VnArgon2Exception"></exception>
        /// <returns>A <see cref="PrivateString"/> of the hashed and encoded password</returns>
        public PrivateString Hash(ReadOnlySpan<byte> password)
        {
            using UnsafeMemoryHandle<byte> saltBuf = GetSaltBuffer();

            // Salt is just cryptographically secure random data.
            RandomHash.GetRandomBytes(saltBuf.Span);

            try
            {
                Argon2CostParams costParams = GetCostParams();

                // Pass secret buffer if not null, otherwise pass default
                return (PrivateString)_argon2.Hash2id(
                    password: password,
                    salt: saltBuf.AsSpan(0, _config.SaltLen),   // Pass actual salt not the entire overallocated buffer
                    secret: GetSecretOrDefault(),
                    costParams: in costParams,
                    hashLen: _config.HashLen
                );
            }
            finally
            {
                MemoryUtil.InitializeBlock(
                    ref saltBuf.GetReference(),
                    saltBuf.IntLength
                );
            }
        }

        /// <summary>
        /// Hashes the specified password with the initialized pepper and writes the raw hash output to the specified buffer.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <param name="salt">The salt to hash the password with.</param>
        /// <param name="hashOutput">The output buffer to store the hashed password. The exact length of this buffer is the hash size.</param>
        /// <exception cref="VnArgon2Exception">An error occurred during Argon2 hash computation.</exception>
        public void Hash(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, Span<byte> hashOutput)
        {
            Argon2CostParams costParams = GetCostParams();

            _argon2.Hash2id(
                password: password,
                salt: salt,
                secret: GetSecretOrDefault(),
                rawHashOutput: hashOutput,
                costParams: in costParams
            );
        }

        ///<inheritdoc/>
        ///<exception cref="VnArgon2Exception"></exception>
        public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput)
        {
            using UnsafeMemoryHandle<byte> saltBuf = GetSaltBuffer();

            // Salt is just cryptographically secure random data.
            RandomHash.GetRandomBytes(saltBuf.Span);

            try
            {
                Hash(
                    password: password,
                    salt: saltBuf.AsSpan(0, _config.SaltLen),  // Pass actual salt not the entire overallocated buffer
                    hashOutput: hashOutput
                );
            }
            finally
            {
                MemoryUtil.InitializeBlock(
                    ref saltBuf.GetReference(),
                    saltBuf.IntLength
                );
            }

            return hashOutput.Length;
        }

        /// <summary>
        /// Verifies a password against its hash.
        /// </summary>
        /// <remarks>This overload is not supported. Use <see cref="Verify(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> to specify the salt that was used to hash the original password.</remarks>
        /// <param name="passHash">The previously hashed password.</param>
        /// <param name="password">The password to verify.</param>
        /// <exception cref="NotSupportedException">This method is not supported.</exception>
        public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password)
            => throw new NotSupportedException();

        /*
         * Allocates a constant size buffer from the supplied heap for storing the salt. 
         * The buffer is zeroed out and should be cleared after use.
         * 
         * Rounded up to the nearest page to help obscure the actual size of the salt and 
         * make it less identifiable in memory dumps.
         */
        private UnsafeMemoryHandle<byte> GetSaltBuffer() 
            => MemoryUtil.UnsafeAllocNearestPage<byte>(_bufferHeap, _config.SaltLen, zero: true);

        private ReadOnlySpan<byte> GetSecretOrDefault() => _secret != null ? _secret.Secret : default;
    }
}
