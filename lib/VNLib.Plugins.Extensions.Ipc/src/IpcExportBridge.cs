/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc
* File: IpcExportBridge.cs 
*
* IpcExportBridge.cs is part of VNLib.Plugins.Extensions.Ipc which is part of the larger 
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Plugins.Ipc.SharedMemory;

namespace VNLib.Plugins.Extensions.Ipc
{
    /// <summary>
    /// A delegate that provides access to the shared memory buffer used by the IPC export bridge.
    /// </summary>
    /// <returns>A span over the shared memory buffer.</returns>
    public delegate Span<byte> IpcBufferCallback();

    /// <summary>
    /// Bridges managed object exports across assembly boundaries within the same process
    /// using a shared memory IPC table. Both producers and consumers can publish, unpublish,
    /// and query exports. The producer owns the internal table structure and its lifetime;
    /// individual export lifetimes are tracked independently via <see cref="IpcObjectExport.OnExitTask"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the producer is disposed, the table is destroyed and all exports are revoked.
    /// </para>
    /// <para>
    /// Consumer bridges track exports they publish and automatically unpublish them when
    /// the consumer is disposed, preventing stale entries from lingering in the export table.
    /// </para>
    /// </remarks>
    public class IpcExportBridge : VnDisposeable
    {
        /// <summary>
        /// The delay in milliseconds to wait between checks for the desired export during polling.
        /// </summary>
        private const int ExportPollLoopDelay = 100;

        private readonly bool _producer;
        private readonly object _lockObj;
        private readonly IpcBufferCallback _getSpan;
        private readonly CancellationTokenSource _disposeToken;

        /// <summary>
        /// Tracks export names published by this bridge instance, so they can be
        /// unpublished when the bridge is disposed. Only maintained for consumer
        /// bridges; <see langword="null"/> for producers since <see cref="IpcObjectExporter.Destroy"/>
        /// reclaims all exports on producer disposal.
        /// </summary>
        private readonly HashSet<string>? _publishedSymbols;

        /// <summary>
        /// Initializes a new <see cref="IpcExportBridge"/> instance.
        /// </summary>
        /// <param name="producer"><see langword="true"/> if this instance owns the export table (producer); <see langword="false"/> if it is a consumer.</param>
        /// <param name="lock">The lock object shared between all users of the shared memory region.</param>
        /// <param name="getSpan">A callback delegate that returns the shared memory buffer span.</param>
        private IpcExportBridge(bool producer, object @lock, IpcBufferCallback getSpan)
        {
            _producer = producer;
            _lockObj = @lock;
            _getSpan = getSpan;
            _disposeToken = new();
            _publishedSymbols = producer ? null : new(StringComparer.OrdinalIgnoreCase);

            // Producer must initialize the export table
            if (producer)
            {
                ExportTable.Initialize();
            }
        }

        private IpcObjectExporter ExportTable => new(_getSpan(), _lockObj);

        /// <inheritdoc/>
        protected override void Free()
        {
            try
            {
                // Notify all internal waiting tasks that we're exiting
                _disposeToken.Cancel();
                _disposeToken.Dispose();
            }
            finally
            {
                if (_producer)
                {
                    // Producer owns the table and must destroy it, which reclaims all exports
                    ExportTable.Destroy();
                }
                else
                {
                    // Consumer must unpublish its own exports before the table is left
                    UnpublishAll();
                }
            }
        }

        /// <summary>
        /// Removes all exports published by this consumer bridge instance from the shared
        /// export table. The shared lock is held for the entire operation to ensure atomicity
        /// with concurrent <see cref="Publish"/> and <see cref="Unpublish"/> calls.
        /// </summary>
        private void UnpublishAll()
        {
            Debug.Assert(_publishedSymbols is not null, "Consumer bridge must have a published symbols set");

            lock (_lockObj)
            {
                IpcObjectExporter table = ExportTable;

                foreach (string name in _publishedSymbols)
                {
                    try
                    {
                        _ = table.Unpublish(name);
                    }
                    catch (InvalidOperationException)
                    {
                        // The producer has already destroyed the table; remaining entries are gone
                        break;
                    }
                }
            }
        }

        /// <inheritdoc cref="IpcObjectExporter.Publish(ReadOnlySpan{char}, object)"/>
        public IpcExportBridge Publish(string exportName, object instance)
        {
            Check();

            lock (_lockObj)
            {
                ExportTable.Publish(exportName, instance);

                _publishedSymbols?.Add(exportName);
            }

            return this;
        }

        /// <inheritdoc cref="IpcObjectExporter.Unpublish(ReadOnlySpan{char})"/>
        public bool Unpublish(string exportName)
        {
            Check();

            lock (_lockObj)
            {
                bool removed = ExportTable.Unpublish(exportName);

                if (removed)
                {
                    _publishedSymbols?.Remove(exportName);
                }

                return removed;
            }
        }

        /// <inheritdoc cref="IpcObjectExporter.TryGetExport(ReadOnlySpan{char})"/>
        public IpcObjectExport TryGetExport(ReadOnlySpan<char> symbolName)
        {
            Check();
            return ExportTable.TryGetExport(symbolName);
        }

        /// <summary>
        /// Polls the IPC export table until the specified export becomes available and its instance is non-null.
        /// </summary>
        /// <param name="symbolName">The name that identifies the export to wait for.</param>
        /// <param name="cancellation">A cancellation token to cancel the wait operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the initialized <see cref="IpcObjectExport"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="symbolName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the wait operation is cancelled.</exception>
        private async Task<IpcObjectExport> WaitForExportInternal(string symbolName, CancellationToken cancellation)
        {
            ArgumentException.ThrowIfNullOrEmpty(symbolName);

            IpcObjectExport result;

            // Wait for the producer to load and the export to become available
            do
            {
                result = ExportTable.TryGetExport(symbolName);

                if (result.Initialized && result.Instance is not null)
                {
                    break;
                }

                await Task.Delay(ExportPollLoopDelay, cancellation)
                    .ConfigureAwait(false);

            } while (true);

            return result;
        }

        /// <summary>
        /// Waits for an export to become available, using a linked cancellation token that combines 
        /// a timeout-based token with this bridge's disposal token. This method blocks until the 
        /// named instance is published, the timeout expires, or this bridge is disposed.
        /// </summary>
        /// <param name="symbolName">The name of the IPC export symbol to wait for.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the IPC export object.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="symbolName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if this bridge has been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the wait operation is cancelled because this bridge is being disposed.</exception>
        /// <remarks>
        /// This function polls the IPC export table indefinitely until the specified export becomes available or this bridge connection is closed.
        /// </remarks>
        public async Task<IpcObjectExport> WaitForExport(string symbolName)
        {
            ArgumentException.ThrowIfNullOrEmpty(symbolName);

            try
            {
                return await WaitForExportInternal(symbolName, _disposeToken.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
            {
                // Bridge disposed, raise disposed exception instead
                throw new ObjectDisposedException(nameof(IpcExportBridge), "WaitForExport was cancelled because the bridge is being disposed");
            }
        }

        /// <summary>
        /// Waits asynchronously for an object export with the specified symbol name to become 
        /// available, then returns it.
        /// </summary>
        /// <param name="symbolName">The name of the IPC export symbol to wait for.</param>
        /// <param name="cancellation">An optional cancellation token to cancel the wait operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the IPC export object.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="symbolName"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if this bridge has been disposed.</exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the wait operation is cancelled because this bridge is being disposed or the supplied cancellation 
        /// token was triggered.
        /// </exception>
        /// <remarks>
        /// This function polls the IPC export table indefinitely until the specified export becomes available or the operation is cancelled. 
        /// </remarks>
        public async Task<IpcObjectExport> WaitForExport(string symbolName, CancellationToken cancellation)
        {
            ArgumentException.ThrowIfNullOrEmpty(symbolName);

            // Combine the disposal token with the supplied token
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.Token, cancellation);

            try
            {
                return await WaitForExportInternal(symbolName, cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
            {
                // Bridge disposed, raise disposed exception instead
                throw new ObjectDisposedException(nameof(IpcExportBridge), "WaitForExport was cancelled because the bridge is being disposed");
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException("WaitForExport was cancelled by user requested cancellation", cts.Token);
            }
            finally
            {
                cts.Dispose();
            }
        }

        /// <inheritdoc cref="WaitForExport(string, CancellationToken)"/>
        /// <param name="symbolName">The name of the IPC export symbol to wait for.</param>
        /// <param name="timeout">The maximum amount of time to wait for the IPC export to become available.</param>
        /// <remarks>
        /// This function polls the IPC export table until the specified export becomes available, the timeout is reached, or the operation is cancelled. 
        /// </remarks>
        public async Task<IpcObjectExport> WaitForExport(string symbolName, TimeSpan timeout)
        {
            ArgumentException.ThrowIfNullOrEmpty(symbolName);

            // Combine the disposal token with a timeout cancellation token
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.Token);

            try
            {
                cts.CancelAfter(timeout);

                return await WaitForExportInternal(symbolName, cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
            {
                // Bridge disposed, raise disposed exception instead
                throw new ObjectDisposedException(nameof(IpcExportBridge), "WaitForExport was cancelled because the bridge is being disposed");
            }
            catch (OperationCanceledException)
            {
                // If OCE when plugin is not the cause, means that the timeout expired
                throw new TimeoutException("WaitForExport was cancelled because the timeout was exceeded");
            }
            finally
            {
                cts.Dispose();
            }
        }

        /// <summary>
        /// Creates and initializes a new producer <see cref="IpcExportBridge"/> over a shared memory block.
        /// The producer owns the export table structure and is responsible for its lifetime.
        /// </summary>
        /// <param name="lock">The lock object shared between all users of the shared memory region.</param>
        /// <param name="getSpan">A callback delegate that returns the shared memory buffer span.</param>
        /// <returns>The initialized producer <see cref="IpcExportBridge"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="lock"/> or <paramref name="getSpan"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the export table is invalid or has been corrupted.</exception>
        public static IpcExportBridge Create(object @lock, IpcBufferCallback getSpan)
        {
            ArgumentNullException.ThrowIfNull(@lock);
            ArgumentNullException.ThrowIfNull(getSpan);

            return new(true, @lock, getSpan);
        }

        /// <inheritdoc cref="Create(object, IpcBufferCallback)"/>
        /// <param name="sharedMem">A producer shared memory region</param>
        public static IpcExportBridge Create(IPluginMemoryRegion sharedMem) 
            => Create(sharedMem.SyncRoot, sharedMem.AsSpan);

        /// <summary>
        /// Opens an existing IPC export table as a consumer <see cref="IpcExportBridge"/>.
        /// </summary>
        /// <param name="lock">The lock object shared between all users of the shared memory region.</param>
        /// <param name="getSpan">A callback delegate that returns the shared memory buffer span.</param>
        /// <returns>A consumer <see cref="IpcExportBridge"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="lock"/> or <paramref name="getSpan"/> is <see langword="null"/>.</exception>
        public static IpcExportBridge Open(object @lock, IpcBufferCallback getSpan)
        {
            ArgumentNullException.ThrowIfNull(@lock);
            ArgumentNullException.ThrowIfNull(getSpan);

            return new (false, @lock, getSpan);
        }
    }
}
