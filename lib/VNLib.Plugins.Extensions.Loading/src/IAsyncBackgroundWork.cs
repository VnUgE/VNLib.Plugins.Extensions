/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IAsyncBackgroundWork.cs 
*
* IAsyncBackgroundWork.cs is part of VNLib.Plugins.Extensions.Loading which is 
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

using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Represents a low-priority or long-running work task to be performed and observed by a loaded plugin.
    /// </summary>
    public interface IAsyncBackgroundWork
    {
        /// <summary>
        /// Performs low-priority or long-running work when ready, marshaling results back to the plugin context.
        /// </summary>
        /// <param name="pluginLog">The plugin's default log provider.</param>
        /// <param name="exitToken">A token that signals when the plugin is unloading and work should be cancelled.</param>
        /// <returns>A task that represents the low-priority work to be observed.</returns>
        Task DoWorkAsync(ILogProvider pluginLog, CancellationToken exitToken);
    }
}
