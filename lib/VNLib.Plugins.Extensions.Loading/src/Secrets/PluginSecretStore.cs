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
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Memory;
using VNLib.Utils.Resources;

using VNLib.Plugins.Extensions.Loading.Configuration;
using VNLib.Plugins.Extensions.Loading.Secrets.Readers;

namespace VNLib.Plugins.Extensions.Loading.Secrets
{
    /// <summary>
    /// A secret store for a plugin that can be used to fetch secrets from plugin configuration
    /// </summary>
    /// <param name="plugin">The plugin instance to get secrets from</param>
    public readonly struct PluginSecretStore(PluginBase plugin) : IEquatable<PluginSecretStore>
    {
        /// <summary>
        /// The default HashiCorp Vault KV secrets engine version used when
        /// <c>kv_version</c> is not specified in the <c>hcp_vault</c> configuration.
        /// </summary>
        private const int HCVaultDefaultKvVersion = 2;

        /// <summary>
        /// The object property within the configuration store that identifies secrets within the host 
        /// and plugin configuration document.
        /// </summary>
        private const string SecretsConfigKey = "secrets";

        /// <summary>
        /// The environment variable name/key string used to identify the HCP Vault token from an 
        /// environment variable. This matches Hashicorp default name for tokens.
        /// </summary>
        private const string VaultTokenEnvName = "VAULT_TOKEN";

        private readonly PluginBase _plugin = plugin;
        private readonly PluginSecretState _state = plugin.Deps().GetOrCreateSingleton<PluginSecretState>();

        /// <summary>
        /// Gets the ambient vault client for the current plugin
        /// if the configuration is loaded, null otherwise
        /// </summary>
        /// <returns>The ambient <see cref="IKvVaultClient"/> if configuration is loaded; otherwise, <see langword="null" />.</returns>
        /// <exception cref="KeyNotFoundException">The vault configuration is missing required keys.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been disposed.</exception>
        public IKvVaultClient? GetVaultClient()
        {
            // If config is defined, attempt to load the vault client
            return _plugin.Config().HasForType<LazyVaultClient>() 
                ? _plugin.Deps().GetOrCreateSingleton<LazyVaultClient>().Client.Instance 
                : null;
        }

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
            return HasNamedSecret(_plugin.PluginConfig, secretName) 
                || HasNamedSecret(_plugin.HostConfig, secretName);
        
            // Determines if the enumerated element contains objects that 
            // have the case-insensitive name.
            static bool HasNamedSecret(JsonElement secretEl, string secretName)
            {
                if (!secretEl.TryGetProperty(SecretsConfigKey, out JsonElement el))
                {
                    return false;
                }

                Validate.Assert(
                    el.ValueKind == JsonValueKind.Object,
                    message: $"The '{SecretsConfigKey}' configuration element must be a JSON object, but got {el.ValueKind} in plugin/host config"
                );

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

            string? rawValue = TryGetSecretFromConfig(_plugin, secretName);

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

            string? rawValue = TryGetSecretFromConfig(_plugin, secretName);

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
        /// <returns>An <see cref="ISecretResult"/> for the required secret, raises an exception if the secret does not exist</returns>
        /// <exception cref="KeyNotFoundException">The required secret does not exist in the merged secrets elements.</exception>
        /// <exception cref="ObjectDisposedException">The secret store has been disposed.</exception>
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
        /// Gets an on-demand secret that can be used to fetch the secret value from its 
        /// store when needed.
        /// </summary>
        /// <param name="secretName">The name of the secret in the secret config to read</param>
        /// <returns>An on-demand secret instance</returns>
        public readonly IOnDemandSecret GetOnDemandSecret(string secretName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

            string? rawValue = TryGetSecretFromConfig(_plugin, secretName);

            return new OnDemandSecret(
                _state, 
                secretName: secretName, 
                rawValue: rawValue
            );
        }       

        ///<inheritdoc cref="TryGetAsync(string, CancellationToken)"/>
        [Obsolete("Use TryGetAsync instead")]
        public readonly Task<ISecretResult?> TryGetSecretAsync(string secretName, CancellationToken cancellation = default)
            => TryGetAsync(secretName, cancellation);

        ///<inheritdoc cref="TryGet(string)"/>
        [Obsolete("Use TryGet instead")]
        public readonly ISecretResult? TryGetSecret(string secretName)
            => TryGet(secretName);

        ///<inheritdoc/>
        public override bool Equals(object? obj) 
            => obj is PluginSecretStore store && Equals(store);

        ///<inheritdoc/>
        public static bool operator ==(PluginSecretStore left, PluginSecretStore right) 
            => left.Equals(right);

        ///<inheritdoc/>
        public static bool operator !=(PluginSecretStore left, PluginSecretStore right) 
            => !(left == right);

        /// <inheritdoc/>
        public bool Equals(PluginSecretStore other) => ReferenceEquals(other._plugin, _plugin);

        ///<inheritdoc/>
        public override int GetHashCode() => _plugin.GetHashCode();

        private static (string, string) ParseSchemeAndPath(string rawValue)
        {
            string[] parts = rawValue.Split("://", StringSplitOptions.RemoveEmptyEntries);

            return parts.Length == 2
                ? (parts[0], parts[1])
                : throw new FormatException($"Invalid secret scheme format: '{rawValue}'. Expected 'scheme://path'.");
        }

        private static string? TryGetSecretFromConfig(PluginBase plugin, string secretName)
        {
            bool local = plugin.PluginConfig.TryGetProperty(SecretsConfigKey, out JsonElement localEl);
            bool host = plugin.HostConfig.TryGetProperty(SecretsConfigKey, out JsonElement hostEl);

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
                Validate.Assert(
                    hostEl.ValueKind == JsonValueKind.Object,
                    message: $"The '{SecretsConfigKey}' configuration element must be a JSON object, but got {hostEl.ValueKind} in host config"
                );

                foreach (JsonProperty p in hostEl.EnumerateObject())
                {
                    conf[p.Name] = p.Value;
                }
            }

            if (local)
            {
                Validate.Assert(
                    localEl.ValueKind == JsonValueKind.Object,
                    message: $"The '{SecretsConfigKey}' configuration element must be a JSON object, but got {localEl.ValueKind} in plugin config"
                );

                foreach (JsonProperty p in localEl.EnumerateObject())
                {
                    conf[p.Name] = p.Value;
                }
            }

            // If secret was found ensure it's a string and return it
            if (conf.TryGetValue(secretName, out JsonElement el))
            {
                Validate.Assert(
                    el.ValueKind == JsonValueKind.String,
                    message: $"Secret {secretName} config value exists, but is {el.ValueKind} but must be a string."
                );

                return el.GetString();
            }

            return null;
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

            if (rawValue.Contains("://", StringComparison.Ordinal))
            {
                // Try to process the scheme and see if a reader has a handler registered
                (string scheme, string secretPath) = ParseSchemeAndPath(rawValue);
                if (state.Readers.TryGetValue(scheme, out ISecretReader? reader))
                {
                    return reader.GetSecret(secretPath);
                }
            }

            // Fall back to return raw value
            return SecretResult.ToSecret(rawValue);         
        }

        private static Task<ISecretResult?> GetSecretAsync(
            PluginSecretState state, 
            string rawValue, 
            CancellationToken cancellation
        )
        {
            // Empty or whitespace strings are allowed and will fall through
            // to raw secret value
            ArgumentNullException.ThrowIfNull(rawValue);

            /*
             * See if external secret scheme format is being used, if so
             * it will need to use an external reader to resolve the secret value.
             * 
             * Copy the raw string value to a new secret value. 
             * Read the security note on the rawValue field
             * above.
             */

            if (rawValue.Contains("://", StringComparison.Ordinal))
            {
                (string scheme, string secretPath) = ParseSchemeAndPath(rawValue);

                if (state.Readers.TryGetValue(scheme, out ISecretReader? reader))
                {
                    return reader.GetSecretAsync(secretPath, cancellation);
                }
            }

            return Task.FromResult<ISecretResult?>(SecretResult.ToSecret(rawValue));         
        }       

        private sealed class PluginSecretState
        {
            public readonly FrozenDictionary<string, ISecretReader> Readers;

            public PluginSecretState(PluginBase plugin)
            {
                // Load the built-in readers, these are always available regardless of vault configuration
                // since they rely on platform features
                List<ISecretReader> readers = [
                    new EnvironmentSecretReader(),
                    new FileSecretReader()
                ];

                // If config is loaded for the vault, 
                if (plugin.Config().HasForType<LazyVaultClient>())
                {
                    LazyVaultClient client = plugin.Deps()
                                                   .GetOrCreateSingleton<LazyVaultClient>();

                    readers.Add(new VaultSecretReader(client.Client));
                }

                // Init state from loaded vault and readers, the readers will be used to resolve secrets
                // on demand when requested by the plugin
                Readers = readers.ToFrozenDictionary(r => r.Scheme, StringComparer.OrdinalIgnoreCase);
            }          
        }

        [ConfigurationName("vault_client")]
        private sealed class LazyVaultClient
        {
            public readonly LazyInitializer<IKvVaultClient> Client;

            public LazyVaultClient(PluginBase plugin, IConfigScope config)
            {
                KvVaultConfig kvVaultConfig = config.DeserializeAndValidate<KvVaultConfig>();

                switch (kvVaultConfig.Type)
                {
                    case "hcp":
                        Client = new(() => LoadHcpVault(kvVaultConfig.HcpVaultConfig!));
                        break;

                    case "external":
                        Client = new(
                            () => plugin.Deps().LoadExternal<IKvVaultClient>(kvVaultConfig.CustomAssemblyPath!)
                        );

                        break;

                    default:
                        Debug.Fail("Config validation failed to detect config type");
                        throw new NotSupportedException("Invalid vault type detected with validation failure. This is a bug");
                }      
            }

            private static HCVaultClient LoadHcpVault(HcpVaultConfig conf)
            {
                //Get auth token from config, then fall back to environment variable
                string authToken = conf.Token 
                    ?? Environment.GetEnvironmentVariable(VaultTokenEnvName)
                    ?? throw new KeyNotFoundException($"HCP Vault authentication token required. Set 'hcp_vault.token' or env:{VaultTokenEnvName}");

                //create vault client, invalid or nulls will raise exceptions here
                return HCVaultClient.Create(
                     serverAddress: conf.Url!,
                     authToken,
                     kvVersion: conf.KvVersion,
                     trustCert: conf.TrustCert,
                     heap: MemoryUtil.Shared
                );
            }

            private sealed class KvVaultConfig : IOnConfigValidation
            {
                [JsonPropertyName("type")]
                public string Type { get; init; } = "";

                [JsonPropertyName("assembly_name")]
                public string? CustomAssemblyPath { get; init; }

                [JsonPropertyName("hcp_vault")]
                public HcpVaultConfig? HcpVaultConfig { get; init; }

                public void OnValidate()
                {
                    switch (Type)
                    {
                        case "hcp":
                            Validate.NotNull(HcpVaultConfig, "'hcp_vault' property must not be null when using HCP Vault mode");
                            HcpVaultConfig.OnValidate();
                            break;

                        case "external":
                            Validate.NotNull(CustomAssemblyPath, "'assembly_name' must not be null or empty when using type 'external'");
                            Validate.Matches(
                                CustomAssemblyPath,
                                pattern: @"(?i)\.dll$",
                                message: "'assembly_name' must reference a .NET assembly (.dll) file."
                            );
                            break;

                        default:
                            throw new ConfigurationException($"Vault client type '{Type}' is not supported");
                    }
                }
            }

            private sealed class HcpVaultConfig : IOnConfigValidation
            {
                [JsonPropertyName("url")]
                public string? Url { get; init; }

                [JsonPropertyName("token")]
                public string? Token { get; init; }

                [JsonPropertyName("kv_version")]
                public int KvVersion { get; init; } = HCVaultDefaultKvVersion;

                [JsonPropertyName("trust_certificate")]
                public bool TrustCert { get; init; } = false;              

                public void OnValidate()
                {
                    Validate.NotNull(Url, "HCP Vault url may not be empty or whitespace when declaring vault");
                    Validate.Assert(
                        Uri.TryCreate(Url, UriKind.Absolute, out Uri? _),
                        message: "HCP Vault 'url' is not a valid absolute http(s) url."
                    );

                    Validate.Range(KvVersion, 1, 2, "vault_client::hcp_vault::kv_version");
                }
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
