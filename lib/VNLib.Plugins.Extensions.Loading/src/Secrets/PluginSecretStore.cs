/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginSecretStore.cs 
*
* PluginSecretStore.cs is part of VNLib.Plugins.Extensions.Loading which is 
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils.Memory;

using static VNLib.Plugins.Extensions.Loading.PluginSecretConstants;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// A secret store for a plugin that can be used to fetch secrets from plugin configuration
    /// </summary>
    /// <param name="plugin">The plugin instance to get secrets from</param>
    public readonly struct PluginSecretStore(PluginBase plugin) : IEquatable<PluginSecretStore>
    {
        const int HCVaultDefaultKvVersion = 2;

        private readonly PluginBase _plugin = plugin;

        /// <summary>
        /// Gets the ambient vault client for the current plugin
        /// if the configuration is loaded, null otherwise
        /// </summary>
        /// <returns>The ambient <see cref="IKvVaultClient"/> if loaded, null otherwise</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public IKvVaultClient? GetVaultClient() => LoadingExtensions.GetOrCreateSingleton(_plugin, TryGetVaultLoader);

        private static HCVaultClient? TryGetVaultLoader(PluginBase pbase)
        {
            //Get vault config
            IConfigScope? conf = pbase.TryGetConfig(VAULT_OBJECT_NAME);

            if (conf is null)
            {
                return null;
            }

            //try get server address creds from config
            string serverAddress = conf.GetRequiredProperty(VAULT_URL_KEY, p => p.GetString()!);
            bool trustCert = conf.GetValueOrDefault(VAULT_TRUST_CERT_KEY, el => el.GetBoolean(), false);

            string? authToken;
            
            if (conf.TryGetValue(VAULT_TOKEN_KEY, out JsonElement tokenEl))
            {
                //Init token
                authToken = tokenEl.GetString();
            }
            //Try to get the token as an environment variable
            else if (Environment.GetEnvironmentVariable(VAULT_TOKEN_ENV_NAME) != null)
            {
                authToken = Environment.GetEnvironmentVariable(VAULT_TOKEN_ENV_NAME)!;
            }
            else
            {
                throw new KeyNotFoundException($"Failed to load the vault authentication method from {VAULT_OBJECT_NAME}");
            }

            _ = authToken ?? throw new KeyNotFoundException($"Failed to load the vault authentication method from {VAULT_OBJECT_NAME}");

            //Check for vault kv version, otherwise use the default
            int version = conf.GetValueOrDefault(VAULT_KV_VERSION_KEY, el => el.GetInt32(), HCVaultDefaultKvVersion);

            //create vault client, invalid or nulls will raise exceptions here
            return HCVaultClient.Create(serverAddress, authToken, version, trustCert, MemoryUtil.Shared);
        }

        ///<inheritdoc/>
        public Task<ISecretResult?> TryGetSecretAsync(string secretName, CancellationToken cancellation = default)
        {
            IOnDemandSecret secret = GetOnDemandSecret(secretName);
            return secret.FetchSecretAsync(cancellation);
        }

        ///<inheritdoc/>
        public ISecretResult? TryGetSecret(string secretName)
        {
            IOnDemandSecret secret = GetOnDemandSecret(secretName);
            return secret.FetchSecret();
        }

        ///<inheritdoc/>
        public IOnDemandSecret GetOnDemandSecret(string secretName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
            return new OnDemandSecret(_plugin, secretName, GetVaultClient);
        }

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is PluginSecretStore store && Equals(store);

        ///<inheritdoc/>
        public static bool operator ==(PluginSecretStore left, PluginSecretStore right) => left.Equals(right);

        ///<inheritdoc/>
        public static bool operator !=(PluginSecretStore left, PluginSecretStore right) => !(left == right);

        /// <inheritdoc/>
        public bool Equals(PluginSecretStore other) => ReferenceEquals(other._plugin, _plugin);

        ///<inheritdoc/>
        public override int GetHashCode() => _plugin.GetHashCode();
    }
}
