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
        /// A background worker that tracks a named export on an <see cref="IpcExportBridge"/>
        /// and notifies the consumer when the exported instance becomes available or expires,
        /// for the duration of the plugin's lifetime.
        /// </summary>
        private sealed class IpcConsumerWorker(
            Task<IpcExportBridge> bridgeTask, 
            string symbolName,
            IIpcExportConsumer consumer
        ) : IAsyncBackgroundWork
        {

            /// <summary>
            /// Runs the export monitoring loop, waiting for the bridge and the named export
            /// to become available, notifying the consumer of instance changes, and repeating
            /// until the plugin unloads.
            /// </summary>
            /// <param name="pluginLog">The log provider used for diagnostic messages during monitoring.</param>
            /// <param name="exitToken">The cancellation token that signals when the plugin is unloading.</param>
            /// <exception cref="InvalidOperationException">Thrown when the export bridge returns an invalid state (uninitialized, null instance, or null exit task).</exception>
            public async Task DoWorkAsync(ILogProvider pluginLog, CancellationToken exitToken)
            {             
                ILogProvider log = pluginLog.CreateScope("IpcExport");

                log.Verbose("{sym} - waiting for bridge to become available.", symbolName);

                IpcExportBridge bridge = await bridgeTask.ConfigureAwait(false);

                /*
                 * In reloadable environments the table producer or even the export can unload
                 * or expire so we can run in a loop to wait for exports as long as the current plugin
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
        }
    }   
}
