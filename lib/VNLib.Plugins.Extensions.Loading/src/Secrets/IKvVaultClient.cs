/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IKvVaultClient.cs 
*
* IKvVaultClient.cs is part of VNLib.Plugins.Extensions.Loading which is
* part of the larger VNLib collection of libraries and utilities.
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
using System.Net.Http;
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Loading.Secrets
{
    /// <summary>
    /// Represents a client interface for reading key-value secrets from a vault server.
    /// </summary>
    public interface IKvVaultClient
    {
        /// <summary>
        /// Reads a single key-value secret from the vault server asynchronously.
        /// </summary>
        /// <param name="path">The path to the item within the store.</param>
        /// <param name="mountPoint">The vault mount point.</param>
        /// <param name="secretName">The name of the secret within the property array to retrieve.</param>
        /// <returns>The secret result if found; otherwise, <see langword="null" />.</returns>
        /// <exception cref="ArgumentException">A required argument is invalid.</exception>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="HCVaultException">A vault operation failed.</exception>
        /// <exception cref="HttpRequestException">The HTTP request to the vault server failed.</exception>
        Task<ISecretResult?> ReadSecretAsync(string path, string mountPoint, string secretName);

        /// <summary>
        /// Reads a single key-value secret from the vault server synchronously.
        /// </summary>
        /// <param name="path">The path to the item within the store.</param>
        /// <param name="mountPoint">The vault mount point.</param>
        /// <param name="secretName">The name of the secret within the property array to retrieve.</param>
        /// <returns>The secret result if found; otherwise, <see langword="null" />.</returns>
        /// <exception cref="ArgumentException">A required argument is invalid.</exception>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="HCVaultException">A vault operation failed.</exception>
        /// <exception cref="HttpRequestException">The HTTP request to the vault server failed.</exception>
        ISecretResult? ReadSecret(string path, string mountPoint, string secretName);
    }
}
