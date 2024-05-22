/*
* Copyright (c) 2024 Vaughn Nugent
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

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// A secret client interace for reading secrets from a vault server
    /// </summary>
    public interface IKvVaultClient
    {
        /// <summary>
        /// Reads a single KeyValue secret from the vault server asyncrhonously and returns the result
        /// or null if the secret does not exist
        /// </summary>
        /// <param name="path">The path to the item within the store</param>
        /// <param name="mountPoint">The vault mount points</param>
        /// <param name="secretName">The name of the secret within the property array to retrieve</param>
        /// <returns>The secret wrapper if found, null otherwise</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="HCVaultException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        Task<ISecretResult?> ReadSecretAsync(string path, string mountPoint, string secretName);

        /// <summary>
        /// Reads a single KeyValue secret from the vault server syncrhonously and returns the result
        /// or null if the secret does not exist
        /// </summary>
        /// <param name="path">The path to the item within the store</param>
        /// <param name="mountPoint">The vault mount points</param>
        /// <param name="secretName">The name of the secret within the property array to retrieve</param>
        /// <returns>The secret wrapper if found, null otherwise</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="HCVaultException"></exception>
        /// <exception cref="HttpRequestException"></exception>
        ISecretResult? ReadSecret(string path, string mountPoint, string secretName);
    }
}
