/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: HttpStaticRouteAttribute.cs 
*
* HttpStaticRouteAttribute.cs is part of VNLib.Plugins.Extensions.Loading which is 
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

using VNLib.Net.Http;

namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{

    /// <summary>
    /// Defines a static HTTP endpoint for a controller.
    /// </summary>
    /// <remarks>
    /// A static route is configured at startup and does not perform dynamic pattern matching.
    /// Values in the <see cref="Path"/> property may include configuration substitution in the
    /// form of <c>{{ var_name }}</c>, which will be replaced at startup with the configured value.
    /// </remarks>
    /// <param name="path">The static route path, which may include configuration substitution variables.</param>
    /// <param name="method">The HTTP method or methods allowed for this endpoint.</param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpStaticRouteAttribute(string path, HttpMethod method) : Attribute
    {
        /// <summary>
        /// Gets the path of the endpoint.
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// Gets the HTTP method of the endpoint. More than one method may be set for a given endpoint.
        /// </summary>
        public HttpMethod Method { get; } = method;
    }
}
