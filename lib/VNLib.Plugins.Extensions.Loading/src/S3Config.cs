/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: S3Config.cs 
*
* S3Config.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Text.Json.Serialization;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// Provides a JSON-serializable configuration for S3-compatible storage.
    /// </summary>
    [Obsolete("S3 built-in config support is being deprecated")]
    public class S3Config
    {
        /// <summary>
        /// Gets or sets the S3 server address.
        /// </summary>
        [JsonPropertyName("server_address")]
        public string? ServerAddress { get; init; }

        /// <summary>
        /// Gets or sets the S3 access key identifier.
        /// </summary>
        [JsonPropertyName("access_key")]
        public string? ClientId { get; init; }

        /// <summary>
        /// Gets or sets the base bucket name.
        /// </summary>
        [JsonPropertyName("bucket")]
        public string? BaseBucket { get; init; }

        /// <summary>
        /// Gets a value that indicates whether to use SSL for the S3 connection.
        /// </summary>
        [JsonPropertyName("use_ssl")]
        public bool? UseSsl { get; init; }

        /// <summary>
        /// Gets or sets the S3 region.
        /// </summary>
        [JsonPropertyName("region")]
        public string? Region { get; init; }
    }
}
