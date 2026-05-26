/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: EventManagment.cs 
*
* EventManagment.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Events
{

    /// <summary>
    /// Represents an asynchronous callback for scheduled interval events.
    /// </summary>
    /// <param name="log">The plugin's default log provider.</param>
    /// <param name="pluginExitToken">The plugin's exit token.</param>
    /// <returns>A task that represents the asynchronous work.</returns>
    public delegate Task AsyncSchedulableCallback(ILogProvider log, CancellationToken pluginExitToken);

    /// <summary>
    /// Provides event scheduling extensions for plugins.
    /// </summary>
    public static class EventManagment
    {      

        /// <summary>
        /// Schedules an asynchronous event interval for the current plugin, that is active until canceled or until the plugin unloads.
        /// </summary>
        /// <param name="plugin">The plugin instance to schedule the interval for.</param>
        /// <param name="asyncCallback">The asynchronous callback method to invoke on each interval.</param>
        /// <param name="interval">The time interval between callback invocations.</param>
        /// <param name="immediate"><see langword="true"/> to run the callback immediately; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ObjectDisposedException">The plugin has been disposed.</exception>
        /// <remarks>If exceptions are raised during callback execution, they are written to the plugin's default log provider.</remarks>
        public static void ScheduleInterval(this PluginBase plugin, AsyncSchedulableCallback asyncCallback, TimeSpan interval, bool immediate = false)
        {
            plugin.ThrowIfUnloaded();
            ArgumentNullException.ThrowIfNull(asyncCallback);

            plugin.Log.Verbose("Interval for {t} scheduled on type {rr}", interval, asyncCallback.Target);
            
            //Run interval on plugins bg scheduler
            _ = plugin.ObserveWork(() => RunIntervalOnPluginScheduler(plugin, asyncCallback, interval, immediate));
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

            // Run callback immediately if requested
            if (immediate)
            {
                await RunCallbackAsync(plugin, callback)
                    .ConfigureAwait(false);
            }

            //Timer loop
            while (true)
            {
                try
                {
                    //await delay and wait for plugin cancellation
                    await Task.Delay(interval, plugin.UnloadToken)
                        .ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    //Unload token canceled, exit loop
                    break;
                }

                await RunCallbackAsync(plugin, callback)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Registers an <see cref="IIntervalSchedulable"/> type's event handler for
        /// raising timed interval events.
        /// </summary>
        /// <param name="plugin">The plugin instance to schedule the interval for.</param>
        /// <param name="schedulable">The schedulable instance to register for interval events.</param>
        /// <param name="interval">The time interval between invocations.</param>
        /// <param name="immediate"><see langword="true"/> to run the callback immediately; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ObjectDisposedException">The plugin has been disposed.</exception>
        /// <remarks>If exceptions are raised during callback execution, they are written to the plugin's default log provider.</remarks>
        public static void ScheduleInterval(this PluginBase plugin, IIntervalScheduleable scheduleable, TimeSpan interval, bool immediate = false)
        {
            ArgumentNullException.ThrowIfNull(scheduleable);
            ScheduleInterval(plugin, scheduleable.OnIntervalAsync, interval, immediate);
        }
    }
}
