/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IHttpController.cs 
*
* IHttpController.cs is part of VNLib.Plugins.Extensions.Loading which is 
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
using VNLib.Plugins.Essentials.Endpoints;

namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// The base interface type for all http controllers, which
    /// are responsible for handling http requests.
    /// </summary>
    public interface IHttpController
    {
        /// <summary>
        /// Gets the protection settings for all routes within
        /// this controller.
        /// </summary>
        /// <returns>The endpoint protection settings for all routes</returns>
        ProtectionSettings GetProtectionSettings();

        /// <summary>
        /// Allows pre-processing of the http entity before
        /// the request is processed by routing handlers
        /// </summary>
        /// <param name="entity">The request entity to pre-process</param>
        /// <returns>A value that indicates if the request should continue processing or return</returns>
        virtual bool PreProccess(HttpEntity entity) => true;
    }
  
}
