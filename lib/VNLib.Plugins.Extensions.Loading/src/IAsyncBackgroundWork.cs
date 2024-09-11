/*
* Copyright (c) 2024 Vaughn Nugent
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
    /// Represents a low priority or long running work task to be done 
    /// and observed by a loaded plugin
    /// </summary>
    public interface IAsyncBackgroundWork
    {
        /// <summary>
        /// Called when low priority work is ready to be run and its results 
        /// marshaled back to the plugin context
        /// </summary>
        /// <param name="pluginLog">The plugins default log provider</param>
        /// <param name="exitToken">A token that signals when the plugin is unloading and work should be cancelled</param>
        /// <returns>A task representing the low priority work to observed</returns>
        Task DoWorkAsync(ILogProvider pluginLog, CancellationToken exitToken);
    }
}
