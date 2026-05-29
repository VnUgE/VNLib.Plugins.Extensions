/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: HttpRouteProtectionAttribute.cs 
*
* HttpRouteProtectionAttribute.cs is part of VNLib.Plugins.Extensions.Loading which is 
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
using System.Net;

namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// Requires the client connection to satisfy the configured session checks before accessing the endpoint.
    /// </summary>
    /// <remarks>
    /// Session checks include requiring a session to be set, optional session type match, and optional new-session gate.
    /// Authentication enforcement is handled separately by the accounts plugin.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpRouteProtectionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRouteProtectionAttribute"/> class.
        /// </summary>
        public HttpRouteProtectionAttribute()
        { }

        /// <summary>
        /// Gets or sets the required session type identifier string, or <see langword="null" /> to allow any session type.
        /// </summary>
        public string? RequiredSessionTypeId { get; init; }


        /// <summary>
        /// Gets or sets the HTTP status code to return when the client is not authorized.
        /// </summary>
        public HttpStatusCode ErrorCode { get; init; } = HttpStatusCode.Unauthorized;

        /// <summary>
        /// Gets a value that indicates whether connections with newly initialized sessions are allowed.
        /// </summary>
        /// <remarks>
        /// Disallowing new sessions ensures the same session has been reused and verified.
        /// </remarks>
        public bool AllowNewSession { get; init; }
    }
}
