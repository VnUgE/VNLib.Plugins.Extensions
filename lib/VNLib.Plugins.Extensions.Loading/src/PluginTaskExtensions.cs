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
using System.Collections.Generic;

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
           
            private static Task ObserveWork(PluginBase plugin, Func<Task> asyncTask, int delayMs = 0)
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

                //Test status before delay
                plugin.ThrowIfUnloaded();

                /*
                 * In some cases (like unit testing) The scheduler is very busy and plugins can exit 
                 * very quickly, between the guard above and when the scheduler checks the token again
                 * to begin work. In that condition, the work gets added to the queue, cancelled and 
                 * observed on Unload() which throws before the work had a chance to get scheduled or 
                 * complete. 
                 * 
                 * Im considering this a TOCTOU bug for now and intentionally ignoring the cancellation
                 * token on the Task.Run() call to force the plugin to wait until at least the Task.Delay
                 * call where the token can be observed. We consider Task.Run to be "idempotent" in the
                 * case that once it's called it's up to the work to cancel itself and the task must get
                 * added to the work queue. 
                 * 
                 * Currently, during PluginBase.Unload() takes a snapshot of the pending task list so 
                 * removing it does nothing.
                 * 
                 */
                Task deferred = Task.Run(DoDeferredWork);

                // Add task to deferred list
                plugin.ObserveTask(deferred);

                // Best effort to remove once completed regardless of result
                _ = deferred.ContinueWith(
                    plugin.RemoveObservedTask, 
                    TaskContinuationOptions.ExecuteSynchronously
                );                

                return deferred;

                async Task DoDeferredWork()
                {
                    try
                    {
                        // Optional delay
                        await Task.Delay(delayMs, plugin.UnloadToken)
                            .ConfigureAwait(false);
                      
                        await asyncTask()
                            .ConfigureAwait(false);
                    }
                    // Cancelled because the plugin unloaded while waiting or starting up
                    catch (TaskCanceledException) when (plugin.UnloadToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        //Log errors
                        plugin.Log.Error(ex, "Error occurred while observing deferred task");
                    }
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
            /// Registers a callback to execute when the plugin is unloaded, blocking <see cref="IPlugin.Unload"/> until completion.
            /// </summary>
            /// <param name="callback">The method to invoke when the plugin is unloaded.</param>
            /// <returns>A <see cref="Task"/> that represents the registered unload work.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            public readonly PluginTaskObserver RegisterForUnload(Action callback)
            {
                ArgumentNullException.ThrowIfNull(callback);

                // Get or init unload container to register the callback on
               _plugin.Deps()
                      .GetOrCreateSingleton<OnUnloadContainer>()
                      .Add(callback);

                return this;
            }

            /// <summary>
            /// Registers a callback to execute when the plugin is unloaded, blocking <see cref="IPlugin.Unload"/> until completion.
            /// </summary>
            /// <param name="disposable">The disposable type to dispose on plugin unload.</param>
            /// <returns>A <see cref="Task"/> that represents the registered unload work.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin instance has been unloaded.</exception>
            public readonly PluginTaskObserver RegisterForUnload(IDisposable disposable)
            {
                ArgumentNullException.ThrowIfNull(disposable);
                return RegisterForUnload(disposable.Dispose);
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
                ArgumentNullException.ThrowIfNull(service);

                PluginBase plugin = _plugin;

                return ObserveWork(() => service.ConfigureServiceAsync(plugin), delayMs);
            }

            private sealed class OnUnloadContainer
            {
                private readonly HashSet<Action> _onUnloadActions = [];
                private readonly CancellationTokenRegistration _reg;

                public OnUnloadContainer(PluginBase plugin)
                {
                    // Register this class's cleanup on plugin unload.
                    // Forces all cleanup tasks to execute on the token when cancelled, without
                    // capturing context
                    _reg = plugin.UnloadToken.Register(OnPluginUnload, useSynchronizationContext: false);
                }

                public bool Add(Action instance) => _onUnloadActions.Add(instance);

                public void OnPluginUnload()
                {
                    try
                    {
                        // Best effort call all dispose
                        _onUnloadActions.TryForeach(static dis => dis.Invoke());
                    }
                    finally
                    {
                        _reg.Dispose();
                    }
                }
            }
        }
    }
}
