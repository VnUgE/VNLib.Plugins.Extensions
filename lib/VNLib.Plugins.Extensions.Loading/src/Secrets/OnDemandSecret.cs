/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: OnDemandSecret.cs 
*
* OnDemandSecret.cs is part of VNLib.Plugins.Extensions.Loading which is 
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Utils.Resources;

using static VNLib.Plugins.Extensions.Loading.Secrets.PluginSecretConstants;

namespace VNLib.Plugins.Extensions.Loading.Secrets
{
    internal sealed class OnDemandSecret(PluginBase plugin, string secretName, Func<IKvVaultClient?> vaultCb) : IOnDemandSecret
    {
        /*
         * Defer loading vault until needed by a vault secret. This avoids loading the vault client
         * if no secrets are needed from the vault.
         */
        private readonly LazyInitializer<IKvVaultClient?> vault = new(vaultCb);

        public string SecretName { get; } = secretName ?? throw new ArgumentNullException(nameof(secretName));

        /*
         * Caching the raw secret (read from the config file here)
         * 
         * SECURITY NOTE: 
         * It is assumed that secrets stored in plaintext in the configuration file 
         * are not any more secret than storing a copy of it's string value in memory
         * right here. The configuration file is always loaded into memory so it's not 
         * any worse albeit haveing a second copy.
         * 
         * That said, all secrets derrived (loaded) from this secret, env variable, 
         * file, or vault, are read on demand and cleared from memory as soon as possible.
         * So these methods are considered much more secure.
         */
        private readonly string? _rawSecretValue = TryGetSecretFromConfig(plugin, secretName);

        ///<inheritdoc/>
        public ISecretResult? FetchSecret()
        {
            plugin.ThrowIfUnloaded();

            if (_rawSecretValue == null)
            {
                return null;
            }

            //Secret is a vault path, or return the raw value
            if (_rawSecretValue.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                ValueTask<ISecretResult?> res = GetSecretFromVault(_rawSecretValue, false);
                Debug.Assert(res.IsCompleted);
                return res.GetAwaiter().GetResult();
            }

            //See if the secret is an environment variable path
            if (_rawSecretValue.StartsWith(ENV_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                //try to get the environment variable
                string envVar = _rawSecretValue[ENV_URL_SCHEME.Length..];
                string? envVal = Environment.GetEnvironmentVariable(envVar);

                /*
                 * I can't safely take ownership of the memory of the 
                 * string returned by the environment variable. So I can only 
                 * copy it and let the refence fall out of scope.
                 * 
                 * In the future I may consider using PrivateStringManager to
                 * wrap it if I can determine it's safe to destroy the string.
                 */

                return envVal == null ? null : SecretResult.ToSecret(envVal);
            }

            //See if the secret is a file path
            if (_rawSecretValue.StartsWith(FILE_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                string filePath = _rawSecretValue[FILE_URL_SCHEME.Length..];
                byte[] fileData = File.ReadAllBytes(filePath);

                return GetResultFromFileData(fileData);
            }

            /*
             * Copy the raw string value to a new secret value. 
             * Read the security note on the _rawSecretValue field
             * above.
             */
            return SecretResult.ToSecret(_rawSecretValue);
        }

        ///<inheritdoc/>
        public Task<ISecretResult?> FetchSecretAsync(CancellationToken cancellation)
        {
            plugin.ThrowIfUnloaded();

            if (_rawSecretValue == null)
            {
                return Task.FromResult<ISecretResult?>(null);
            }

            //Secret is a vault path, or return the raw value
            if (_rawSecretValue.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                //Exec vault async
                ValueTask<ISecretResult?> res = GetSecretFromVault(_rawSecretValue, true);
                return res.AsTask();
            }

            //See if the secret is an environment variable path
            if (_rawSecretValue.StartsWith(ENV_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                //try to get the environment variable
                string envVar = _rawSecretValue[ENV_URL_SCHEME.Length..];
                string? envVal = Environment.GetEnvironmentVariable(envVar);


                /*
                 * I can't safely take ownership of the memory of the 
                 * string returned by the environment variable. So I can only 
                 * copy it and let the refence fall out of scope.
                 * 
                 * In the future I may consider using PrivateStringManager to
                 * wrap it if I can determine it's safe to destroy the string.
                 */

                return Task.FromResult<ISecretResult?>(envVal == null ? null : SecretResult.ToSecret(envVal));
            }

            //See if the secret is a file path
            if (_rawSecretValue.StartsWith(FILE_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                string filePath = _rawSecretValue[FILE_URL_SCHEME.Length..];
                return GetResultFromFileAsync(filePath, cancellation);
            }

            /*
            * Copy the raw string value to a new secret value. 
            * Read the security note on the _rawSecretValue field
            * above.
            */
            return Task.FromResult<ISecretResult?>(SecretResult.ToSecret(_rawSecretValue.AsSpan()));


            static async Task<ISecretResult?> GetResultFromFileAsync(string filePath, CancellationToken ct)
            {
                byte[] fileData = await File.ReadAllBytesAsync(filePath, ct);
                return GetResultFromFileData(fileData);
            }
        }

        /// <summary>
        /// Gets a secret at the given vault url (in the form of "vault://[mount-name]/[secret-path]?secret=[secret_name]")
        /// </summary>
        /// <param name="vaultPath">The raw vault url to lookup</param>
        /// <param name="async"></param>
        /// <returns>The string of the object at the specified vault path</returns>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        private ValueTask<ISecretResult?> GetSecretFromVault(ReadOnlySpan<char> vaultPath, bool async)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            //print the path for debug
            if (plugin.IsDebug())
            {
                plugin.Log.Debug("Retrieving secret {s} from vault", vaultPath.ToString());
            }

            //Slice off path
            ReadOnlySpan<char> paq = vaultPath.SliceAfterParam(VAULT_URL_SCHEME);
            ReadOnlySpan<char> path = paq.SliceBeforeParam('?');
            ReadOnlySpan<char> query = paq.SliceAfterParam('?');

            if (paq.IsEmpty)
            {
                throw new UriFormatException("Vault secret location not valid/empty ");
            }

            //Get the secret 
            string secretTableKey = query.SliceAfterParam("secret=").SliceBeforeParam('&').ToString();
            string vaultType = query.SliceBeforeParam("vault_type=").SliceBeforeParam('&').ToString();

            //get mount and path
            int lastSep = path.IndexOf('/');
            string mount = path[..lastSep].ToString();
            string secret = path[(lastSep + 1)..].ToString();

            //Try load client
            _ = vault.Instance ?? throw new KeyNotFoundException("Vault client not found");

            if (async)
            {
                Task<ISecretResult?> asTask = Task.Run(() => vault.Instance.ReadSecretAsync(secret, mount, secretTableKey));
                return new ValueTask<ISecretResult?>(asTask);
            }
            else
            {
                ISecretResult? result = vault.Instance.ReadSecret(secret, mount, secretTableKey);
                return new ValueTask<ISecretResult?>(result);
            }
        }

        private static string? TryGetSecretFromConfig(PluginBase plugin, string secretName)
        {
            bool local = plugin.PluginConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement localEl);
            bool host = plugin.HostConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement hostEl);

            //total config
            Dictionary<string, JsonElement> conf = new(StringComparer.OrdinalIgnoreCase);

            if (local && host)
            {
                //Load both config objects to dict
                Dictionary<string, JsonElement> localConf = localEl
                    .EnumerateObject()
                    .ToDictionary(static x => x.Name, static x => x.Value);

                Dictionary<string, JsonElement> hostConf = hostEl
                    .EnumerateObject()
                    .ToDictionary(static x => x.Name, static x => x.Value);

                //Enter all host secret objects, then follow up with plugin secert elements
                hostConf.ForEach(kv => conf[kv.Key] = kv.Value);
                localConf.ForEach(kv => conf[kv.Key] = kv.Value);

            }
            else if (local)
            {
                //Store only local config
                conf = localEl
                    .EnumerateObject()
                    .ToDictionary(
                        static x => x.Name,
                        static x => x.Value,
                        StringComparer.OrdinalIgnoreCase
                    );
            }
            else if (host)
            {
                //store only host config
                conf = hostEl
                    .EnumerateObject()
                    .ToDictionary(
                        static x => x.Name,
                        static x => x.Value,
                        StringComparer.OrdinalIgnoreCase
                    );
            }

            //Get the value or default json element
            return conf.TryGetValue(secretName, out JsonElement el) ? el.GetString() : null;
        }

        /// <summary>
        /// Attempts to quickly check if a secret has been defined in the system configuration.
        /// It does not check if the value exists from whatever store it is defined in.
        /// </summary>
        /// <param name="plugin">The plugin to check</param>
        /// <param name="secretName">The name of the secret to search for</param>
        /// <returns>True of the host or plugin configuration contains a named element with the secret</returns>
        internal static bool IsSecretDefined(PluginBase plugin, string secretName)
        {
            /*
             * A secret is defined if an element is found in either the plugin or host config.
             * Plugin is always checked first.
             */
            return (plugin.PluginConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement secConfig) && secConfig.TryGetProperty(secretName, out _))
                || (plugin.HostConfig.TryGetProperty(SECRETS_CONFIG_KEY, out secConfig) && secConfig.TryGetProperty(secretName, out _));
        }

        private static SecretResult GetResultFromFileData(byte[] secretFileData)
        {
            //recover the character data from the file data
            int chars = Encoding.UTF8.GetCharCount(secretFileData);
            char[] secretFileChars = new char[chars];
            Encoding.UTF8.GetChars(secretFileData, secretFileChars);

            //Clear file data buffer
            MemoryUtil.InitializeBlock(secretFileData);

            //Keep the char array as a secret
            return SecretResult.ToSecret(secretFileChars);
        }
    }
}
