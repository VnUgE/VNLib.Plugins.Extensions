/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ISecretReader.cs 
*
* ISecretReader.cs is part of VNLib.Plugins.Extensions.Loading which is 
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

using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Loading.Secrets.Readers
{
    /// <summary>
    /// Interface for reading secrets from various secret stores
    /// </summary>
    internal interface ISecretReader
    {
        /// <summary>
        /// The scheme used by this secret reader (e.g. "vault", "env", "file")
        /// </summary>
        string Scheme { get; }

        /// <summary>
        /// Gets a secret at the given secret path. 
        /// </summary>
        /// <param name="secretPath">The path defined by the user </param>
        /// <returns>An <see cref="ISecretResult"/> containing the secret value, or null if not found</returns>
        ISecretResult? GetSecret(string secretPath);

        /// <summary>
        /// Gets a secret at the given secret path.
        /// </summary>
        /// <param name="secretPath">
        /// The path defined by the user. 
        /// The path is the entire string portion of the secret after the scheme prefix.
        /// </param>
        /// <param name="cancellation"></param>
        /// <returns>A task that resolves a <see cref="ISecretResult"/> containing secret or null if not found</returns>
        Task<ISecretResult?> GetSecretAsync(string secretPath, CancellationToken cancellation);
    }
}
