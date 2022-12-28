/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.VNCache
* File: VnCacheClient.cs 
*
* VnCacheClient.cs is part of VNLib.Plugins.Extensions.VNCache which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.VNCache is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.VNCache is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Text.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Hashing.IdentityUtility;
using VNLib.Data.Caching;
using VNLib.Data.Caching.Extensions;
using VNLib.Net.Messaging.FBM.Client;
using VNLib.Plugins.Extensions.Loading;


namespace VNLib.Plugins.Extensions.VNCache
{
    /// <summary>
    /// A wrapper to simplify a shared global cache client
    /// </summary>
    [ConfigurationName("vncache")]
    internal sealed class VnCacheClient : VnDisposeable, IGlobalCacheProvider
    {
        FBMClient? _client;

        private TimeSpan RetryInterval;

        private readonly ILogProvider? DebugLog;
        private readonly IUnmangedHeap? ClientHeap;

        /// <summary>
        /// Initializes an emtpy client wrapper that still requires 
        /// configuration loading
        /// </summary>
        /// <param name="debugLog">An optional debugging log</param>
        /// <param name="heap">An optional <see cref="IUnmangedHeap"/> for <see cref="FBMClient"/> buffers</param>
        public VnCacheClient(ILogProvider? debugLog, IUnmangedHeap? heap = null)
        {
            DebugLog = debugLog;
            //Default to 10 seconds
            RetryInterval = TimeSpan.FromSeconds(10);

            ClientHeap = heap;
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            _client?.Dispose();
            _client = null;
        }


        /// <summary>
        /// Loads required configuration variables from the config store and 
        /// intializes the interal client
        /// </summary>
        /// <param name="pbase"></param>
        /// <param name="config">A dictionary of configuration varables</param>
        /// <exception cref="KeyNotFoundException"></exception>
        public async Task LoadConfigAsync(PluginBase pbase, IReadOnlyDictionary<string, JsonElement> config)
        {
            int maxMessageSize = config["max_message_size"].GetInt32();
            string? brokerAddress = config["broker_address"].GetString() ?? throw new KeyNotFoundException("Missing required configuration variable broker_address");

            //Get keys async
            Task<ReadOnlyJsonWebKey?> clientPrivTask = pbase.TryGetSecretAsync("client_private_key").ToJsonWebKey();
            Task<ReadOnlyJsonWebKey?> brokerPubTask = pbase.TryGetSecretAsync("broker_public_key").ToJsonWebKey();
            Task<ReadOnlyJsonWebKey?> cachePubTask = pbase.TryGetSecretAsync("cache_public_key").ToJsonWebKey();

            //Wait for all tasks to complete
            _ = await Task.WhenAll(clientPrivTask, brokerPubTask, cachePubTask);

            ReadOnlyJsonWebKey clientPriv = await clientPrivTask ?? throw new KeyNotFoundException("Missing required secret client_private_key");
            ReadOnlyJsonWebKey brokerPub = await brokerPubTask ?? throw new KeyNotFoundException("Missing required secret broker_public_key");
            ReadOnlyJsonWebKey cachePub = await cachePubTask ?? throw new KeyNotFoundException("Missing required secret cache_public_key");

            RetryInterval = config["retry_interval_sec"].GetTimeSpan(TimeParseType.Seconds);

            Uri brokerUri = new(brokerAddress);

            //Init the client with default settings
            FBMClientConfig conf = FBMDataCacheExtensions.GetDefaultConfig(ClientHeap ?? Memory.Shared, maxMessageSize, DebugLog);

            _client = new(conf);

            //Add the configuration to the client
            _client.GetCacheConfiguration()
                .WithBroker(brokerUri)
                .WithVerificationKey(cachePub)
                .WithSigningCertificate(clientPriv)
                .WithBrokerVerificationKey(brokerPub)
                .WithTls(brokerUri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Discovers nodes in the configured cluster and connects to a random node
        /// </summary>
        /// <param name="Log">A <see cref="ILogProvider"/> to write log events to</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A task that completes when the operation has been cancelled or an unrecoverable error occured</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public async Task RunAsync(ILogProvider Log, CancellationToken cancellationToken)
        {
            _ = _client ?? throw new InvalidOperationException("Client configuration not loaded, cannot connect to cache servers");

            while (true)
            {
                //Load the server list
                ActiveServer[]? servers;
                while (true)
                {
                    try
                    {
                        Log.Debug("Discovering cluster nodes in broker");
                        //Get server list
                        servers = await _client.DiscoverCacheNodesAsync(cancellationToken);
                        break;
                    }
                    catch (HttpRequestException re) when (re.InnerException is SocketException)
                    {
                        Log.Warn("Broker server is unreachable");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Failed to get server list from broker, reason {r}", ex.Message);
                    }

                    //Gen random ms delay
                    int randomMsDelay = RandomNumberGenerator.GetInt32(1000, 2000);
                    await Task.Delay(randomMsDelay, cancellationToken);
                }

                if (servers?.Length == 0)
                {
                    Log.Warn("No cluster nodes found, retrying");
                    await Task.Delay(RetryInterval, cancellationToken);
                    continue;
                }

                try
                {
                    Log.Debug("Connecting to random cache server");

                    //Connect to a random server
                    ActiveServer selected = await _client.ConnectToRandomCacheAsync(cancellationToken);
                    Log.Debug("Connected to cache server {s}", selected.ServerId);

                    //Set connection status flag
                    IsConnected = true;

                    //Wait for disconnect
                    await _client.WaitForExitAsync(cancellationToken);

                    Log.Debug("Cache server disconnected");
                }
                catch (WebSocketException wse)
                {
                    Log.Warn("Failed to connect to cache server {reason}", wse.Message);
                    continue;
                }
                catch (HttpRequestException he) when (he.InnerException is SocketException)
                {
                    Log.Debug("Failed to connect to random cache server server");
                    //Continue next loop
                    continue;
                }
                finally
                {
                    IsConnected = false;
                }
            }
        }


        ///<inheritdoc/>
        public bool IsConnected { get; private set; }

        ///<inheritdoc/>
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellation)
        {
            return !IsConnected
                ? throw new InvalidOperationException("The underlying client is not connected to a cache node")
                : _client!.GetObjectAsync<T>(key, cancellation);
        }

        ///<inheritdoc/>
        Task IGlobalCacheProvider.AddOrUpdateAsync<T>(string key, string? newKey, T value, CancellationToken cancellation)
        {
            return !IsConnected
               ? throw new InvalidOperationException("The underlying client is not connected to a cache node")
               : _client!.AddOrUpdateObjectAsync(key, newKey, value, cancellation);
        }

        ///<inheritdoc/>
        Task IGlobalCacheProvider.DeleteAsync(string key, CancellationToken cancellation)
        {
            return !IsConnected
              ? throw new InvalidOperationException("The underlying client is not connected to a cache node")
              : _client!.DeleteObjectAsync(key, cancellation);
        }
    }
}