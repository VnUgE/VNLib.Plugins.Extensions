/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: LoadingExtensions.cs 
*
* LoadingExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Accounts;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// Provides common loading (and unloading when required) extensions for plugins
    /// </summary>
    public static class LoadingExtensions
    {
        /// <summary>
        /// A key in the 'plugins' configuration object that specifies 
        /// an asset search directory
        /// </summary>
        public const string PLUGIN_ASSET_KEY = "assets";
        public const string DEBUG_CONFIG_KEY = "debug";
        public const string SECRETS_CONFIG_KEY = "secrets";
        public const string PASSWORD_HASHING_KEY = "passwords";

        /*
         * Plugin local cache used for storing singletons for a plugin instance
         */
        private static readonly ConditionalWeakTable<PluginBase, PluginLocalCache> _localCache = new();
       
        /// <summary>
        /// Gets a previously cached service singleton for the desired plugin
        /// </summary>
        /// <param name="serviceType">The service instance type</param>
        /// <param name="plugin">The plugin to obtain or build the singleton for</param>
        /// <param name="serviceFactory">The method to produce the singleton</param>
        /// <returns>The cached or newly created singleton</returns>
        public static object GetOrCreateSingleton(PluginBase plugin, Type serviceType, Func<PluginBase, object> serviceFactory)
        {
            Lazy<object>? service;
            //Get local cache
            PluginLocalCache pc = _localCache.GetValue(plugin, PluginLocalCache.Create);
            //Hold lock while get/set the singleton
            lock (pc.SyncRoot)
            {
                //Check if service already exists
                service = pc.GetService(serviceType);
                //publish the service if it isnt loaded yet
                service ??= pc.AddService(serviceType, serviceFactory);
            }
            //Deferred load of the service
            return service.Value;
        }

        /// <summary>
        /// Gets a previously cached service singleton for the desired plugin
        /// or creates a new singleton instance for the plugin
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin">The plugin to obtain or build the singleton for</param>
        /// <param name="serviceFactory">The method to produce the singleton</param>
        /// <returns>The cached or newly created singleton</returns>
        public static T GetOrCreateSingleton<T>(PluginBase plugin, Func<PluginBase, T> serviceFactory) 
            => (T)GetOrCreateSingleton(plugin, typeof(T), p => serviceFactory(p)!);

        
        /// <summary>
        /// Gets the plugins ambient <see cref="PasswordHashing"/> if loaded, or loads it if required. This class will
        /// be unloaded when the plugin us unloaded.
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="PasswordHashing"/></returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static PasswordHashing GetPasswords(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Get/load the passwords one time only
            return GetOrCreateSingleton(plugin, LoadPasswords);
        }
        
        private static PasswordHashing LoadPasswords(PluginBase plugin)
        {
            PasswordHashing Passwords;

            //Create new session provider
            SecretProvider secrets = new();

            //Load the secret in the background
            secrets.LoadSecret(plugin);

            //See hashing params are defined
            IReadOnlyDictionary<string, JsonElement>? hashingArgs = plugin.TryGetConfig(PASSWORD_HASHING_KEY);
            if (hashingArgs != null)
            {
                //Get hashing arguments
                uint saltLen = hashingArgs["salt_len"].GetUInt32();
                uint hashLen = hashingArgs["hash_len"].GetUInt32();
                uint timeCost = hashingArgs["time_cost"].GetUInt32();
                uint memoryCost = hashingArgs["memory_cost"].GetUInt32();
                uint parallelism = hashingArgs["parallelism"].GetUInt32();
                //Load passwords
                Passwords = new(secrets, (int)saltLen, timeCost, memoryCost, parallelism, hashLen);
            }
            else
            {
                //Init default password hashing
                Passwords = new(secrets);
            }
            //return
            return Passwords;
        }


        /// <summary>
        /// Loads an assembly into the current plugins AppDomain and will unload when disposed
        /// or the plugin is unloaded from the host application. 
        /// </summary>
        /// <typeparam name="T">The desired exported type to load from the assembly</typeparam>
        /// <param name="plugin"></param>
        /// <param name="assemblyName">The name of the assembly (ex: 'file.dll') to search for</param>
        /// <param name="dirSearchOption">Directory/file search option</param>
        /// <returns>The <see cref="AssemblyLoader{T}"/> managing the loaded assmbly in the current AppDomain</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <remarks>
        /// The assembly is searched within the 'assets' directory specified in the plugin config
        /// or the global plugins ('path' key) directory if an assets directory is not defined.
        /// </remarks>
        public static AssemblyLoader<T> LoadAssembly<T>(this PluginBase plugin, string assemblyName, SearchOption dirSearchOption = SearchOption.AllDirectories)
        {
            plugin.ThrowIfUnloaded();
            _ = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
            
            //get plugin directory from config
            IReadOnlyDictionary<string, JsonElement> config = plugin.GetConfig("plugins");

            /*
             * Allow an assets directory to limit the scope of the search for the desired
             * assembly, otherwise search all plugins directories
             */
            
            string? assetDir = config.GetPropString("assets");
            assetDir ??= config["path"].GetString();

            /*
             * This should never happen since this method can only be called from a
             * plugin context, which means this path was used to load the current plugin
             */
            _ = assetDir ?? throw new ArgumentNullException("assets", "No plugin path is defined for the current host configuration, this is likely a bug");
            
            //Get the first file that matches the search file
            string? asmFile = Directory.EnumerateFiles(assetDir, assemblyName, dirSearchOption).FirstOrDefault();
            _ = asmFile ?? throw new FileNotFoundException($"Failed to load custom assembly {assemblyName} from plugin directory");
            
            //Load the assembly
            return AssemblyLoader<T>.Load(asmFile, plugin.UnloadToken);
        }
        

        /// <summary>
        /// Determintes if the current plugin config has a debug propety set
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>True if debug mode is enabled, false otherwise</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static bool IsDebug(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Check for debug element
            return plugin.PluginConfig.TryGetProperty(DEBUG_CONFIG_KEY, out JsonElement dbgEl) && dbgEl.GetBoolean();
        }
        
        /// <summary>
        /// Internal exception helper to raise <see cref="ObjectDisposedException"/> if the plugin has been unlaoded
        /// </summary>
        /// <param name="plugin"></param>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void ThrowIfUnloaded(this PluginBase plugin)
        {
            //See if the plugin was unlaoded
            if (plugin.UnloadToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException("The plugin has been unloaded");
            }
        }

        /// <summary>
        /// Schedules an asynchronous callback function to run and its results will be observed
        /// when the operation completes, or when the plugin is unloading
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="asyncTask">The asynchronous operation to observe</param>
        /// <param name="delayMs">An optional startup delay for the operation</param>
        /// <returns>A task that completes when the deferred task completes </returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static async Task ObserveTask(this PluginBase plugin, Func<Task> asyncTask, int delayMs = 0)
        {
            /*
             * Motivation:
             * Sometimes during plugin loading, a plugin may want to asynchronously load
             * data, where the results are not required to be observed during loading, but 
             * should not be pending after the plugin is unloaded, as the assembly may be 
             * unloaded and referrences collected by the GC.
             * 
             * So we can use the plugin's unload cancellation token to observe the results
             * of a pending async operation 
             */

            //Test status
            plugin.ThrowIfUnloaded();

            //Optional delay
            await Task.Delay(delayMs);

            //Run on ts
            Task deferred = Task.Run(asyncTask);

            //Add task to deferred list
            plugin.ObserveTask(deferred);
            try
            {
                //Await the task results
                await deferred.ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                //Log errors
                plugin.Log.Error(ex, "Error occured while observing deferred task");
            }
            finally
            {
                //Remove task when complete
                plugin.RemoveObservedTask(deferred);
            }
        }


        /// <summary>
        /// Schedules work to begin after the specified delay to be observed by the plugin while 
        /// passing plugin specifie information. Exceptions are logged to the default plugin log
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="work">The work to be observed</param>
        /// <param name="delayMs">The time (in milliseconds) to delay dispatching the work item</param>
        /// <returns>The task that represents the scheduled work</returns>
        public static Task ObserveWork(this PluginBase plugin, IAsyncBackgroundWork work, int delayMs = 0)
        {
            return ObserveTask(plugin, () => work.DoWorkAsync(plugin.Log, plugin.UnloadToken), delayMs);
        }

        /// <summary>
        /// Registers an event to occur when the plugin is unloaded on a background thread
        /// and will cause the Plugin.Unload() method to block until the event completes
        /// </summary>
        /// <param name="pbase"></param>
        /// <param name="callback">The method to call when the plugin is unloaded</param>
        /// <returns>A task that represents the registered work</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Task RegisterForUnload(this PluginBase pbase, Action callback)
        {
            //Test status
            pbase.ThrowIfUnloaded();
            _ = callback ?? throw new ArgumentNullException(nameof(callback));

            //Wait method
            static async Task WaitForUnload(PluginBase pb, Action callback)
            {
                //Wait for unload as a task on the threadpool to avoid deadlocks
                await pb.UnloadToken.WaitHandle.WaitAsync()
                    .ConfigureAwait(false);
                
                callback();
            }

            //Registaer the task to cause the plugin to wait
            return pbase.ObserveTask(() => WaitForUnload(pbase, callback));
        }

        private sealed class PluginLocalCache
        {
            private readonly PluginBase _plugin;

            private readonly Dictionary<Type, Lazy<object>> _store;

            public object SyncRoot { get; } = new();

            private PluginLocalCache(PluginBase plugin)
            {
                _plugin = plugin;
                _store = new();
                //Register cleanup on unload
                _ = _plugin.RegisterForUnload(() => _store.Clear());
            }

            public static PluginLocalCache Create(PluginBase plugin) => new(plugin);


            public Lazy<object>? GetService(Type serviceType)
            {
                Lazy<object>? t = _store.Where(t => t.Key.IsAssignableTo(serviceType))
                    .Select(static tk => tk.Value)
                    .FirstOrDefault();
                return t;
            }

            public Lazy<object> AddService(Type serviceType, Func<PluginBase, object> factory)
            {
                //Get lazy loader to invoke factory outside of cache lock
                Lazy<object> lazyFactory = new(() => factory(_plugin), true);
                //Store lazy factory
                _store.Add(serviceType, lazyFactory);
                //Pass the lazy factory back
                return lazyFactory;
            }
        }

        private sealed class SecretProvider : ISecretProvider
        {
            private byte[]? _pepper;
            private Exception? _error;

            ///<inheritdoc/>
            public int BufferSize => _error != null ? throw _error : _pepper?.Length ?? 0;

            public ERRNO GetSecret(Span<byte> buffer)
            {
                if(_error != null)
                {
                    throw _error;
                }
                //Coppy pepper to buffer
                _pepper.CopyTo(buffer);
                //Return pepper length
                return _pepper!.Length;
            }

            public void LoadSecret(PluginBase pbase)
            {
                _ = pbase.ObserveTask(() => LoadSecretInternal(pbase));
            }

            private async Task LoadSecretInternal(PluginBase pbase)
            {
                try
                {
                    //Get the pepper from secret storage
                    _pepper = await pbase.TryGetSecretAsync(PASSWORD_HASHING_KEY).ToBase64Bytes();

                    //Regsiter cleanup
                    _ = pbase.RegisterForUnload(Clear);
                }
                catch(Exception ex)
                {
                    //Store exception for re-propagation
                    _error = ex;
                }
            }

            public void Clear()
            {
                //Clear the pepper if set
                MemoryUtil.InitializeBlock(_pepper.AsSpan());
            }
        }
    }
}
