/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.VNCache
* File: VNCacheExtensions.cs 
*
* VNCacheExtensions.cs is part of VNLib.Plugins.Extensions.VNCache which is part of the larger 
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

using VNLib.Utils.Logging;
using VNLib.Data.Caching;
using VNLib.Data.Caching.Extensions;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Extensions.VNCache
{
    /// <summary>
    /// Contains extension methods for aquiring a Plugin managed 
    /// global cache provider.
    /// </summary>
    public static class VNCacheExtensions
    {
        /// <summary>
        /// Loads the shared cache provider for the current plugin
        /// </summary>
        /// <param name="pbase"></param>
        /// <returns>The shared <see cref="IGlobalCacheProvider"/> </returns>
        /// <remarks>
        /// The returned instance, background work, logging, and its lifetime 
        /// are managed by the current plugin. Beware when calling this method
        /// network connections may be spawend and managed in the background by 
        /// this library.
        /// </remarks>
        public static IGlobalCacheProvider GetGlobalCache(this PluginBase pbase) 
            => LoadingExtensions.GetOrCreateSingleton(pbase, LoadCacheClient);

        private static IGlobalCacheProvider LoadCacheClient(PluginBase pbase)
        {
            //Get config for client
            IReadOnlyDictionary<string, JsonElement> config = pbase.GetConfigForType<VnCacheClient>();

            //Init client
            ILogProvider? debugLog = pbase.IsDebug() ? pbase.Log : null;
            VnCacheClient client = new(debugLog);

            //Begin cache connections by scheduling a task on the plugin's scheduler
            _ = pbase.DeferTask(() => RunClientAsync(pbase, config, client), 250);

            return client;
        }
        
        private static async Task RunClientAsync(PluginBase pbase, IReadOnlyDictionary<string, JsonElement> config, VnCacheClient client)
        {
            ILogProvider Log = pbase.Log;

            try
            {
                //Try loading config
                await client.LoadConfigAsync(pbase, config);

                Log.Verbose("VNCache client configration loaded successfully");

                //Run and wait for exit
                await client.RunAsync(Log, pbase.UnloadToken);
            }
            catch (OperationCanceledException)
            { }
            catch (KeyNotFoundException e)
            {
                Log.Error("Missing required configuration variable for VnCache client: {0}", e.Message);
            }
            catch (FBMServerNegiationException fne)
            {
                Log.Error("Failed to negotiate connection with cache server {reason}", fne.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Cache client error occured in session provider");
            }
            finally
            {
                client.Dispose();
            }

            Log.Information("Cache client exited");
        }     
    }
}
