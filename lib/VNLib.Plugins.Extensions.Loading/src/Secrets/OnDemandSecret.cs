/*
* Copyright (c) 2024 Vaughn Nugent
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

using static VNLib.Plugins.Extensions.Loading.PluginSecretConstants;

namespace VNLib.Plugins.Extensions.Loading
{
    internal sealed class OnDemandSecret(PluginBase plugin, string secretName, IKvVaultClient? vault) : IOnDemandSecret
    {
        public string SecretName { get; } = secretName ?? throw new ArgumentNullException(nameof(secretName));

        ///<inheritdoc/>
        public ISecretResult? FetchSecret()
        {
            plugin.ThrowIfUnloaded();

            //Get the secret from the config file raw
            string? rawSecret = TryGetSecretFromConfig(secretName);

            if (rawSecret == null)
            {
                return null;
            }

            //Secret is a vault path, or return the raw value
            if (rawSecret.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                ValueTask<ISecretResult?> res = GetSecretFromVault(rawSecret, false);
                Debug.Assert(res.IsCompleted);
                return res.GetAwaiter().GetResult();
            }

            //See if the secret is an environment variable path
            if (rawSecret.StartsWith(ENV_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                //try to get the environment variable
                string envVar = rawSecret[ENV_URL_SCHEME.Length..];
                string? envVal = Environment.GetEnvironmentVariable(envVar);

                return envVal == null ? null : SecretResult.ToSecret(envVal);
            }

            //See if the secret is a file path
            if (rawSecret.StartsWith(FILE_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                string filePath = rawSecret[FILE_URL_SCHEME.Length..];
                byte[] fileData = File.ReadAllBytes(filePath);

                return GetResultFromFileData(fileData);
            }

            //Finally, return the raw value
            return SecretResult.ToSecret(rawSecret);
        }

        ///<inheritdoc/>
        public Task<ISecretResult?> FetchSecretAsync(CancellationToken cancellation)
        {
            plugin.ThrowIfUnloaded();

            //Get the secret from the config file raw
            string? rawSecret = TryGetSecretFromConfig(secretName);

            if (rawSecret == null)
            {
                return Task.FromResult<ISecretResult?>(null);
            }

            //Secret is a vault path, or return the raw value
            if (rawSecret.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                //Exec vault async
                ValueTask<ISecretResult?> res = GetSecretFromVault(rawSecret, true);
                return res.AsTask();
            }

            //See if the secret is an environment variable path
            if (rawSecret.StartsWith(ENV_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                //try to get the environment variable
                string envVar = rawSecret[ENV_URL_SCHEME.Length..];
                string? envVal = Environment.GetEnvironmentVariable(envVar);
                return Task.FromResult<ISecretResult?>(envVal == null ? null : SecretResult.ToSecret(envVal));
            }

            //See if the secret is a file path
            if (rawSecret.StartsWith(FILE_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                string filePath = rawSecret[FILE_URL_SCHEME.Length..];
                return GetResultFromFileAsync(filePath, cancellation);
            }

            //Finally, return the raw value
            return Task.FromResult<ISecretResult?>(SecretResult.ToSecret(rawSecret));


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
            _ = vault ?? throw new KeyNotFoundException("Vault client not found");

            if (async)
            {
                Task<ISecretResult?> asTask = Task.Run(() => vault.ReadSecretAsync(secret, mount, secretTableKey));
                return new ValueTask<ISecretResult?>(asTask);
            }
            else
            {
                ISecretResult? result = vault.ReadSecret(secret, mount, secretTableKey);
                return new ValueTask<ISecretResult?>(result);
            }
        }

        private string? TryGetSecretFromConfig(string secretName)
        {
            bool local = plugin.PluginConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement localEl);
            bool host = plugin.HostConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement hostEl);

            //total config
            Dictionary<string, JsonElement> conf = new(StringComparer.OrdinalIgnoreCase);

            if (local && host)
            {
                //Load both config objects to dict
                Dictionary<string, JsonElement> localConf = localEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
                Dictionary<string, JsonElement> hostConf = hostEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);

                //Enter all host secret objects, then follow up with plugin secert elements
                hostConf.ForEach(kv => conf[kv.Key] = kv.Value);
                localConf.ForEach(kv => conf[kv.Key] = kv.Value);
                
            }
            else if (local)
            {
                //Store only local config
                conf = localEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
            }
            else if (host)
            {
                //store only host config
                conf = hostEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
            }

            //Get the value or default json element
            return conf.TryGetValue(secretName, out JsonElement el) ? el.GetString() : null;
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
