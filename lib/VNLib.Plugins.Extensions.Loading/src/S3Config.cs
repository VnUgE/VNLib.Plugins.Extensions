/*
* Copyright (c) 2024 Vaughn Nugent
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

using System.Text.Json.Serialization;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// A common json-serializable configuration for S3 storage
    /// in an attempt to unify S3 configuration.
    /// </summary>
    public class S3Config
    {
        [JsonPropertyName("server_address")]
        public string? ServerAddress { get; init; }

        [JsonPropertyName("access_key")]
        public string? ClientId { get; init; }

        [JsonPropertyName("bucket")]
        public string? BaseBucket { get; init; }

        [JsonPropertyName("use_ssl")]
        public bool? UseSsl { get; init; }

        [JsonPropertyName("region")]
        public string? Region { get; init; }
    }
}
