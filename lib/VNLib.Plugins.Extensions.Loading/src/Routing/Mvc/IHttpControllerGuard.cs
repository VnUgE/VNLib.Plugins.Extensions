/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IHttpControllerGuard.cs 
*
* IHttpControllerGuard.cs is part of VNLib.Plugins.Extensions.Loading which is 
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

using VNLib.Plugins.Essentials;

namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// Defines a guard that intercepts incoming requests to an 
    /// <see cref="IHttpController"/> before route handlers are invoked.
    /// </summary>
    /// <remarks>
    /// Guards are invoked after the controller's own 
    /// <see cref="IHttpController.PreProcess(HttpEntity)"/> method returns 
    /// <see langword="true" />. Multiple guards may be composed and are 
    /// evaluated in order, short-circuiting on the first guard that returns 
    /// <see langword="false" />.
    /// </remarks>
    public interface IHttpControllerGuard
    {
        /// <summary>
        /// Determines whether the request should continue processing.
        /// </summary>
        /// <param name="entity">The request entity to evaluate.</param>
        /// <returns>
        /// <see langword="true" /> if the request should continue processing; 
        /// otherwise, <see langword="false" /> to reject the request.
        /// </returns>
        bool PreProcess(HttpEntity entity);
    }
}
