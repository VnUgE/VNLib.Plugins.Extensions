/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Utils.Extensions;

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
      
        public const string DEBUG_CONFIG_KEY = "debug";
        public const string SECRETS_CONFIG_KEY = "secrets";
        public const string PASSWORD_HASHING_KEY = "passwords";
        public const string CUSTOM_PASSWORD_ASM_KEY = "custom_asm";

        /*
         * Plugin local cache used for storing singletons for a plugin instance
         */
        private static readonly ConditionalWeakTable<PluginBase, PluginLocalCache> _localCache = new();
        private static readonly ConcurrentDictionary<string, ManagedLibrary> _assemblyCache = new();


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
        /// Gets the full file path for the assembly asset file name within the assets
        /// directory. 
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="assemblyName">The name of the assembly (ex: 'file.dll') to search for</param>
        /// <param name="searchOption">Directory search flags</param>
        /// <returns>The full path to the assembly asset file, or null if the file does not exist</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string? GetAssetFilePath(this PluginBase plugin, string assemblyName, SearchOption searchOption)
        {
            plugin.ThrowIfUnloaded();
            _ = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));

            /*
             * Allow an assets directory to limit the scope of the search for the desired
             * assembly, otherwise search all plugins directories
             */

            string? assetDir = plugin.GetAssetsPath();
            assetDir ??= plugin.GetPluginsPath();

            /*
             * This should never happen since this method can only be called from a
             * plugin context, which means this path was used to load the current plugin
             */
            _ = assetDir ?? throw new ArgumentNullException(ConfigurationExtensions.PLUGIN_ASSET_KEY, "No plugin path is defined for the current host configuration, this is likely a bug");

            //Get the first file that matches the search file
            return Directory.EnumerateFiles(assetDir, assemblyName, searchOption).FirstOrDefault();
        }

        /// <summary>
        /// Loads an assembly into the current plugin's load context and will unload when disposed
        /// or the plugin is unloaded from the host application. 
        /// </summary>
        /// <typeparam name="T">The desired exported type to load from the assembly</typeparam>
        /// <param name="plugin"></param>
        /// <param name="assemblyName">The name of the assembly (ex: 'file.dll') to search for</param>
        /// <param name="dirSearchOption">Directory/file search option</param>
        /// <param name="explictAlc">
        /// Explicitly define an <see cref="AssemblyLoadContext"/> to load the assmbly, and it's dependencies
        /// into. If null, uses the plugin's alc.
        /// </param>
        /// <returns>The <see cref="AssemblyLoader{T}"/> managing the loaded assmbly in the current AppDomain</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <remarks>
        /// The assembly is searched within the 'assets' directory specified in the plugin config
        /// or the global plugins ('path' key) directory if an assets directory is not defined.
        /// </remarks>
        public static AssemblyLoader<T> LoadAssembly<T>(
            this PluginBase plugin, 
            string assemblyName, 
            SearchOption dirSearchOption = SearchOption.AllDirectories, 
            AssemblyLoadContext? explictAlc = null)
        {
            //Get the file path for the assembly
            string asmFile = GetAssetFilePath(plugin, assemblyName, dirSearchOption)
                 ?? throw new FileNotFoundException($"Failed to load custom assembly {assemblyName} from plugin directory");

            //Get the plugin's load context if not explicitly supplied
            explictAlc ??= GetPluginLoadContext();

            if (plugin.IsDebug())
            {
                plugin.Log.Verbose("Loading assembly {asm}: from file {file}", assemblyName, asmFile);
            }
            //Load the assembly
            return AssemblyLoader<T>.Load(asmFile, explictAlc, plugin.UnloadToken);
        }
        
        /// <summary>
        /// Gets the current plugin's <see cref="AssemblyLoadContext"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static AssemblyLoadContext GetPluginLoadContext()
        {
           /*
            * Since this library should only be used in a plugin context, the executing assembly
            * will be loaded into the plugin's isolated load context. So we can get the load 
            * context for the executing assembly and use that as the plugin's load context.
            */

            Assembly executingAsm = Assembly.GetExecutingAssembly();
            return AssemblyLoadContext.GetLoadContext(executingAsm) ?? throw new InvalidOperationException("Could not get plugin's assembly load context");
        }

        /// <summary>
        /// Gets a single type implemenation of the abstract type from the current assembly. If multiple
        /// concrete types are found, an exception is raised, if no concrete types are found, an exception
        /// is raised.
        /// </summary>
        /// <param name="abstractType">The abstract type to get the concrete type from</param>
        /// <returns>The concrete type if found</returns>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static Type GetTypeImplFromCurrentAssembly(Type abstractType)
        {
            //Get all types from the current assembly that implement the abstract type
            Assembly executingAsm = Assembly.GetExecutingAssembly();
            Type[] concreteTypes = executingAsm.GetTypes().Where(t => !t.IsAbstract && abstractType.IsAssignableFrom(t)).ToArray();

            if(concreteTypes.Length == 0)
            {
                throw new ConcreteTypeNotFoundException($"Failed to load implemenation of abstract type {abstractType} because no concrete implementations were found in this assembly");
            }

            if(concreteTypes.Length > 1)
            {
                throw new ConcreteTypeAmbiguousMatchException(
                    $"Failed to load implemenation of abstract type {abstractType} because multiple concrete implementations were found in this assembly");
            }

            //Get the only concrete type
            return concreteTypes[0];
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
        public static async Task ObserveWork(this PluginBase plugin, Func<Task> asyncTask, int delayMs = 0)
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
            return ObserveWork(plugin, () => work.DoWorkAsync(plugin.Log, plugin.UnloadToken), delayMs);
        }

        /// <summary>
        /// Registers an event to occur when the plugin is unloaded on a background thread
        /// and will cause the Plugin.Unload() method to block until the event completes
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="callback">The method to call when the plugin is unloaded</param>
        /// <returns>A task that represents the registered work</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Task RegisterForUnload(this PluginBase plugin, Action callback)
        {
            //Test status
            plugin.ThrowIfUnloaded();
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
            return plugin.ObserveWork(() => WaitForUnload(plugin, callback));
        }

        /// <summary>
        /// Adds a service to the <see cref="IServiceContainer"/> using generics syntax
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="container"></param>
        /// <param name="service">The service instance to add to the container</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AddService<T>(this IServiceContainer container, T service)
        {
            _ = container ?? throw new ArgumentNullException(nameof(container));
            _ = service ?? throw new ArgumentNullException(nameof(service));

            container.AddService(typeof(T), service);
        }

        /// <summary>
        /// Creates a new instance of the desired service type from an external assembly and 
        /// caches the loaded assembly so it's never loaded more than once. Managed assembly 
        /// life cycles are managed by the plugin. Instances are treated as services and 
        /// their service hooks will be called like any internal service.
        /// </summary>
        /// <typeparam name="T">The service type, may be an interface or abstract type</typeparam>
        /// <param name="plugin"></param>
        /// <param name="assemblyDllName">The name of the assembly that contains the desired type to search for</param>
        /// <param name="search">The directory search method</param>
        /// <param name="defaultCtx">A <see cref="AssemblyLoadContext"/> to load the assembly into. Defaults to the plugins current ALC</param>
        /// <returns>A new instance of the desired service type </returns>
        /// <exception cref="TypeLoadException"></exception>
        public static T CreateServiceExternal<T>(
            this PluginBase plugin, 
            string assemblyDllName, 
            SearchOption search = SearchOption.AllDirectories, 
            AssemblyLoadContext? defaultCtx = null
        )
        {
            /*
             * Get or create the library for the assembly path, but only load it once
             * Loading it on the plugin will also cause it be cleaned up when the plugin 
             * is unloaded.
             */
            ManagedLibrary manLib = _assemblyCache.GetOrAdd(assemblyDllName, (name) => plugin.LoadAssembly<T>(name, search, defaultCtx));
            Type[] matchingTypes = manLib.TryGetAllMatchingTypes<T>().ToArray();

            //try to get the first type that has the extern attribute, or fall back to the first public & concrete type
            Type? exported = matchingTypes.FirstOrDefault(t => t.GetCustomAttribute<ExternServiceAttribute>() != null)
                ?? matchingTypes.Where(t => !t.IsAbstract && t.IsPublic).FirstOrDefault();
           
            _ = exported ?? throw new TypeLoadException($"The desired external asset type {typeof(T).Name} is not exported as part of the assembly {manLib.Assembly.FullName}");

            //Try to get a configuration for the exported type
            if (plugin.HasConfigForType(exported))
            {
                //Get the config for the type and create the service
                return (T)CreateService(plugin, exported, plugin.GetConfigForType(exported));
            }

            //Create new instance of the desired type
            return (T)CreateService(plugin, exported, null);
        }


        /// <summary>
        /// <para>
        /// Gets or inializes a singleton service of the desired type.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static T GetOrCreateSingleton<T>(this PluginBase plugin) => GetOrCreateSingleton(plugin, CreateService<T>);

        /// <summary>
        /// <para>
        /// Gets or inializes a singleton service of the desired type.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <param name="configName">Overrids the default configuration property name</param>
        /// <returns>The configured service singleton</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static T GetOrCreateSingleton<T>(this PluginBase plugin, string configName) 
            => GetOrCreateSingleton(plugin, (plugin) => CreateService<T>(plugin, configName));

        /// <summary>
        /// Configures the service asynchronously on the plugin's scheduler and returns a task
        /// that represents the configuration work.
        /// </summary>
        /// <typeparam name="T">The service type</typeparam>
        /// <param name="plugin"></param>
        /// <param name="service">The service to configure</param>
        /// <param name="delayMs">The time in milliseconds to delay the configuration task</param>
        /// <returns>A task that complets when the load operation completes</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static Task ConfigureServiceAsync<T>(this PluginBase plugin, T service, int delayMs = 0) where T : IAsyncConfigurable
        {
            //Register async load
            return ObserveWork(plugin, () => service.ConfigureServiceAsync(plugin), delayMs);
        }

        /// <summary>
        /// <para>
        /// Creates and configures a new instance of the desired type and captures the configuration
        /// information from the type.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IDisposable"/> the <see cref="IDisposable.Dispose"/> method is called once when 
        /// the plugin is unloaded.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The service type</typeparam>
        /// <param name="plugin"></param>
        /// <returns>The a new instance configured service</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static T CreateService<T>(this PluginBase plugin)
        {
            if (plugin.HasConfigForType<T>())
            {
                IConfigScope config = plugin.GetConfigForType<T>();
                return CreateService<T>(plugin, config);
            }
            else
            {
                return CreateService<T>(plugin, (IConfigScope?)null);
            }
        }

        /// <summary>
        /// <para>
        /// Creates and configures a new instance of the desired type, with the configuration property name
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The service type</typeparam>
        /// <param name="plugin"></param>
        /// <param name="configName">The configuration element name to pass to the new instance</param>
        /// <returns>The a new instance configured service</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static T CreateService<T>(this PluginBase plugin, string configName)
        {
            IConfigScope config = plugin.GetConfig(configName);
            return CreateService<T>(plugin, config);
        }

        /// <summary>
        /// <para>
        /// Creates and configures a new instance of the desired type, with the specified configuration scope
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The service type</typeparam>
        /// <param name="plugin"></param>
        /// <param name="config">The configuration scope to pass directly to the new instance</param>
        /// <returns>The a new instance configured service</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static T CreateService<T>(this PluginBase plugin, IConfigScope? config) => (T)CreateService(plugin, typeof(T), config);

        /// <summary>
        /// <para>
        /// Creates and configures a new instance of the desired type, with the specified configuration scope
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="serviceType">The service type to instantiate</param>
        /// <param name="config">The configuration scope to pass directly to the new instance</param>
        /// <returns>The a new instance configured service</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        /// <exception cref="ConcreteTypeNotFoundException"></exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException"></exception>
        public static object CreateService(this PluginBase plugin, Type serviceType, IConfigScope? config)
        {
            _ = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

            plugin.ThrowIfUnloaded();

            //The requested sesrvice is not a class, so see if we can find a default implementation in assembly
            if (serviceType.IsAbstract || serviceType.IsInterface)
            {
                //Overwrite the service type with the default implementation
                serviceType = GetTypeImplFromCurrentAssembly(serviceType);
            }

            object service;

            try
            {
                //Determin configuration requirments
                if (ConfigurationExtensions.ConfigurationRequired(serviceType) || config != null)
                {
                    if (config == null)
                    {
                        ConfigurationExtensions.ThrowConfigNotFoundForType(serviceType);
                    }

                    //Get the constructor for required or available config
                    ConstructorInfo? constructor = serviceType.GetConstructor(new Type[] { typeof(PluginBase), typeof(IConfigScope) });

                    //Make sure the constructor exists
                    _ = constructor ?? throw new MissingMemberException($"No constructor found for {serviceType.Name}");

                    //Call constructore
                    service = constructor.Invoke(new object[2] { plugin, config });
                }
                else
                {
                    //Get the constructor
                    ConstructorInfo? constructor = serviceType.GetConstructor(new Type[] { typeof(PluginBase) });

                    //Make sure the constructor exists
                    _ = constructor ?? throw new MissingMemberException($"No constructor found for {serviceType.Name}");

                    //Call constructore
                    service = constructor.Invoke(new object[1] { plugin });
                }
            }
            catch(TargetInvocationException te) when (te.InnerException != null) 
            {
                FindAndThrowInnerException(te);
                throw;
            }

            Task? loading = null;

            //If the service is async configurable, configure it
            if (service is IAsyncConfigurable asc)
            {
#pragma warning disable CA5394 // Do not use insecure randomness
                int randomDelay = Random.Shared.Next(1, 100);
#pragma warning restore CA5394 // Do not use insecure randomness

                //Register async load
                loading = plugin.ConfigureServiceAsync(asc, randomDelay);
            }

            //Allow background work loading
            if (service is IAsyncBackgroundWork bw)
            {

#pragma warning disable CA5394 // Do not use insecure randomness
                int randomDelay = Random.Shared.Next(10, 200);
#pragma warning restore CA5394 // Do not use insecure randomness

                //If the instances supports async loading, dont start work until its loaded
                if (loading != null)
                {
                    _ = loading.ContinueWith(t => ObserveWork(plugin, bw, randomDelay), TaskScheduler.Default);
                }
                else
                {
                    _ = ObserveWork(plugin, bw, randomDelay);
                }
            }

            //register dispose cleanup
            if (service is IDisposable disp)
            {
                _ = plugin.RegisterForUnload(disp.Dispose);
            }

            return service;
        }

        [DoesNotReturn]
        internal static void FindAndThrowInnerException(Exception ex)
        {
            //Recursivley search for the innermost exception of a TIE
            if (ex is TargetInvocationException && ex.InnerException != null)
            {
                FindAndThrowInnerException(ex.InnerException);
            }
            else
            {
                ExceptionDispatchInfo.Throw(ex);
            }
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
    }
}
