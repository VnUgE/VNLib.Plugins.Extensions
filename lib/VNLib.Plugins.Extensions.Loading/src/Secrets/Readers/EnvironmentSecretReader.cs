/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: EnvironmentSecretReader.cs 
*
* EnvironmentSecretReader.cs is part of VNLib.Plugins.Extensions.Loading which is 
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
using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Plugins.Extensions.Loading.Secrets.Readers
{
    /// <summary>
    /// A built-in secret reader that reads secrets from environment variables. Uses the 
    /// path "env://VARIABLE_NAME".
    /// </summary>
    internal sealed class EnvironmentSecretReader : ISecretReader
    {
        /// <inheritdoc/>
        public string Scheme => "env";

        /// <inheritdoc/>
        public ISecretResult? GetSecret(string secretPath)
        {
            ArgumentNullException.ThrowIfNull(secretPath);

            string? envVal = Environment.GetEnvironmentVariable(secretPath);

            return envVal == null ? null : SecretResult.ToSecret(envVal);
        }

        /// <inheritdoc/>
        public Task<ISecretResult?> GetSecretAsync(string secretPath, CancellationToken cancellation)
        {
            ArgumentNullException.ThrowIfNull(secretPath);

            string? envVal = Environment.GetEnvironmentVariable(secretPath);

            return Task.FromResult<ISecretResult?>(envVal == null ? null : SecretResult.ToSecret(envVal));
        }
    }
}
