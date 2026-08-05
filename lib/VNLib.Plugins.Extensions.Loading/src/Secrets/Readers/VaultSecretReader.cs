/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: VaultSecretReader.cs
*
* VaultSecretReader.cs is part of VNLib.Plugins.Extensions.Loading which is
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

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading.Secrets.Readers
{
    /// <summary>
    /// A built-in secret reader that reads secrets from HashiCorp Vault.
    /// Secrets are referenced using the <c>vault://</c> scheme prefix in configuration
    /// (e.g., <c>vault://mount/path?secret=key</c>). The scheme prefix is stripped before
    /// the path is passed to this reader.
    /// </summary>
    internal sealed class VaultSecretReader(IKvVaultClient vaultClient) : ISecretReader
    {
        /// <inheritdoc/>
        public string Scheme => "vault";

        /// <inheritdoc/>
        public ISecretResult? GetSecret(string secretPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretPath);

            GetVaultComponents(
                secretPath,
                out string mount,
                out string secret,
                out string secretTableKey
           );

            return vaultClient.ReadSecret(secret, mount, secretTableKey);
        }
        /// <inheritdoc/>
        public Task<ISecretResult?> GetSecretAsync(string secretPath, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretPath);

            GetVaultComponents(
                secretPath,
                out string mount,
                out string secret,
                out string secretTableKey
            );

            return vaultClient.ReadSecretAsync(secret, mount, secretTableKey);
        }

        /*
         * Recovers the vault components from the given vault path
         * in the format
         *
         * [mount-name]/[secret-path]?secret=[secret_name]
         *
         * The leading scheme (vault://) has already been removed
         */

        private static void GetVaultComponents(
            ReadOnlySpan<char> vaultPath,
            out string mount,
            out string secret,
            out string secretTableKey
        )
        {
            // Slice off path
            ReadOnlySpan<char> path = vaultPath.SliceBeforeParam('?');
            ReadOnlySpan<char> query = vaultPath.SliceAfterParam('?');

            if (path.IsEmpty)
            {
                throw new UriFormatException("Vault secret location not valid/empty");
            }

            // Get the secret table key
            secretTableKey = query.SliceAfterParam("secret=").SliceBeforeParam('&').ToString();

            // get mount and path
            int lastSep = path.IndexOf('/');

            if (lastSep <= 0)
            {
                throw new UriFormatException("Vault secret location must contain mount and secret in the form '<mount>/<secret>'");
            }

            mount = path[..lastSep].ToString();
            secret = path[(lastSep + 1)..].ToString();
        }
    }
}
