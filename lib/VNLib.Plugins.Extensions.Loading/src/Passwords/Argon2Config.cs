/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: Argon2Config.cs 
*
* Argon2Config.cs is part of VNLib.Plugins.Extensions.Loading which 
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
using System.Text.Json.Serialization;

using VNLib.Plugins.Extensions.Loading.Configuration;

namespace VNLib.Plugins.Extensions.Loading.Passwords
{
   
    /// <summary>
    /// Represents configuration options for the Argon2 password hashing algorithm.
    /// </summary>
    internal sealed record class Argon2Config: IOnConfigValidation
    {

        /// <summary>
        /// Gets the hash length parameter for Argon2. The default is 128.
        /// </summary>
        [JsonPropertyName("hash_length")]
        public uint HashLen { get; init; } = 128;

        /// <summary>
        /// Gets the memory cost parameter for Argon2. The default is <see cref="ushort.MaxValue"/>.
        /// </summary>
        [JsonPropertyName("memory_cost")]
        public int MemoryCost { get; init; } = UInt16.MaxValue;

        /// <summary>
        /// Gets the parallelism parameter for Argon2. The default is <see cref="Environment.ProcessorCount"/>.
        /// </summary>
        [JsonPropertyName("parallelism")]
        public int Parallelism { get; init; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets the length of the random salt in bytes. The default is 32.
        /// </summary>
        [JsonPropertyName("salt_length")]
        public int SaltLen { get; init; } = 32;

        /// <summary>
        /// Gets the time cost parameter for Argon2. The default is 4.
        /// </summary>
        [JsonPropertyName("time_cost")]
        public int TimeCost { get; init; } = 4;       

        public void OnValidate()
        {
            // Limit max hash to 16KB to prevent insane misconfigurations.
            Validate.Range(HashLen, 1u, 16384u, "argon2_options:hash_length");

            // Mem cost really doesn't matter as much although would likely exhaust the system during hashing and 
            // really fragment the heap.
            Validate.Range(MemoryCost, 8, Int32.MaxValue, "argon2_options:memory_cost");

            // Limit max threads to 1024 to prevent horrible misconfigurations. The actual max is 2^32-1
            Validate.Range(Parallelism, 1, 1024, "argon2_options:parallelism");

            Validate.Range(SaltLen, 8, Int16.MaxValue, "argon2_options:salt_length");

            // Sane limits for time cost
            Validate.Range(TimeCost, 1, 1024, "argon2_options:time_cost");
        }
    }
}
