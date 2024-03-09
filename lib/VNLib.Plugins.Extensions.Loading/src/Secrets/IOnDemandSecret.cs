/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IOnDemandSecret.cs 
*
* IOnDemandSecret.cs is part of VNLib.Plugins.Extensions.Loading which is 
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

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// A secret that can be fetched from it's backing store when needed
    /// to avoid storing sensitive information in memory long term
    /// </summary>
    public interface IOnDemandSecret
    {
        /// <summary>
        /// The name of the secret that will be fetched on demand
        /// </summary>
        string SecretName { get; }

        /// <summary>
        /// Fetches the secret value from the backing store
        /// synchronously
        /// </summary>
        /// <returns>The secret value if found, null otherwise</returns>
        ISecretResult? FetchSecret();

        /// <summary>
        /// Fetches the secret value from the backing store
        /// asynchronously
        /// </summary>
        /// <param name="cancellation">An optionall canceallation token to cancel the operation</param>
        /// <returns>A task that completes with the value of the secret if it exists</returns>
        Task<ISecretResult?> FetchSecretAsync(CancellationToken cancellation = default);
    }
}
