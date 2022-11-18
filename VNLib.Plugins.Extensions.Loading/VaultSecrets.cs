using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.SecretsEngines.PKI;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

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

        public const string VAULT_URL_KEY = "url";

        public const string VAULT_URL_SCHEME = "vault://";
       

        private static readonly ConditionalWeakTable<PluginBase, Lazy<IVaultClient?>> _vaults = new();

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
        public static Task<string?> TryGetSecretAsync(this PluginBase plugin, string secretName)
        {
            //Get the secret from the config file raw
            string? rawSecret = TryGetSecretInternal(plugin, secretName);
            if (rawSecret == null)
            {
                return Task.FromResult<string?>(null);
            }

            //Secret is a vault path, or return the raw value
            if (!rawSecret.StartsWith(VAULT_URL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<string?>(rawSecret);
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
        public static Task<string?> GetSecretFromVaultAsync(this PluginBase plugin, ReadOnlySpan<char> vaultPath)
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

            async Task<string?> execute()
            {
                //Try load client
                IVaultClient? client = _vaults.GetValue(plugin, TryGetVaultLoader).Value;
                
                _ = client ?? throw new KeyNotFoundException("Vault client not found");
                //run read async
                Secret<SecretData> result = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path:secret, mountPoint:mount);
                //Read the secret
                return result.Data.Data[secretTableKey].ToString();
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
                IVaultClient? client = _vaults.GetValue(plugin, TryGetVaultLoader).Value;

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
        public static IVaultClient? GetVault(this PluginBase plugin) => _vaults.GetValue(plugin, TryGetVaultLoader).Value;
        
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

        private static Lazy<IVaultClient?> TryGetVaultLoader(PluginBase pbase)
        {
            //Local func to load the vault client
            IVaultClient? LoadVault()
            {
                //Get vault config
                IReadOnlyDictionary<string, JsonElement>? conf = pbase.TryGetConfig(VAULT_OBJECT_NAME);

                if(conf == null)
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
                else if(conf.TryGetValue(VAULT_ROLE_KEY, out JsonElement roleEl) && conf.TryGetValue(VAULT_SECRET_KEY, out JsonElement secretEl))
                {
                    authMethod = new AppRoleAuthMethodInfo(roleEl.GetString(), secretEl.GetString());
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
            //init lazy
            return new (LoadVault, LazyThreadSafetyMode.PublicationOnly);
        }
    }
}
