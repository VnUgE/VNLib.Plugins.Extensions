using System.Text;
using System.Text.Json;

using RestSharp;

using Emails.Transactional.Client;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Net.Rest.Client;
using VNLib.Net.Rest.Client.OAuth2;
using VNLib.Plugins.Extensions.Loading;


namespace VNLib.Plugins.Extentions.TransactionalEmail
{
    /// <summary>
    /// Contains extension methods for implementing templated 
    /// transactional emails 
    /// </summary>
    public static class TransactionalEmailExtensions
    {
        public const string EMAIL_CONFIG_KEY = "emails";
        public const string REQUIRED_EMAIL_TEMPALTE_CONFIG_KEY = "required_email_templates";

        public const uint DEFAULT_MAX_CLIENTS = 5;
        public const uint DEFAULT_CLIENT_TIMEOUT_MS = 10000;

        /// <summary>
        /// Gets (or loads) the ambient <see cref="TransactionalEmailConfig"/> configuration object
        /// to send transactional emails against
        /// </summary>
        /// <param name="pbase"></param>
        /// <returns>The <see cref="TransactionalEmailConfig"/> from the current plugins config</returns>
        public static TransactionalEmailConfig GetEmailConfig(this PluginBase pbase) => LoadingExtensions.GetOrCreateSingleton(pbase, LoadConfig);

        /// <summary>
        /// Sends an <see cref="EmailTransactionRequest"/> on the current configuration resource pool
        /// </summary>
        /// <param name="config"></param>
        /// <param name="request">The <see cref="EmailTransactionRequest"/> request to send to the server</param>
        /// <returns>A task the resolves the <see cref="TransactionResult"/> of the request</returns>
        public static async Task<TransactionResult> SendEmailAsync(this TransactionalEmailConfig config, EmailTransactionRequest request)
        {
            //Get a new client contract from the configuration's pool assuming its a EmailSystemConfig class
            using ClientContract client = ((EmailSystemConfig)config).RestClientPool.Lease();
            //Send the email and await the result before releasing the client
            return await client.Resource.SendEmailAsync(request);
        }

        private static TransactionalEmailConfig LoadConfig(PluginBase pbase) 
        {
            //Get the required email config
            IReadOnlyDictionary<string, JsonElement> conf = pbase.GetConfig(EMAIL_CONFIG_KEY);

            string emailFromName = conf["from_name"].GetString() ?? throw new KeyNotFoundException("Missing required configuration key 'from_name'");
            string emailFromAddress = conf["from_address"].GetString() ?? throw new KeyNotFoundException("Missing required configuration key 'from_address'");
            Uri baseServerPath = new(conf["base_url"].GetString()!, UriKind.RelativeOrAbsolute);

            //Get the token server url or use the base path if no set
            Uri tokenServerBase = conf.TryGetValue("token_server_url", out JsonElement tksEl) && tksEl.GetString() != null ?
                new(tksEl.GetString()!, UriKind.RelativeOrAbsolute)
                : baseServerPath;

            //Get the transaction endpoint path, should be a realative path
            Uri transactionEndpoint = new(conf["transaction_path"].GetString()!, UriKind.Relative);

            //Load credentials
            string authEndpoint = conf["token_path"].GetString() ?? throw new KeyNotFoundException("Missing required configuration key 'token_path'");

            //Optional user-agent
            string? userAgent = conf.GetPropString("user_agent");

            //Get optional timeout ms
            int timeoutMs = (int)(conf.TryGetValue("request_timeout_ms", out JsonElement timeoutEl) ? timeoutEl.GetUInt32() : DEFAULT_CLIENT_TIMEOUT_MS);

            //Get maximum client limit
            int maxClients = (int)(conf.TryGetValue("max_clients", out JsonElement mxcEl) ? mxcEl.GetUInt32() : DEFAULT_MAX_CLIENTS);

            //Load all templates from the plugin config
            Dictionary<string, string> templates = pbase.PluginConfig.GetProperty(REQUIRED_EMAIL_TEMPALTE_CONFIG_KEY)
                .EnumerateObject()
                .ToDictionary(static jp => jp.Name, static jp => jp.Value.GetString()!);

            pbase.Log.Verbose("Required email templates {t}", templates);

            //Load oauth secrets from vault
            Task<SecretResult?> oauth2ClientID = pbase.TryGetSecretAsync("email_client_id");
            Task<SecretResult?> oauth2Password = pbase.TryGetSecretAsync("email_client_secret");

            //Lazy cred loaded, tasks should be loaded before this method will ever get called
            Credential lazyCredentialGet()
            {
                //Load the results 
                SecretResult cliendId = oauth2ClientID.GetAwaiter().GetResult() ?? throw new KeyNotFoundException("Missing required oauth2 client id");
                SecretResult password = oauth2Password.GetAwaiter().GetResult() ?? throw new KeyNotFoundException("Missing required oauth2 client secret");

                //Creat credential
                return Credential.Create(cliendId.Result, password.Result);
            }

            //Init client creation options
            RestClientOptions poolOptions = new(baseServerPath)
            {
                AllowMultipleDefaultParametersWithSameName = true,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PreAuthenticate = true,
                Encoding = Encoding.UTF8,
                MaxTimeout = timeoutMs,
                UserAgent = userAgent,
                //Server should not redirect
                FollowRedirects = false,
            };

            //Options for auth token endpoint
            RestClientOptions oAuth2ClientOptions = new(tokenServerBase)
            {
                AllowMultipleDefaultParametersWithSameName = true,
                //Server supports compression
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PreAuthenticate = false,
                Encoding = Encoding.UTF8,
                MaxTimeout = timeoutMs,
                UserAgent = userAgent,
                //Server should not redirect
                FollowRedirects = false
            };

            //Init Oauth authenticator
            OAuth2Authenticator authenticator = new(oAuth2ClientOptions, lazyCredentialGet, authEndpoint);

            //Create client pool
            RestClientPool pool = new(maxClients, poolOptions, authenticator: authenticator);

            void Cleanup()
            {
                authenticator.Dispose();
                pool.Dispose();
                oauth2ClientID.Dispose();
                oauth2Password.Dispose();
            }

            //register password cleanup
            _ = pbase.RegisterForUnload(Cleanup);

            //Create config
            EmailSystemConfig config = new ()
            {
                EmailFromName = emailFromName,
                EmailFromAddress = emailFromAddress,
                RestClientPool = pool,
            };

            //Store templates and set service url
            config.WithTemplates(templates)
                .WithUrl(transactionEndpoint);

            return config;
        }
    }
}