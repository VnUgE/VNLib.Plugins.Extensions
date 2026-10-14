/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc
* File: IIpcExportConsumer.cs 
*
* IIpcExportConsumer.cs is part of VNLib.Plugins.Extensions.Ipc which is part of the larger 
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

namespace VNLib.Plugins.Extensions.Ipc
{
    /// <summary>
    /// Defines a callback contract for receiving notifications when an exported
    /// object instance becomes available or is revoked by its producer. Implement this
    /// interface on a consumer type to allow subscribing to export lifecycle changes.
    /// </summary>
    public interface IIpcExportConsumer
    {
        /// <summary>
        /// Called when the exported instance associated with this consumer changes.
        /// </summary>
        /// <param name="instance">The exported object instance when it is available,
        /// or <see langword="null"/> when the export has been revoked or the producer
        /// has unloaded.</param>
        /// <remarks>
        /// This method is invoked by the IPC consumer worker on a background thread.
        /// Implementations must not block and should return promptly to avoid
        /// delaying export monitoring.
        /// </remarks>
        void OnInstanceChanged(object? instance);
    }
}
