/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Validation
* File: ValErrWebMessage.cs 
*
* ValErrWebMessage.cs is part of VNLib.Plugins.Extensions.Validation which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Validation is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Validation is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Validation. If not, see http://www.gnu.org/licenses/.
*/

using System.Collections;
using System.Text.Json.Serialization;

namespace VNLib.Plugins.Extensions.Validation
{
    /// <summary>
    /// Extends the <see cref="WebMessage"/> class with provisions for a collection of validations
    /// </summary>
    public class ValErrWebMessage : WebMessage
    {
        /// <summary>
        /// A collection of error messages to send to clients
        /// </summary>
        [JsonPropertyName("errors")]
        public ICollection Errors { get; set; }
    }
}
