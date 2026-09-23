/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc
* File: IpcObjectExporter.cs 
*
* IpcObjectExporter.cs is part of VNLib.Plugins.Extensions.Ipc which is part of the larger 
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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;
using VNLib.Utils.Memory;

namespace VNLib.Plugins.Extensions.Ipc
{
    /// <summary>
    /// Represents a shared IPC store for exporting managed objects across assembly boundaries
    /// within the same process using a pre-allocated shared memory buffer.
    /// </summary>
    /// <param name="buffer">A span over the shared memory buffer that is at least <see cref="RequiredBufferSize"/> bytes in length.</param>
    /// <param name="lockObj">A shared lock object used for synchronization across the entire shared memory region.</param>
    public readonly ref struct IpcObjectExporter(Span<byte> buffer, object lockObj)
    {
        /// <summary>
        /// Gets the maximum number of export slots available for exporting objects.
        /// </summary>
        public const int MaxExports = InlineArray<SymbolExport>.Length;

        /// <summary>
        /// Gets the maximum length, in characters, of an export symbol name.
        /// Names exceeding this length will be rejected by <see cref="Publish"/>.
        /// </summary>
        public const int MaxExportNameSize = InlineArray<char>.Length;

        /// <summary>
        /// Gets the required size, in bytes, of the shared memory buffer to hold internal shared structures
        /// within the shared memory region.
        /// </summary>
        public static readonly int RequiredBufferSize = Unsafe.SizeOf<SharedHeader>();

        private readonly Span<byte> _buffer = buffer;

        /*
         * Precomputed status flags with magic value and schema.
         * First header qword is magic.schema.status
         *                        u32    u16   u16
         * 
         * Magic   = 0x24542d75
         * Schema  = 0x0001 (version 1 right now)
         * 
         * NOTE! any changes to unmanaged structures memory layout requires
         * incrementing the schema version number
         */

        private const ulong HeaderStatusNone      = 0x24542d75_00010000ul;
        private const ulong HeaderStatusReady     = 0x24542d75_00010001ul;

        private readonly ref SharedHeader GetHeader()
        {
            if (_buffer.Length < RequiredBufferSize)
            {
                throw new InvalidOperationException("The shared memory buffer is too small to contain the required header.");
            }

            return ref Unsafe.As<byte, SharedHeader>(ref _buffer[0]);
        }

        /// <summary>
        /// Initializes the shared export table for use.
        /// </summary>
        /// <remarks>
        /// NOTE: Should only be called by the shared memory region owner to establish the internal
        /// structures and schema.
        /// </remarks>
        public readonly void Initialize()
        {
            ref SharedHeader header = ref GetHeader();

            lock (lockObj)
            {
                if (header.StatusWord == HeaderStatusReady)
                {
                    throw new InvalidOperationException("The table is still initialized and has not been cleared. Clear the table before re-initializing.");
                }

                // Clear the header if it was uninitialized or in an invalid state
                MemoryUtil.ZeroStruct(ref header);
              
                // Ready for use
                header.StatusWord = HeaderStatusReady;
            }
        }

        /// <summary>
        /// Destroys the shared export table and frees all allocated handles.
        /// </summary>
        /// <remarks>
        /// Should only be called by the shared memory region owner when the store is no longer
        /// needed, such as during plugin shutdown. After calling this method, the store will be
        /// in an unusable state and must be re-initialized before use.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The table is not initialized or has already been destroyed.</exception>
        public readonly void Destroy()
        {
            ref SharedHeader header = ref GetHeader();

            lock (lockObj)
            {
                if (header.StatusWord != HeaderStatusReady)
                {
                    throw new InvalidOperationException("The table is not initialized or has already been destroyed.");
                }

                // walk the table and free all allocated handles
                Span<SymbolExport> exports = header.Exports.AsSpan();

                for (int i = 0; i < exports.Length; i++)
                {
                    ref SymbolExport export = ref exports[i];

                    if (export.GCHandlePtr.IsAllocated)
                    {
                        Debug.Assert(export.ExitTaskPtr.Handle.IsAllocated);

                        CloseExport(ref export);
                    }
                }

                // Clear out the entire header
                MemoryUtil.ZeroStruct(ref header);
            }
        }

        /// <summary>
        /// Exports an object instance to the IPC store under the specified symbol name.
        /// </summary>
        /// <remarks>
        /// The store takes ownership of a strong reference to the object, keeping it alive until
        /// <see cref="Unpublish"/> or <see cref="Destroy"/> is called by the producer.
        /// </remarks>
        /// <param name="instance">The object instance to export and share with consumers.</param>
        /// <param name="exportName">The unique symbol name identifying the export. Must be non-empty and fit within the predefined name buffer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="exportName"/> is empty, whitespace, or a symbol with that name already exists.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="exportName"/> exceeds the maximum allowed name length.</exception>
        /// <exception cref="InvalidOperationException">The store is not initialized or no export slots are available.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded.</exception>
        public readonly void Publish(ReadOnlySpan<char> exportName, object instance)
        {
            ArgumentNullException.ThrowIfNull(instance);

            if (exportName.IsEmpty || exportName.IsWhiteSpace())
            {
                throw new ArgumentException("Object name cannot be empty or whitespace.", nameof(exportName));
            }

            // Ensure the object name can fit in the predefined name buffer
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                exportName.Length,
                MaxExportNameSize,
                nameof(exportName)
            );

            ref SharedHeader header = ref GetHeader();

            lock (lockObj)
            {
                ThrowIfNotReady(in header);

                Span<SymbolExport> exports = header.Exports.AsSpan();

                ThrowIfDuplicateName(exports, exportName, nameof(exportName));

                // Find an empty export slot
                for (int i = 0; i < exports.Length; i++)
                {
                    ref SymbolExport export = ref exports[i];

                    // Check for an empty slot (zero handle indicates empty)
                    if (!export.GCHandlePtr.IsAllocated)
                    {
                        InitExport(ref export, exportName, instance);

                        return;
                    }                    
                }               
            }

            throw new InvalidOperationException("No available export slots to export the object.");
        }

        /// <summary>
        /// Removes a previously exported object from the IPC store by its symbol name.
        /// </summary>
        /// <remarks>
        /// Consumers that have already retrieved the object will not be notified and may continue
        /// holding their existing references.
        /// </remarks>
        /// <param name="exportName">The symbol name of the export to remove.</param>
        /// <returns><see langword="true"/> if a matching export was found and removed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="exportName"/> is empty or whitespace.</exception>
        /// <exception cref="InvalidOperationException">The store is not initialized.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded.</exception>
        public readonly bool Unpublish(ReadOnlySpan<char> exportName)
        {
            if (exportName.IsEmpty || exportName.IsWhiteSpace())
            {
                throw new ArgumentException("Export name cannot be empty or whitespace.", nameof(exportName));
            }

            ref SharedHeader header = ref GetHeader();

            lock (lockObj)
            {
                ThrowIfNotReady(in header);

                Span<SymbolExport> exports = header.Exports.AsSpan();

                for (int i = 0; i < exports.Length; i++)
                {
                    ref SymbolExport export = ref exports[i];

                    if (!export.GCHandlePtr.IsAllocated)
                    {
                        continue;
                    }

                    ReadOnlySpan<char> existingName = export.Name
                        .AsReadOnlySpan()
                        .SliceBeforeParam('\0');

                    if (existingName.Equals(exportName, StringComparison.OrdinalIgnoreCase))
                    {
                        CloseExport(ref export);

                        MemoryUtil.ZeroStruct(ref export);

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to retrieve an exported object from the IPC store by its symbol name.
        /// Returns a <see cref="IpcObjectExport"/> containing the lookup result, initialization
        /// state, and the producer's exit handle in a single atomic operation.
        /// </summary>
        /// <param name="symbolName">The symbol name to search for in the export table.</param>
        /// <returns>A <see cref="IpcObjectExport"/> describing the lookup result.</returns>
        public readonly IpcObjectExport TryGetExport(ReadOnlySpan<char> symbolName)
        {
            ref SharedHeader header = ref GetHeader();

            lock (lockObj)
            {
                if (header.StatusWord != HeaderStatusReady)
                {
                    return default;
                }

                Span<SymbolExport> exports = header.Exports.AsSpan();

                for (int i = 0; i < exports.Length; i++)
                {
                    ref SymbolExport export = ref exports[i];

                    if (!export.GCHandlePtr.IsAllocated)
                    {
                        continue;
                    }

                    /*
                     * Trim trailing null characters in the name. Name is fixed length 
                     * char array so null termination is used for padding the end of the name.
                     * 
                     * Most stages, including publish ensure the memory for the entire slot is 
                     * zeroed, which should be `\0` for char buffers
                     */
                    ReadOnlySpan<char> nameSpan = export.Name.AsReadOnlySpan()
                        .SliceBeforeParam('\0');

                    if (nameSpan.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found 
                        return new IpcObjectExport
                        {
                            Initialized     = true,
                            Instance        = export.GCHandlePtr.Target,
                            OnExitTask      = export.ExitTaskPtr.GetTask()
                        };
                    }
                }

                // Name not found, but table is initialized
                return new IpcObjectExport
                {
                    Initialized     = true,
                    Instance        = null,
                    OnExitTask      = null
                };
            }
        }

        private static void ThrowIfNotReady(ref readonly SharedHeader header)
        {
            if (header.StatusWord != HeaderStatusReady)
            {
                throw new InvalidOperationException("The export store is not initialized or is in an invalid state.");
            }
        }

        private static void ThrowIfDuplicateName(Span<SymbolExport> exports, ReadOnlySpan<char> exportName, string paramName)
        {
            for (int i = 0; i < exports.Length; i++)
            {
                ref readonly SymbolExport export = ref exports[i];

                if (!export.GCHandlePtr.IsAllocated)
                {
                    continue;
                }

                // trim trailing null termination for names, see TryGetExport note above
                ReadOnlySpan<char> existingName = export.Name
                    .AsReadOnlySpan()
                    .SliceBeforeParam('\0');

                if (existingName.Equals(exportName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"An export with the name '{exportName}' is already registered.", paramName);
                }
            }
        }            

        private static void InitExport(ref SymbolExport export, ReadOnlySpan<char> exportName, object instance)
        {
            Debug.Assert(!export.GCHandlePtr.IsAllocated, "Empty slot has allocated gc handle");
            Debug.Assert(!export.ExitTaskPtr.Handle.IsAllocated, "Empty slot has allocated gc handle");

            // Zeroing the export element should zero all fields including the inline arrays.
            MemoryUtil.ZeroStruct(ref export);

            // Alloc new gc handle with a strong reference to ensure the object remains alive as long as the producer allows it.
            export.GCHandlePtr = GCHandle.Alloc(instance, GCHandleType.Normal);

            // Also add the producer's exit handle to the export
            export.ExitTaskPtr = OnExitHandle.New();

            // Write the object name into the export slot.
            // Null terminated only because the array is zeroed. 
            exportName.CopyTo(export.Name.AsSpan());
        }

        private static void CloseExport(ref SymbolExport export)
        {
            export.ExitTaskPtr.FireAndForget();

            // Free gc handle (mutates the handle structures)
            export.GCHandlePtr.Free();
            export.ExitTaskPtr.Free();
        }

        /*
         * Data structures
         * 
         * Below are the structures stored in unmanaged memory assumed to be in a shared memory region 
         * accessible by multiple plugins. These structures are designed to be simple and contain only 
         * blittable types to ensure they can be safely shared across assembly boundaries without 
         * requiring complex marshaling. 
         * 
         * Structures may only contain blittable types to safely use managed semantics to store and
         * access fields from a block of managed/unmanaged memory. The InlineArray<T> structure allows 
         * defining a fixed-size array without needing to mark the assembly as unsafe and require using
         * the fixed keyword.
         *
         * Caveats. The size of the array is... fixed which means we are responsible for the number of 
         * allowed exports and maximum size of names. Users can get creative with factory functions to
         * work around this. It's a tradeoff between memory consumption and flexibility.
         * 
         * NOTE: The compiler augments the structures depending on the target runtime and may add padding
         * or additional hidden fields to support the inline arrays. It is safe to use Unsafe.SizeOf<T>() 
         * at runtime to get the size of the structures.
         *
         */

        [StructLayout(LayoutKind.Sequential)]
        private struct SharedHeader
        {
            /// <summary>
            /// Tracks the initialization and status of the shared memory region.
            /// </summary>
            internal ulong StatusWord;

            /// <summary>
            /// Contains the fixed-size array of exported symbols, where each slot holds a reference
            /// to a shared object along with its name.
            /// </summary>
            internal InlineArray<SymbolExport> Exports;
        }

        /// <summary>
        /// Provides an export status structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SymbolExport
        {
            /// <summary>
            /// Holds a <see cref="GCHandle"/> that references the shared object instance
            /// across assembly boundaries.
            /// </summary>
            internal GCHandle GCHandlePtr;

            /// <summary>
            /// Holds a reference to the producer unload signal <see cref="WaitHandle"/>.
            /// </summary>
            internal OnExitHandle ExitTaskPtr;

            /// <summary>
            /// Gets the name of the exported symbol used to identify it within the export table.
            /// </summary>
            internal InlineArray<char> Name;
        }

        /// <summary>
        /// Provides a special unmanaged array structure of length <see cref="Length"/>.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to define the array of.</typeparam>
        [InlineArray(Length)]
        private struct InlineArray<T> where T : unmanaged
        {
            /// <summary>
            /// Gets the fixed length of the inline array, which determines how many elements
            /// the array can hold and is used to create spans over the array.
            /// </summary>
            public const int Length = 32;

            /// <summary>
            /// Represents the first element of the inline array, with remaining elements laid out
            /// sequentially in memory after this field.
            /// </summary>
            private T _element0;

            /// <summary>
            /// Returns a mutable span over the elements of the inline array.
            /// </summary>
            /// <returns>A mutable span over the elements of the inline array.</returns>
            public readonly Span<T> AsSpan()
                => MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _element0), Length);

            /// <summary>
            /// Returns a read-only span over the elements of the inline array.
            /// </summary>
            /// <returns>A read-only span over the elements of the inline array.</returns>
            public readonly ReadOnlySpan<T> AsReadOnlySpan()
                => MemoryMarshal.CreateReadOnlySpan(in _element0, Length);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OnExitHandle(TaskCompletionSource tcs)
        {
            internal GCHandle Handle = GCHandle.Alloc(tcs, GCHandleType.Normal);

            public void Free() => Handle.Free();

            /// <summary>
            /// Gets the shared task instance from the internal completion source
            /// </summary>
            /// <returns>The task associated with this handle if it's been initialized</returns>
            public readonly Task? GetTask()
            {
                if (Handle.IsAllocated)
                {
                    if (Handle.Target is TaskCompletionSource t)
                    {
                        return t.Task;
                    }

                    Debug.Fail("Object held within exit handle was not an instance of TaskCompletionSource");
                }

                return null;
            }

            /// <summary>
            /// Completes the internal task by queuing the exit task on a background thread and 
            /// returning regardless of the child's attachment. 
            /// </summary>
            public readonly void FireAndForget()
            {
                if (Handle.IsAllocated)
                {
                    if (Handle.Target is TaskCompletionSource t)
                    {
                        Debug.Assert(t.Task.Status == TaskStatus.WaitingForActivation);

                        _ = Task.Run(t.TrySetResult);
                    }
                    else
                    {
                        Debug.Fail("Object held within exit handle was not an instance of TaskCompletionSource");
                    }
                }
            }

            public static OnExitHandle New()
            {
                TaskCompletionSource rcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Debug.Assert(rcs.Task.Status == TaskStatus.WaitingForActivation);
                return new OnExitHandle(rcs);
            }
        }
    }
}

