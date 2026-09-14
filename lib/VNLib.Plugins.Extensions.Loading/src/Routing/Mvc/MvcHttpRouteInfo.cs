/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: MvcHttpRouteInfo.cs
*
* MvcHttpRouteInfo.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger
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

using System.Reflection;

using VNLib.Net.Http;

namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// Information about a single MVC route discovered from a <see cref="IHttpController"/>.
    /// This type holds the handler method that declared a <see cref="HttpStaticRouteAttribute"/>
    /// and it's resolved path and <see cref="HttpMethod"/>
    /// </summary>
    /// <param name="Path">The fully resolved local-path url for the handler</param>
    /// <param name="Method">The desired <see cref="HttpMethod"/> of the route</param>
    /// <param name="Controller">The <see cref="IHttpController"/> the route was declared on</param>
    /// <param name="RouteHandler">The runtime <see cref="MethodInfo"/> information for the method that declared itself as the route handler</param>
    public sealed record MvcHttpRouteInfo (
        string Path,
        HttpMethod Method,
        IHttpController Controller,
        MethodInfo RouteHandler
    );
}
