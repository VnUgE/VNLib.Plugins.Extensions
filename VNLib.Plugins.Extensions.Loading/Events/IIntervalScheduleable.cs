/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IIntervalScheduleable.cs 
*
* IIntervalScheduleable.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Loading. If not, see http://www.gnu.org/licenses/.
*/

using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// Exposes a type for asynchronous event schelueling
    /// </summary>
    public interface IIntervalScheduleable
    {
        /// <summary>
        /// A method that is called when the interval time has elapsed
        /// </summary>
        /// <param name="log">The plugin default log provider</param>
        /// <param name="cancellationToken">A token that may cancel an operations if the plugin becomes unloaded</param>
        /// <returns>A task that resolves when the async operation completes</returns>
        Task OnIntervalAsync(ILogProvider log, CancellationToken cancellationToken);
    }
}
