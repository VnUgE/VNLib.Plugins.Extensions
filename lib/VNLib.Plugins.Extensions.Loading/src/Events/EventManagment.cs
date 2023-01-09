/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: EventManagment.cs 
*
* EventManagment.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Events
{

    /// <summary>
    /// A deletage to form a method signature for shedulable interval callbacks
    /// </summary>
    /// <param name="log">The plugin's default log provider</param>
    /// <param name="pluginExitToken">The plugin's exit token</param>
    /// <returns>A task the represents the asynchronous work</returns>
    public delegate Task AsyncSchedulableCallback(ILogProvider log, CancellationToken pluginExitToken);

    /// <summary>
    /// Provides event schedueling extensions for plugins
    /// </summary>
    public static class EventManagment
    {
        /// <summary>
        /// Schedules an asynchronous event interval for the current plugin, that is active until canceled or until the plugin unloads
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="asyncCallback">An asyncrhonous callback method.</param>
        /// <param name="interval">The event interval</param>
        /// <param name="immediate">A value that indicates if the callback should be run as soon as possible</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <remarks>If exceptions are raised during callback execution, they are written to the plugin's default log provider</remarks>
        public static void ScheduleInterval(this PluginBase plugin, AsyncSchedulableCallback asyncCallback, TimeSpan interval, bool immediate = false)
        {
            plugin.ThrowIfUnloaded();
            
            plugin.Log.Verbose("Interval for {t} scheduled", interval);
            
            //Run interval on plugins bg scheduler
            _ = plugin.DeferTask(() => RunIntervalOnPluginScheduler(plugin, asyncCallback, interval, immediate));
        }

        private static async Task RunIntervalOnPluginScheduler(PluginBase plugin, AsyncSchedulableCallback callback, TimeSpan interval, bool immediate)
        {

            static async Task RunCallbackAsync(PluginBase plugin, AsyncSchedulableCallback callback)
            {
                try
                {
                    //invoke interval callback
                    await callback(plugin.Log, plugin.UnloadToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //unloaded
                    plugin.Log.Verbose("Interval callback canceled due to plugin unload or other event cancellation");
                }
                catch (Exception ex)
                {
                    plugin.Log.Error(ex, "Unhandled exception raised during timer callback");
                }
            }

            //Run callback immediatly if requested
            if (immediate)
            {
                await RunCallbackAsync(plugin, callback);
            }

            //Timer loop
            while (true)
            {
                try
                {
                    //await delay and wait for plugin cancellation
                    await Task.Delay(interval, plugin.UnloadToken);
                }
                catch (TaskCanceledException)
                {
                    //Unload token canceled, exit loop
                    break;
                }

                await RunCallbackAsync(plugin, callback);
            }
        }

        /// <summary>
        /// Registers an <see cref="IIntervalScheduleable"/> type's event handler for 
        /// raising timed interval events
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="scheduleable">The instance to schedule for timeouts</param>
        /// <param name="interval">The timeout interval</param>
        /// <param name="immediate">A value that indicates if the callback should be run as soon as possible</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <remarks>If exceptions are raised during callback execution, they are written to the plugin's default log provider</remarks>
        public static void ScheduleInterval(this PluginBase plugin, IIntervalScheduleable scheduleable, TimeSpan interval, bool immediate = false) => 
            ScheduleInterval(plugin, scheduleable.OnIntervalAsync, interval, immediate);
    }
}
