/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc
* File: IpcObjectExport.cs 
*
* IpcObjectExport.cs is part of VNLib.Plugins.Extensions.Ipc which is part of the larger 
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
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Ipc
{
    /// <summary>
    /// Represents the result of an atomic IPC export lookup that captures the 
    /// export table's initialization state, the resolved object instance, and 
    /// the instance's exit task in a single consistent snapshot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="OnExitTask"/> field is tied to the exported instance's lifetime,
    /// not the producer's lifetime. When <see cref="Instance"/> is non-null,
    /// <see cref="OnExitTask"/> is guaranteed to be a valid, non-null task. When
    /// <see cref="Instance"/> is <see langword="null"/>, <see cref="OnExitTask"/>
    /// is also <see langword="null"/> because no instance exists to track.
    /// </para>
    /// </remarks>
    public readonly struct IpcObjectExport
    {
        /// <summary>
        /// Gets a value that indicates whether the IPC export table is initialized.
        /// </summary>
        public required bool Initialized { get; init; }

        /// <summary>
        /// The object instance exported by the producer under the supplied name, or <see langword="null"/> if 
        /// the export was not found.
        /// </summary>
        public required object? Instance { get; init; }

        /// <summary>
        /// Gets the exit task linked to the exported instance's lifetime. The task
        /// completes when the instance is unpublished or the producer is destroyed,
        /// allowing consumers to detect instance termination.
        /// <para>
        /// This field is <see langword="null"/> when no instance exists at the
        /// requested export name.
        /// </para>
        /// </summary>
        public required Task? OnExitTask { get; init; }

        /// <summary>
        /// Deconstructs the export result into its instance and exit handle.
        /// </summary>
        /// <param name="instance">The resolved object instance from the export table.</param>
        /// <param name="onExitTask">The <see cref="OnExitTask"/> linked to the exported instance's lifetime.</param>
        public void Deconstruct(out object? instance, out Task? onExitTask)
        {
            instance   = this.Instance;
            onExitTask = this.OnExitTask;
        }

        /// <summary>
        /// Deconstructs the export result into its initialization state, instance, and exit task.
        /// </summary>
        /// <param name="initialized">Whether the export table was successfully initialized.</param>
        /// <param name="instance">The resolved object instance from the export table.</param>
        /// <param name="onExitTask">The <see cref="OnExitTask"/> linked to the exported instance's lifetime.</param>
        public void Deconstruct(
            out bool initialized,
            out object? instance,
            out Task? onExitTask
        )
        {
            initialized = this.Initialized;
            instance    = this.Instance;
            onExitTask  = this.OnExitTask;
        }
    }
}
