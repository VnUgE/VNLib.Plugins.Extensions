/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginTaskExtensions.cs 
*
* PluginTaskExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading
{
    public static class PluginTaskExtensions
    {
        /// <summary>
        /// Creates a <see cref="PluginTaskObserver"/> that scopes task operations to the specified plugin instance.
        /// </summary>
        /// <param name="plugin">The plugin instance to scope task operations to.</param>
        /// <returns>A new <see cref="PluginTaskObserver"/> for the plugin instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is <see langword="null"/>.</exception>
        public static PluginTaskObserver Tasks(this PluginBase plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            return new(plugin);
        }

        /// <summary>
        /// Provides a fluent API for observing and scheduling plugin lifecycle tasks.
        /// </summary>
        /// <param name="plugin">The plugin instance to scope task operations to.</param>
        public readonly ref struct PluginTaskObserver(PluginBase plugin)
        {
            private readonly PluginBase _plugin = plugin;
           
            private static async Task ObserveWork(PluginBase plugin, Func<Task> asyncTask, int delayMs = 0)
            {
                /*
                 * Motivation:
                 * Sometimes during plugin loading, a plugin may want to asynchronously load
                 * data, where the results are not required to be observed during loading, but 
                 * should not be pending after the plugin is unloaded, as the assembly may be 
                 * unloaded and references collected by the GC.
                 * 
                 * So we can use the plugin's unload cancellation token to observe the results
                 * of a pending async operation 
                 */

                //Test status
                plugin.ThrowIfUnloaded();

                //Optional delay
                await Task.Delay(delayMs)
                    .ConfigureAwait(false);

                //If plugin unloads during delay, bail
                if (plugin.UnloadToken.IsCancellationRequested)
                {
                    return;
                }

                //Run on ts
                Task deferred = Task.Run(asyncTask);

                //Add task to deferred list
                plugin.ObserveTask(deferred);
                try
                {
                    //Await the task results
                    await deferred.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    //Log errors
                    plugin.Log.Error(ex, "Error occurred while observing deferred task");
                }
                finally
                {
                    //Remove task when complete
                    plugin.RemoveObservedTask(deferred);
                }
            }

            /// <summary>
            /// Schedules an asynchronous callback and observes its completion within the plugin's lifecycle.
            /// </summary>
            /// <param name="asyncTask">The asynchronous operation to observe.</param>
            /// <param name="delayMs">An optional startup delay in milliseconds before the operation begins.</param>
            /// <returns>A <see cref="Task"/> that completes when the deferred operation completes.</returns>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            public readonly Task ObserveWork(Func<Task> asyncTask, int delayMs = 0) 
                => ObserveWork(_plugin, asyncTask, delayMs);

            /// <summary>
            /// Schedules <see cref="IAsyncBackgroundWork"/> to begin after a specified delay, observed by the plugin lifecycle.
            /// </summary>
            /// <param name="work">The background work instance to observe.</param>
            /// <param name="delayMs">The delay in milliseconds before dispatching the work item.</param>
            /// <returns>A <see cref="Task"/> that represents the scheduled work.</returns>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            public readonly Task ObserveWork(IAsyncBackgroundWork work, int delayMs = 0)
            {
                PluginBase plugin = _plugin;

                return ObserveWork(() => work.DoWorkAsync(plugin.Log, plugin.UnloadToken), delayMs);
            }

            /// <summary>
            /// Registers a callback to execute when the plugin is unloaded, blocking <see cref="PluginBase.Unload"/> until completion.
            /// </summary>
            /// <param name="callback">The method to invoke when the plugin is unloaded.</param>
            /// <returns>A <see cref="Task"/> that represents the registered unload work.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            public readonly Task RegisterForUnload(Action callback)
            {
                //Test status
                _plugin.ThrowIfUnloaded();
                ArgumentNullException.ThrowIfNull(callback);

                PluginBase plugin = _plugin;

                //Register the task to cause the plugin to wait until the action is completed
                return ObserveWork(() => WaitForUnload(plugin, callback));

                //Wait method
                static async Task WaitForUnload(PluginBase pb, Action callback)
                {
                    //Wait for unload as a task on the threadpool to avoid deadlocks
                    _ = await pb.UnloadToken
                        .WaitHandle
                        .NoSpinWaitAsync(Timeout.Infinite)
                        .ConfigureAwait(false);

                    callback();
                }
            }

            /// <summary>
            /// Configures a service asynchronously on the plugin's scheduler and observes the result within the plugin's lifecycle.
            /// </summary>
            /// <typeparam name="T">The type of the service to configure.</typeparam>
            /// <param name="service">The service instance to configure.</param>
            /// <param name="delayMs">The delay in milliseconds before starting the configuration.</param>
            /// <returns>A <see cref="Task"/> that completes when the configuration operation finishes.</returns>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            public readonly Task ConfigureServiceAsync<T>(T service, int delayMs = 0) where T : IAsyncConfigurable
            {
                PluginBase plugin = _plugin;

                return _plugin
                    .Tasks()
                    .ObserveWork(() => service.ConfigureServiceAsync(plugin), delayMs);
            }
        }
    }
}
