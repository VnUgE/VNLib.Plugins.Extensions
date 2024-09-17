/*
* Copyright (c) 2024 Vaughn Nugent
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

using VNLib.Plugins.Essentials.Accounts;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// When applied to a method, this attribute will require the client to have a valid
    /// authorization in order to access the endpoint.
    /// </summary>
    /// <param name="authLevel">The protection authorization level</param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpRouteProtectionAttribute(AuthorzationCheckLevel authLevel) : Attribute
    {
        /// <summary>
        /// Defines the allowed session types for this endpoint
        /// </summary>
        public SessionType SessionType { get; init; } = SessionType.Web;

        /// <summary>
        /// The minimum authorization level required to access the endpoint
        /// </summary>
        public AuthorzationCheckLevel AuthLevel { get; } = authLevel;

        /// <summary>
        /// The status code to return when the client is not authorized
        /// </summary>
        public HttpStatusCode ErrorCode { get; init; } = HttpStatusCode.Unauthorized;

        /// <summary>
        /// If true allows connections with newly initalized sessions. This is a protection
        /// because allowing new sessions allows connections with ensuring the same 
        /// session has been reused and verified.
        /// </summary>
        public bool AllowNewSession { get; init; }
    }
}
