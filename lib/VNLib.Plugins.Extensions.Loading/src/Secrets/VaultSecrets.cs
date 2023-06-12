/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: VaultSecrets.cs 
*
* VaultSecrets.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.SecretsEngines.PKI;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Hashing.IdentityUtility;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// Adds loading extensions for secure/centralized configuration secrets
    /// </summary>
    public static class PluginSecretLoading
    {
        public const string VAULT_OBJECT_NAME = "hashicorp_vault";
        public const string SECRETS_CONFIG_KEY = "secrets";
        public const string VAULT_TOKEN_KEY = "token";
        public const string VAULT_ROLE_KEY = "role";
        public const string VAULT_SECRET_KEY = "secret";
        public const string VAULT_TOKNE_ENV_NAME = "VNLIB_PLUGINS_VAULT_TOKEN";

        public const string VAULT_URL_KEY = "url";

        public const string VAULT_URL_SCHEME = "vault://";


        /// <summary>
        /// <para>
        /// Gets a secret from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element.
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="secretName">The name of the secret propery to get</param>
        /// <returns>The element from the configuration file with the given name, or null if the configuration or property does not exist</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static async Task<ISecretResult> GetSecretAsync(this PluginBase plugin, string secretName)
        {
            ISecretResult? res = await TryGetSecretAsync(plugin, secretName).ConfigureAwait(false);
            return res ?? throw new KeyNotFoundException($"Missing required secret {secretName}");
        }

        /// <summary>
        /// <para>
        /// Gets a secret from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element.
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="secretName">The name of the secret propery to get</param>
        /// <returns>The element from the configuration file with the given name, or null if the configuration or property does not exist</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Task<ISecretResult?> TryGetSecretAsync(this PluginBase plugin, string secretName)
        {
            plugin.ThrowIfUnloaded();

            //Get the secret from the config file raw
            string? rawSecret = TryGetSecretInternal(plugin, secretName);

            if (rawSecret == null)
            {
                return Task.FromResult<ISecretResult?>(null);
            }

            //Secret is a vault path, or return the raw value
            if (!rawSecret.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<ISecretResult?>(new SecretResult(rawSecret.AsSpan()));
            }

            return GetSecretFromVaultAsync(plugin, rawSecret);
        }

        /// <summary>
        /// Gets a secret at the given vault url (in the form of "vault://[mount-name]/[secret-path]?secret=[secret_name]")
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="vaultPath">The raw vault url to lookup</param>
        /// <returns>The string of the object at the specified vault path</returns>
        /// <exception cref="UriFormatException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Task<ISecretResult?> GetSecretFromVaultAsync(this PluginBase plugin, ReadOnlySpan<char> vaultPath)
        {
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

            async Task<ISecretResult?> execute()
            {
                //Try load client
                IVaultClient? client = plugin.GetVault();
                
                _ = client ?? throw new KeyNotFoundException("Vault client not found");
                //run read async
                Secret<SecretData> result = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path:secret, mountPoint:mount);
                //Read the secret
                return SecretResult.ToSecret(result.Data.Data[secretTableKey].ToString());
            }
            
            return Task.Run(execute);
        }

        /// <summary>
        /// <para>
        /// Gets a Certicate from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element.
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="secretName">The name of the secret propery to get</param>
        /// <returns>The element from the configuration file with the given name, or null if the configuration or property does not exist</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Task<X509Certificate?> TryGetCertificateAsync(this PluginBase plugin, string secretName)
        {
            plugin.ThrowIfUnloaded();
            //Get the secret from the config file raw
            string? rawSecret = TryGetSecretInternal(plugin, secretName);
            if (rawSecret == null)
            {
                return Task.FromResult<X509Certificate?>(null);
            }

            //Secret is a vault path, or return the raw value
            if (!rawSecret.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<X509Certificate?>(new (rawSecret));
            }
            return GetCertFromVaultAsync(plugin, rawSecret);
        }

        public static Task<X509Certificate?> GetCertFromVaultAsync(this PluginBase plugin, ReadOnlySpan<char> vaultPath, CertificateCredentialsRequestOptions? options = null)
        {
            //print the path for debug
            if (plugin.IsDebug())
            {
                plugin.Log.Debug("Retrieving certificate {s} from vault", vaultPath.ToString());
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
            string role = query.SliceAfterParam("role=").SliceBeforeParam('&').ToString();
            string vaultType = query.SliceBeforeParam("vault_type=").SliceBeforeParam('&').ToString();
            string commonName = query.SliceBeforeParam("cn=").SliceBeforeParam('&').ToString();

            //get mount and path
            int lastSep = path.IndexOf('/');
            string mount = path[..lastSep].ToString();
            string secret = path[(lastSep + 1)..].ToString();

            async Task<X509Certificate?> execute()
            {
                //Try load client
                IVaultClient? client = plugin.GetVault();

                _ = client ?? throw new KeyNotFoundException("Vault client not found");

                options ??= new()
                {
                    CertificateFormat = CertificateFormat.pem,
                    PrivateKeyFormat = PrivateKeyFormat.pkcs8,
                    CommonName = commonName,
                };

                //run read async
                Secret<CertificateCredentials> result = await client.V1.Secrets.PKI.GetCredentialsAsync(pkiRoleName:secret, certificateCredentialRequestOptions:options, pkiBackendMountPoint:mount);
                //Read the secret
                byte[] pemCertData = Encoding.UTF8.GetBytes(result.Data.CertificateContent);

                return new (pemCertData);
            }

            return Task.Run(execute);
        }

        /// <summary>
        /// Gets the ambient vault client for the current plugin
        /// if the configuration is loaded, null otherwise
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="IVaultClient"/> if loaded, null otherwise</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IVaultClient? GetVault(this PluginBase plugin) => LoadingExtensions.GetOrCreateSingleton(plugin, TryGetVaultLoader);
        
        private static string? TryGetSecretInternal(PluginBase plugin, string secretName)
        {
            bool local = plugin.PluginConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement localEl);
            bool host = plugin.HostConfig.TryGetProperty(SECRETS_CONFIG_KEY, out JsonElement hostEl);

            //total config
            IReadOnlyDictionary<string, JsonElement>? conf;

            if (local && host)
            {
                //Load both config objects to dict
                Dictionary<string, JsonElement> localConf = localEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
                Dictionary<string, JsonElement> hostConf = hostEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);

                //merge the two configs
                foreach(KeyValuePair<string, JsonElement> lc in localConf)
                {
                    //Overwrite any host config keys, plugin conf takes priority
                    hostConf[lc.Key] = lc.Value;
                }
                //set the merged config
                conf = hostConf;
            }
            else if(local)
            {
                //Store only local config
                conf = localEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
            }
            else if(host)
            {
                //store only host config
                conf = hostEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
            }
            else
            {
                conf = null;
            }
          
            //Get the value or default json element
            return conf != null && conf.TryGetValue(secretName, out JsonElement el) ? el.GetString() : null;
        }

        private static IVaultClient? TryGetVaultLoader(PluginBase pbase)
        {
            //Get vault config
            IConfigScope? conf = pbase.TryGetConfig(VAULT_OBJECT_NAME);

            if (conf == null)
            {
                return null;
            }

            //try get servre address creds from config
            string? serverAddress = conf[VAULT_URL_KEY].GetString() ?? throw new KeyNotFoundException($"Failed to load the key {VAULT_URL_KEY} from object {VAULT_OBJECT_NAME}");

            IAuthMethodInfo authMethod;

            //Get authentication method from config
            if (conf.TryGetValue(VAULT_TOKEN_KEY, out JsonElement tokenEl))
            {
                //Init token
                authMethod = new TokenAuthMethodInfo(tokenEl.GetString());
            }
            else if (conf.TryGetValue(VAULT_ROLE_KEY, out JsonElement roleEl) && conf.TryGetValue(VAULT_SECRET_KEY, out JsonElement secretEl))
            {
                authMethod = new AppRoleAuthMethodInfo(roleEl.GetString(), secretEl.GetString());
            }
            //Try to get the token as an environment variable
            else if(Environment.GetEnvironmentVariable(VAULT_TOKNE_ENV_NAME) != null)
            {
                string tokenValue = Environment.GetEnvironmentVariable(VAULT_TOKNE_ENV_NAME)!;
                authMethod = new TokenAuthMethodInfo(tokenValue);
            }
            else
            {
                throw new KeyNotFoundException($"Failed to load the vault authentication method from {VAULT_OBJECT_NAME}");
            }

            //Settings
            VaultClientSettings settings = new(serverAddress, authMethod);

            //create vault client
            return new VaultClient(settings);
        }

        /// <summary>
        /// Gets the Secret value as a byte buffer
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The base64 decoded secret as a byte[]</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static byte[] GetFromBase64(this ISecretResult secret)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            
            //Temp buffer
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(secret.Result.Length);
            
            //Get base64
            if(!Convert.TryFromBase64Chars(secret.Result, buffer, out int count))
            {
                throw new InternalBufferTooSmallException("internal buffer too small");
            }

            //Copy to array
            byte[] value = buffer.Span[..count].ToArray();

            //Clear block before returning
            MemoryUtil.InitializeBlock<byte>(buffer);

            return value;
        }

        /// <summary>
        /// Recovers a certificate from a PEM encoded secret
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="X509Certificate2"/> parsed from the PEM encoded data</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static X509Certificate2 GetCertificate(this ISecretResult secret)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            return X509Certificate2.CreateFromPem(secret.Result);
        }

        /// <summary>
        /// Gets the secret value as a secret result
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The document parsed from the secret value</returns>
        public static JsonDocument GetJsonDocument(this ISecretResult secret)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));

            //Alloc buffer, utf8 so 1 byte per char
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);

            //Get utf8 bytes
            int count = Encoding.UTF8.GetBytes(secret.Result, buffer.Span);
            
            //Reader and parse
            Utf8JsonReader reader = new(buffer.Span[..count]);
            
            return JsonDocument.ParseValue(ref reader);
        }
        
        /// <summary>
        /// Gets a SPKI encoded public key from a secret
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="PublicKey"/> parsed from the SPKI public key</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PublicKey GetPublicKey(this ISecretResult secret)
        {          
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            
            //Alloc buffer, base64 is larger than binary value so char len is large enough
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);
            
            //Get base64 bytes
            ERRNO count = VnEncoding.TryFromBase64Chars(secret.Result, buffer.Span);
            
            //Parse the SPKI from base64
            return PublicKey.CreateFromSubjectPublicKeyInfo(buffer.Span[..(int)count], out _);
        }

        /// <summary>
        /// Gets the value of the <see cref="SecretResult"/> as a <see cref="PrivateKey"/>
        /// container
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="PrivateKey"/> from the secret value</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static PrivateKey GetPrivateKey(this ISecretResult secret)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            return new PrivateKey(secret);
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlyJsonWebKey"/> from a secret value
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the result</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static ReadOnlyJsonWebKey GetJsonWebKey(this ISecretResult secret)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            
            //Alloc buffer, utf8 so 1 byte per char
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);
            
            //Get utf8 bytes
            int count = Encoding.UTF8.GetBytes(secret.Result, buffer.Span);

            return new ReadOnlyJsonWebKey(buffer.Span[..count]);
        }

#nullable disable

        /// <summary>
        /// Converts the secret recovery task to return the base64 decoded secret as a byte[]
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>A task whos result the base64 decoded secret as a byte[]</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static async Task<byte[]> ToBase64Bytes(this Task<ISecretResult> secret)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));

            using ISecretResult sec = await secret.ConfigureAwait(false);

            return sec?.GetFromBase64();
        }

        /// <summary>
        /// Gets a task that resolves a <see cref="ReadOnlyJsonWebKey"/>
        /// from a <see cref="SecretResult"/> task
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the secret, or null if the secret was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret) 
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            
            using ISecretResult sec = await secret.ConfigureAwait(false);

            return sec?.GetJsonWebKey();
        }

        /// <summary>
        /// Gets a task that resolves a <see cref="ReadOnlyJsonWebKey"/>
        /// from a <see cref="SecretResult"/> task
        /// </summary>
        /// <param name="secret"></param>
        /// <param name="required">
        /// A value that inidcates that a value is required from the result, 
        /// or a <see cref="KeyNotFoundException"/> is raised
        /// </param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the secret, or throws <see cref="KeyNotFoundException"/> if the key was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public static async Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret, bool required)
        {
            _ = secret ?? throw new ArgumentNullException(nameof(secret));
            
            using ISecretResult sec = await secret.ConfigureAwait(false);
            
            //If required is true and result is null, raise an exception
            return required && sec == null ? throw new KeyNotFoundException("A required secret was missing") : (sec?.GetJsonWebKey()!);
        }

        /// <summary>
        /// Converts a <see cref="SecretResult"/> async operation to a lazy result that can be awaited, that transforms the result
        /// to your desired type. If the result is null, the default value of <typeparamref name="TResult"/> is returned
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="result"></param>
        /// <param name="transformer">Your function to transform the secret to its output form</param>
        /// <returns>A <see cref="IAsyncLazy{T}"/> </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IAsyncLazy<TResult> ToLazy<TResult>(this Task<ISecretResult> result, Func<ISecretResult, TResult> transformer)
        {
            _ = result ?? throw new ArgumentNullException(nameof(result));
            _ = transformer ?? throw new ArgumentNullException(nameof(transformer));

            //standard secret transformer
            static async Task<TResult> Run(Task<ISecretResult> tr, Func<ISecretResult, TResult> transformer)
            {
                using ISecretResult res = await tr.ConfigureAwait(false);
                return res == null ? default : transformer(res); 
            }

            return Run(result, transformer).AsLazy();
        }

        /// <summary>
        /// Converts a <see cref="SecretResult"/> async operation to a lazy result that can be awaited, that transforms the result
        /// to your desired type. If the result is null, the default value of <typeparamref name="TResult"/> is returned
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="result"></param>
        /// <param name="transformer">Your function to transform the secret to its output form</param>
        /// <returns>A <see cref="IAsyncLazy{T}"/> </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IAsyncLazy<TResult> ToLazy<TResult>(this Task<ISecretResult> result, Func<ISecretResult, Task<TResult>> transformer)
        {
            _ = result ?? throw new ArgumentNullException(nameof(result));
            _ = transformer ?? throw new ArgumentNullException(nameof(transformer));

            //Transform with task transformer
            static async Task<TResult> Run(Task<ISecretResult?> tr, Func<ISecretResult, Task<TResult>> transformer)
            {
                using ISecretResult res = await tr.ConfigureAwait(false);
                return res == null ? default : await transformer(res).ConfigureAwait(false);
            }

            return Run(result, transformer).AsLazy();
        }
    }
}
