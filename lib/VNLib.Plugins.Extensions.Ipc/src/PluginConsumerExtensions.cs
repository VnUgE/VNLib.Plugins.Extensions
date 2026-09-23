/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc
* File: PluginConsumerExtensions.cs 
*
* PluginConsumerExtensions.cs is part of VNLib.Plugins.Extensions.Ipc which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Ipc is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Ipc is distributed in the hope that it will be useful,
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

using VNLib.Utils.Async;
using VNLib.Utils.Extensions;
using VNLib.Utils.Logging;

using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Ipc.SharedMemory;

namespace VNLib.Plugins.Extensions.Ipc
{
    /// <summary>
    /// Provides extension methods for plugins to open IPC export bridges and manage
    /// consumer-side export monitoring.
    /// </summary>
    public static class PluginConsumerExtensions
    {
        /// <summary>
        /// Opens an <see cref="IpcExportBridge"/> as a consumer from the specified shared
        /// memory region accessor.
        /// </summary>
        /// <param name="accessor">The memory region accessor that provides gated access to the shared memory region.</param>
        /// <returns>A task that completes with an <see cref="IpcExportBridge"/> consumer instance once the region is available.</returns>
        public static Task<IpcExportBridge> OpenBridgeAsync(this IPluginMemoryRegionAccessor accessor)
        {
            return accessor.WaitAsync()
                .ContinueWith(static tr =>
            {
                IPluginMemoryRegion region = tr.GetAwaiter().GetResult();
                return IpcExportBridge.Open(region.SyncRoot, region.AsSpan);
            
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// Returns a <see cref="PluginIpcManager"/> for the specified plugin that acts as
        /// a starting point for configuring IPC export consumers.
        /// </summary>
        /// <param name="plugin">The plugin that will consume IPC exports.</param>
        /// <returns>A <see cref="PluginIpcManager"/> instance bound to the specified plugin.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plugin"/> is <see langword="null"/>.</exception>
        public static PluginIpcManager Ipc(this PluginBase plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            return new(plugin);
        }     

        /// <summary>
        /// A background worker that publishes a named export on an <see cref="IpcExportBridge"/>
        /// and automatically re-publishes the instance when the export table is destroyed and
        /// re-established by the owning plugin, for the duration of the plugin's lifetime
        /// or until the caller cancels the stop token.
        /// </summary>
        /// <param name="bridgeTask">A task that completes with the <see cref="IpcExportBridge"/> to publish on.</param>
        /// <param name="userCancellation">An optional cancellation token that stops the publish worker.</param>
        /// <param name="symbolName">The name of the export symbol to publish on the bridge.</param>
        /// <param name="instance">The object instance to export and share with consumers.</param>
        private sealed class IpcPublishWorker(
            Task<IpcExportBridge> bridgeTask,
            CancellationToken userCancellation,
            string symbolName,
            object instance
        ) : IAsyncBackgroundWork
        {

            /// <summary>
            /// The delay in milliseconds between polls of the export table state while
            /// waiting for the owning plugin to (re-)establish the table.
            /// </summary>
            private const int TablePollDelayMs = 100;

            /// <summary>
            /// Runs the export publishing loop, waiting for the bridge and the export table
            /// to become available, publishing the instance, blocking until the export is
            /// revoked, and repeating until the plugin unloads.
            /// </summary>
            /// <param name="pluginLog">The log provider used for diagnostic messages during monitoring.</param>
            /// <param name="exitToken">The cancellation token that signals when the plugin is unloading.</param>
            public async Task DoWorkAsync(ILogProvider pluginLog, CancellationToken exitToken)
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(exitToken, userCancellation);
                
                ILogProvider log = pluginLog.CreateScope("IpcExport");

                log.Verbose("{sym} - waiting for bridge to become available.", symbolName);               

                IpcExportBridge bridge = await bridgeTask.ConfigureAwait(false);

                do
                {
                    try
                    {
                        (bool initialized, _, Task? onExitTask) = bridge.TryGetExport(symbolName);

                        // Wait for the export table to be initialized by the owning plugin
                        if (!initialized)
                        {
                            await Task.Delay(TablePollDelayMs, cts.Token)
                                .ConfigureAwait(false);

                            continue;
                        }
                        // onExitTask is connected to the instance, if it's null, the instance is not published
                        // so it's safe to publish it
                        else if (onExitTask is null)
                        {
                            log.Debug("{sym} - Publishing {type} to the export table.", symbolName, instance.GetType().Name);

                            bridge.Publish(symbolName, instance);

                            // Re-loop to re-capture the new export 
                            continue;
                        }

                        log.Debug("{sym} - Export ({real_name}) published. Waiting for revocation.",
                            symbolName,
                            instance.GetType().Name
                        );

                        // Block until the export is revoked or the plugin unloads.
                        // During steady state this is the only waiting point — no polling occurs.
                        await onExitTask.WaitAsync(cts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        log.Error("{sym} - Wait for export task was cancelled unexpectedly", symbolName);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }                   
                }
                while (!cts.IsCancellationRequested);

                log.Verbose("{sym} - publish loop is exiting on plugin unload", symbolName);
            }
        }

        /// <summary>
        /// A background worker that tracks a named export on an <see cref="IpcExportBridge"/>
        /// and notifies the consumer when the exported instance becomes available or expires,
        /// for the duration of the plugin's lifetime.
        /// </summary>
        /// <param name="bridgeTask">A task that completes with the <see cref="IpcExportBridge"/> to monitor.</param>
        /// <param name="symbolName">The name of the export symbol to monitor on the bridge.</param>
        /// <param name="consumer">The consumer that receives notifications when the exported instance changes.</param>
        private sealed class IpcConsumerWorker(
            Task<IpcExportBridge> bridgeTask,
            string symbolName,
            IIpcExportConsumer consumer
        ) : IAsyncBackgroundWork
        {

            /// <summary>
            /// Runs the export monitoring loop, waiting for the bridge and the named export
            /// to become available, notifying the consumer, blocking until the export expires,
            /// and repeating until the plugin unloads.
            /// </summary>
            /// <param name="pluginLog">The log provider used for diagnostic messages during monitoring.</param>
            /// <param name="exitToken">The cancellation token that signals when the plugin is unloading.</param>
            public async Task DoWorkAsync(ILogProvider pluginLog, CancellationToken exitToken)
            {
                ILogProvider log = pluginLog.CreateScope("IpcExport");

                log.Verbose("{sym} - waiting for bridge to become available.", symbolName);

                IpcExportBridge bridge = await bridgeTask.ConfigureAwait(false);

                /*
                 * In reloadable environments the table producer or even the export can unload
                 * or expire so we run in a loop to wait for exports as long as the current plugin
                 * is alive.
                 */

                do
                {
                    try
                    {
                        /*
                         * The bridge should wait for init and the object to become available
                         * before returning. If it does not, then the bridge has a bug. An exception
                         * should be raised if an error occurs or our plugin unloads.
                         */

                        (bool init, object? instance, Task? onExit) = await bridge.WaitForExport(symbolName, exitToken)
                            .ConfigureAwait(false);

                        if (!init || instance is null || onExit is null)
                        {
                            throw new InvalidOperationException("IPC export bridge was not in a valid state");
                        }

                        consumer.OnInstanceChanged(instance);

                        log.Debug("{sym} - Export ({real_name}) available for use. Waiting for cleanup",
                            symbolName,
                            instance.GetType().Name
                        );

                        // Wait for the producer's exit task or our plugin to unload
                        await Task.WhenAny(onExit, exitToken.WaitHandle.WaitAsync());
                    }
                    catch (OperationCanceledException) when (exitToken.IsCancellationRequested)
                    {
                        // Exit token has been cancelled
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        log.Error("{sym} - Wait for export task was cancelled unexpectedly", symbolName);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Plugin is exiting, bridge is closing before we were signaled
                        break;
                    }

                    log.Verbose("{sym} - Export is no longer valid, removing references", symbolName);

                    // Clear the export
                    consumer.OnInstanceChanged(null);
                }
                while (!exitToken.IsCancellationRequested);

                log.Verbose("{sym} - loop is exiting on plugin unload", symbolName);
            }
        }

        /// <summary>
        /// A manager that provides access to the plugin's IPC export monitoring facilities.
        /// </summary>
        /// <param name="Plugin">The plugin associated with this manager.</param>
        public readonly record struct PluginIpcManager(PluginBase Plugin) 
        {
            /// <summary>
            /// Creates an <see cref="IpcExportConsumerMonitor"/> for the specified bridge task.
            /// </summary>
            /// <param name="bridgeTask">A task that completes with the <see cref="IpcExportBridge"/> to monitor.</param>
            /// <returns>An <see cref="IpcExportConsumerMonitor"/> bound to the plugin and bridge task.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <see cref="Plugin"/> or <paramref name="bridgeTask"/> is <see langword="null"/>.</exception>
            public readonly IpcExportConsumerMonitor Exports(Task<IpcExportBridge> bridgeTask)
            {
                ArgumentNullException.ThrowIfNull(Plugin);
                ArgumentNullException.ThrowIfNull(bridgeTask);
                return new(Plugin, bridgeTask);
            }

            /// <inheritdoc cref="Exports(Task{IpcExportBridge})"/>
            /// <param name="bridge">The <see cref="IpcExportBridge"/> to monitor.</param>
            public readonly IpcExportConsumerMonitor Exports(IpcExportBridge bridge)
            {
                ArgumentNullException.ThrowIfNull(bridge);
                return Exports(Task.FromResult(bridge));
            }

            /// <inheritdoc cref="Exports(Task{IpcExportBridge})"/>
            /// <param name="lazyBridge">An <see cref="IAsyncLazy{T}"/> that produces the <see cref="IpcExportBridge"/> to monitor.</param>
            public readonly IpcExportConsumerMonitor Exports(IAsyncLazy<IpcExportBridge> lazyBridge)
            {
                ArgumentNullException.ThrowIfNull(lazyBridge);
                return Exports(lazyBridge.AsTask());
            }
        }

        /// <summary>
        /// A monitor that tracks exports from a shared memory bridge and notifies 
        /// consumers when their requested exports become available or expire.
        /// </summary>
        /// <param name="plugin">The plugin that owns the consumer worker.</param>
        /// <param name="bridgeTask">A task that completes with the <see cref="IpcExportBridge"/> to monitor.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plugin"/> or <paramref name="bridgeTask"/> is <see langword="null"/>.</exception>
        public readonly struct IpcExportConsumerMonitor(PluginBase plugin, Task<IpcExportBridge> bridgeTask)
        {
            private readonly PluginBase _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            private readonly Task<IpcExportBridge> _bridgeTask = bridgeTask ?? throw new ArgumentNullException(nameof(bridgeTask));

            /// <summary>
            /// Registers a consumer to monitor a named export and schedules the background
            /// worker that tracks the export's lifecycle for the duration of the plugin.
            /// </summary>
            /// <param name="exportName">The name of the export symbol to monitor on the bridge.</param>
            /// <param name="consumer">The consumer that receives notifications when the exported instance changes.</param>
            /// <returns>This monitor instance for method chaining.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="exportName"/> or <paramref name="consumer"/> is <see langword="null"/>.</exception>
            public readonly IpcExportConsumerMonitor Consume(string exportName, IIpcExportConsumer consumer)
            {               
                ArgumentNullException.ThrowIfNull(exportName);
                ArgumentNullException.ThrowIfNull(consumer);

                IpcConsumerWorker worker = new (_bridgeTask, exportName, consumer);

                _ = _plugin.Tasks()
                          .ObserveWork(worker);

                return this;
            }

            /// <summary>
            /// Publishes an object instance to the IPC export table under the specified symbol
            /// name and schedules a background worker that automatically re-publishes the
            /// instance if the export table is destroyed and re-established by the owning
            /// plugin (e.g. during hot-reload).
            /// </summary>
            /// <param name="exportName">The name of the export symbol to publish on the bridge.</param>
            /// <param name="instance">The object instance to export and share with consumers.</param>
            /// <param name="stopToken">An optional cancellation token that stops the publish worker and exits the monitoring loop.</param>
            /// <returns>This monitor instance for method chaining.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="exportName"/> or <paramref name="instance"/> is <see langword="null"/>.</exception>
            public readonly IpcExportConsumerMonitor Publish(
                string exportName, 
                object instance, 
                CancellationToken stopToken = default
            )
            {
                ArgumentNullException.ThrowIfNull(exportName);
                ArgumentNullException.ThrowIfNull(instance);

                IpcPublishWorker worker = new (_bridgeTask, stopToken, exportName, instance);

                _ = _plugin.Tasks()
                          .ObserveWork(worker);

                return this;
            }
        }
    }   
}
