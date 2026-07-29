/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Memory;
using VNLib.Plugins.Extensions.Loading.Secrets.Readers;

using static VNLib.Plugins.Extensions.Loading.Secrets.PluginSecretConstants;

namespace VNLib.Plugins.Extensions.Loading.Secrets
{
    /// <summary>
    /// A secret store for a plugin that can be used to fetch secrets from plugin configuration
    /// </summary>
    /// <param name="plugin">The plugin instance to get secrets from</param>
    public readonly struct PluginSecretStore(PluginBase plugin) : IEquatable<PluginSecretStore>
    {
        internal const int HCVaultDefaultKvVersion = 2;

        private readonly PluginBase plugin = plugin;
        private readonly PluginSecretState _state = plugin.Deps().GetOrCreateSingleton(PluginSecretState.LoadState);

        /// <summary>
        /// Gets the ambient vault client for the current plugin
        /// if the configuration is loaded, null otherwise
        /// </summary>
        /// <returns>The ambient <see cref="IKvVaultClient"/> if configuration is loaded; otherwise, <see langword="null" />.</returns>
        /// <exception cref="KeyNotFoundException">The vault configuration is missing required keys.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been disposed.</exception>
        public IKvVaultClient? GetVaultClient() => _state.VaultClient;

        /// <summary>
        /// Checks if a named secret is set in the plugin configuration. 
        /// It does not check if the secret has a value, only if it's defined.
        /// </summary>
        /// <param name="secretName">The name of the secret to search for</param>
        /// <returns>True if the system configuration has a key set for the secret name within the secret configuration element</returns>
        public readonly bool IsSet(string secretName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            /*
            * A secret is defined if an element is found in either the plugin or host config.
            * Plugin is always checked first.
            */
            return (plugin.PluginConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement secConfig) && HasNamedPropertyInEl(in secConfig, secretName))
                || (plugin.HostConfig.TryGetProperty(SECRETS_CONFIG_KEY, out secConfig) && HasNamedPropertyInEl(in secConfig, secretName));
        
            // Determines if the enumerated element contains objects that 
            // have the case-insensitive name.
            static bool HasNamedPropertyInEl(in JsonElement el, string secretName)
            {
                foreach (JsonProperty prop in el.EnumerateObject())
                {
                    if (string.Equals(prop.Name, secretName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Attempts to get a secret from the "secrets" element by name asynchronously.
        /// </summary>
        /// <param name="secretName">The name of the secret in the secret config to read</param>
        /// <param name="cancellation">A token to cancel the asynchronous operation</param>
        /// <returns>A task that resolves the secret result if found, otherwise a task that resolves null</returns>
        public readonly Task<ISecretResult?> TryGetAsync(string secretName, CancellationToken cancellation = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            string? rawValue = TryGetSecretFromConfig(plugin, secretName);

            return rawValue is null 
                ? Task.FromResult<ISecretResult?>(null) 
                : GetSecretAsync(_state, rawValue, cancellation);
        }

        /// <summary>
        /// Attempts to get a secret from the "secrets" element by name.
        /// </summary>
        /// <param name="secretName">The name of the secret in the secret config to read</param>
        /// <returns>The secret result if found, null otherwise</returns>
        public readonly ISecretResult? TryGet(string secretName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            string? rawValue = TryGetSecretFromConfig(plugin, secretName);

            return rawValue is null ? null : GetSecret(_state, rawValue);
        }

        /// <summary>
        /// <para>
        /// Gets a required secret from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and the plugin's local 'secrets' element before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="secretName">The name of the secret property to get</param>
        /// <param name="cancellation">A token to cancel the asynchronous operation</param>
        /// <returns>The element from the configuration file with the given name, raises an exception if the secret does not exist</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public Task<ISecretResult> GetAsync(string secretName, CancellationToken cancellation = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            Task<ISecretResult?> resultTask = TryGetAsync(secretName, cancellation);

            return AwaitRequiredSecretAsync(resultTask, secretName);

            static async Task<ISecretResult> AwaitRequiredSecretAsync(Task<ISecretResult?> resultTask, string secretName)
            {
                ISecretResult? res = await resultTask.ConfigureAwait(false);
                return res ?? throw new KeyNotFoundException($"Missing required secret {secretName}");
            }
        }

        /// <summary>
        /// Gets an on-demand secret that can be used to fetch the secret value from it's 
        /// store when needed.
        /// </summary>
        /// <param name="secretName">The name of the secret in the secret config to read</param>
        /// <returns>An on-demand secret instance</returns>
        public readonly IOnDemandSecret GetOnDemandSecret(string secretName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            string? rawValue = TryGetSecretFromConfig(plugin, secretName);

            return new OnDemandSecret(
                _state, 
                secretName: secretName, 
                rawValue: rawValue
            );
        }       

        ///<inheritdoc/>
        [Obsolete("Use TryGetAsync instead")]
        public readonly Task<ISecretResult?> TryGetSecretAsync(string secretName, CancellationToken cancellation = default)
            => TryGetAsync(secretName, cancellation);

        ///<inheritdoc/>
        [Obsolete("Use TryGet instead")]
        public readonly ISecretResult? TryGetSecret(string secretName)
            => TryGet(secretName);

        ///<inheritdoc/>
        public override bool Equals(object? obj) => obj is PluginSecretStore store && Equals(store);

        ///<inheritdoc/>
        public static bool operator ==(PluginSecretStore left, PluginSecretStore right) => left.Equals(right);

        ///<inheritdoc/>
        public static bool operator !=(PluginSecretStore left, PluginSecretStore right) => !(left == right);

        /// <inheritdoc/>
        public bool Equals(PluginSecretStore other) => ReferenceEquals(other.plugin, plugin);

        ///<inheritdoc/>
        public override int GetHashCode() => plugin.GetHashCode();

        private static string? TryGetSecretFromConfig(PluginBase plugin, string secretName)
        {
            bool local = plugin.PluginConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement localEl);
            bool host = plugin.HostConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement hostEl);

            if (!local && !host)
            {
                return null;
            }

            /*
             * Merge secrets from host and plugin configs into a single case-insensitive lookup.
             * Host entries are written first; plugin entries follow and overwrite any matching
             * key (ordinal-ignore-case), so plugin config always takes precedence.
             *
             * If a key appears more than once within the same config object (malformed JSON),
             * the last occurrence wins — consistent with JsonElement.EnumerateObject() traversal.
             */
            Dictionary<string, JsonElement> conf = new(StringComparer.OrdinalIgnoreCase);

            if (host)
            {
                foreach (JsonProperty p in hostEl.EnumerateObject())
                {
                    conf[p.Name] = p.Value;
                }
            }

            if (local)
            {
                foreach (JsonProperty p in localEl.EnumerateObject())
                {
                    conf[p.Name] = p.Value;
                }
            }

            return conf.TryGetValue(secretName, out JsonElement el) ? el.GetString() : null;
        }

        private static ISecretResult? GetSecret(PluginSecretState state, string rawValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);

            /*
             * See if external secret scheme format is being used, if so
             * it will need to use an external reader to resolve the secret value.
             * 
             * Copy the raw string value to a new secret value. 
             * Read the security note on the _rawSecretValue field
             * above.
             */

            if (!rawValue.Contains("://", StringComparison.Ordinal))
            {
                return SecretResult.ToSecret(rawValue);
            }

            // Try fetching the secret reader for the scheme, otherwise not supported

            string[] schemeAndPath = rawValue.Split("://", StringSplitOptions.RemoveEmptyEntries);

            string scheme = schemeAndPath[0];
            string secretPath = schemeAndPath[1];

            return state.Readers.TryGetValue(scheme, out ISecretReader? reader)
                ? reader.GetSecret(secretPath)
                : throw new NotSupportedException($"Secret scheme {scheme} is not supported");
        }

        private static Task<ISecretResult?> GetSecretAsync(
            PluginSecretState state, 
            string rawValue, 
            CancellationToken cancellation
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);

            /*
             * See if external secret scheme format is being used, if so
             * it will need to use an external reader to resolve the secret value.
             * 
             * Copy the raw string value to a new secret value. 
             * Read the security note on the rawValue field
             * above.
             */

            if (!rawValue.Contains("://", StringComparison.Ordinal))
            {
                return Task.FromResult<ISecretResult?>(SecretResult.ToSecret(rawValue));
            }

            // Try fetching the secret reader for the scheme, otherwise not supported

            string[] schemeAndPath = rawValue.Split("://", StringSplitOptions.RemoveEmptyEntries);

            string scheme = schemeAndPath[0];
            string secretPath = schemeAndPath[1];

            return state.Readers.TryGetValue(scheme, out ISecretReader? reader)
                ? reader.GetSecretAsync(secretPath, cancellation)
                : Task.FromException<ISecretResult?>(new NotSupportedException($"Secret scheme {scheme} is not supported"));
        }
       

        private sealed record PluginSecretState(
            IKvVaultClient? VaultClient,
            FrozenDictionary<string, ISecretReader> Readers
        )
        {
            public static PluginSecretState LoadState(PluginBase plugin)
            {
                // Load the built-in readers, these are always available regardless of vault configuration
                // since they rely on platform features
                List<ISecretReader> readers = [
                    new EnvironmentSecretReader(),
                    new FileSecretReader()
                ];

                // Try to load the vault
                IKvVaultClient? vault = LoadVaultClient(plugin);
                if (vault != null)
                {
                    readers.Add(new VaultSecretReader(vault));
                }

                // Init state from loaded vault and readers, the readers will be used to resolve secrets
                // on demand when requested by the plugin
                return new PluginSecretState(
                    VaultClient: vault,
                    Readers: readers.ToFrozenDictionary(r => r.Scheme, StringComparer.OrdinalIgnoreCase)
                );
            }

            private static IKvVaultClient? LoadVaultClient(PluginBase plugin)
            {
                IConfigScope? customVaultConf = plugin.Config().TryGet(CUSTOM_KV_CONFIG);
                KvVaultConfig? kvVaultConfig = customVaultConf?.Deserialize<KvVaultConfig>();

                // Try loading custom vault first
                if (kvVaultConfig is not null)
                {
                    if (!string.IsNullOrWhiteSpace(kvVaultConfig.CustomAssemblyPath))
                    {
                        return plugin.Deps()
                            .LoadExternal<IKvVaultClient>(kvVaultConfig.CustomAssemblyPath);
                    }
                }

                // Fallback to HCP Vault
                IConfigScope? hcpVaultConf = plugin.Config().TryGet(VAULT_OBJECT_NAME);

                return hcpVaultConf is null ? null : (IKvVaultClient)LoadHcpVault(hcpVaultConf);
            }

            private static HCVaultClient LoadHcpVault(IConfigScope conf)
            {
                //Get auth token from config, then fall back to environment variable
                string? envAuthToken =  Environment.GetEnvironmentVariable(VAULT_TOKEN_ENV_NAME);
                string? authToken = conf.GetValueOrDefault(VAULT_TOKEN_KEY, envAuthToken!);

                _ = authToken ?? throw new KeyNotFoundException($"HCP Vault authentication token required. Set {VAULT_OBJECT_NAME} or env:{VAULT_TOKEN_ENV_NAME}");

                //create vault client, invalid or nulls will raise exceptions here
                return HCVaultClient.Create(
                     serverAddress: conf.GetRequiredProperty(VAULT_URL_KEY, p => p.GetString()!),
                     authToken,
                     kvVersion: conf.GetValueOrDefault(VAULT_KV_VERSION_KEY, HCVaultDefaultKvVersion),
                     trustCert: conf.GetValueOrDefault(VAULT_TRUST_CERT_KEY, false),
                     heap: MemoryUtil.Shared
                );
            }

            private sealed class KvVaultConfig
            {
                [JsonPropertyName("assembly_name")]
                public string? CustomAssemblyPath { get; set; }
            }
        }

        private sealed class OnDemandSecret(PluginSecretState state, string secretName, string? rawValue) : IOnDemandSecret
        {
            public string SecretName { get; } = secretName ?? throw new ArgumentNullException(nameof(secretName));

            /*
             * Caches the raw config value for on-demand resolution.
             *
             * This field stores the value exactly as read from the plugin or host
             * configuration JSON. It is NOT the resolved secret itself — it may be
             * a scheme-prefixed URI (e.g. "vault://secret/path?secret=key",
             * "env://MY_VAR", "file:///path/to/secret") or, in the simplest case,
             * a plaintext value that IS the secret.
             *
             * SECURITY NOTE:
             * - For scheme-prefixed URIs, the actual secret is never held here;
             *   it is resolved on demand by the appropriate ISecretReader and
             *   cleared from memory as soon as the ISecretResult is disposed.
             * - For plaintext values (no "://" scheme), the value is both the
             *   config entry and the secret. Storing a second managed string copy
             *   is no worse than the config document already loaded in memory,
             *   but consumers should prefer scheme-based references to minimize
             *   the window during which secret data resides in the managed heap.
             * - The string is immutable and cannot be zeroed. Prefer the FetchSecret
             *   or FetchSecretAsync methods which return IDisposable ISecretResult
             *   instances whose buffers are wiped on disposal.
             */
            private readonly string? _rawSecretValue = rawValue;

            ///<inheritdoc/>
            public ISecretResult? FetchSecret() => _rawSecretValue is null ? null : GetSecret(state, _rawSecretValue);

            ///<inheritdoc/>
            public Task<ISecretResult?> FetchSecretAsync(CancellationToken cancellation)
            {
                return _rawSecretValue is null
                    ? Task.FromResult<ISecretResult?>(null)
                    : GetSecretAsync(state, _rawSecretValue, cancellation);
            }
        }
    }
}
