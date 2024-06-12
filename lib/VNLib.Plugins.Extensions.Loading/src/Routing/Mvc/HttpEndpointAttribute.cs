﻿/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: HttpEndpointAttribute.cs 
*
* HttpEndpointAttribute.cs is part of VNLib.Plugins.Extensions.Loading which is 
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
    /// Attribute to define an http endpoint for a controller. The class 
    /// must be decorated with the <see cref="HttpControllerAttribute"/> attribute
    /// </summary>
    /// <param name="path">The endpoint path</param>
    /// <param name="method">The method (or methods) allowed to be filtered by this endpoint</param>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpEndpointAttribute(string path, HttpMethod method) : Attribute
    {
        /// <summary>
        /// The path of the endpoint
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// The http method of the endpoint. You may set more than one method
        /// for a given endpoint
        /// </summary>
        public HttpMethod Method { get; } = method;
    }
}
