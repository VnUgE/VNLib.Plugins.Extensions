/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IAsyncConfigurable.cs 
*
* IAsyncConfigurable.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Allows for asynchronous service configuration during service creation, that 
    /// will be observed on the plugin
    /// </summary>
    public interface IAsyncConfigurable
    {
        /// <summary>
        /// Configures the service for use. Exceptions will be written to the 
        /// plugin's default log provider
        /// </summary>
        /// <returns>A task that completes when the service has been loaded successfully</returns>
        Task ConfigureServiceAsync(PluginBase plugin);
    }
}
