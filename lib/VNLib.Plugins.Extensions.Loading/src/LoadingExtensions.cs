/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Threading;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Collections.Generic;
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
            //Get local cache
            PluginLocalCache pc = _localCache.GetValue(plugin, PluginLocalCache.Create);
            return pc.GetOrCreateService(serviceType, serviceFactory);
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
            ArgumentNullException.ThrowIfNull(assemblyName);

            string[] searchDirs;

            /*
             * Allow an assets directory to limit the scope of the search for the desired
             * assembly, otherwise search all plugins directories
             */

            string? assetDir = plugin.Config().GetAssetsPath();

            searchDirs = assetDir is null
                ? plugin.Config().GetPluginSearchDirs()
                : ([assetDir]);

            /*
            * This should never happen since this method can only be called from a
            * plugin context, which means this path was used to load the current plugin
            */
            if (searchDirs.Length == 0)
            {
                throw new ConfigurationException("No plugin asset directory is defined for the current host configuration, this is likely a bug");
            }

            //Get the first file that matches the search file
            return searchDirs
                .SelectMany(d => Directory.EnumerateFiles(d, assemblyName, searchOption))
                .FirstOrDefault();
        }

        /// <summary>
        /// Loads a managed assembly into the current plugin's load context and will unload when disposed
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
                 ?? throw new FileNotFoundException($"Failed to find custom assembly {assemblyName} from plugin directory");

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
        /// Loads a managed assembly into the current plugin's load context and will unload when disposed
        /// or the plugin is unloaded from the host application. 
        /// </summary>
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
        public static ManagedLibrary LoadAssembly(
            this PluginBase plugin,
            string assemblyName,
            SearchOption dirSearchOption = SearchOption.AllDirectories,
            AssemblyLoadContext? explictAlc = null
        )
        {
            /*
             * Using an assembly loader instance instead of managed library, so it respects 
             * the plugin's unload events. Returning the managed library instance will
             * hide the overloads that would cause possible type load issues, so using
             * an object as the generic type parameter shouldn't be an issue.
             */
            return LoadAssembly<object>(plugin, assemblyName, dirSearchOption, explictAlc);
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
            return AssemblyLoadContext.GetLoadContext(executingAsm)
                ?? throw new InvalidOperationException("Could not get plugin's assembly load context");
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
            Type[] concreteTypes = executingAsm
                .GetTypes()
                .Where(t => !t.IsAbstract && abstractType.IsAssignableFrom(t))
                .ToArray();

            if (concreteTypes.Length == 0)
            {
                throw new ConcreteTypeNotFoundException(
                    $"Failed to load implemenation of abstract type {abstractType} because no concrete implementations were found in this assembly");
            }

            if (concreteTypes.Length > 1)
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
        public static void ThrowIfUnloaded(this PluginBase? plugin)
        {
            //See if the plugin was unlaoded
            ArgumentNullException.ThrowIfNull(plugin);
            ObjectDisposedException.ThrowIf(plugin.UnloadToken.IsCancellationRequested, plugin);
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

            //If plugin unloads during delay, bail
            if (plugin.UnloadToken.IsCancellationRequested)
            {
                return;
            }

            //Run on ts
            Task deferred = Task.Run(asyncTask);

            //Add task to deferred list
            plugin.ObserveTask(deferred);
            try
            {
                //Await the task results
                await deferred.ConfigureAwait(false);
            }
            catch (Exception ex)
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
            ArgumentNullException.ThrowIfNull(callback);

            //Wait method
            static async Task WaitForUnload(PluginBase pb, Action callback)
            {
                //Wait for unload as a task on the threadpool to avoid deadlocks
                _ = await pb.UnloadToken.WaitHandle
                    .NoSpinWaitAsync(Timeout.Infinite)
                    .ConfigureAwait(false);

                callback();
            }

            //Registaer the task to cause the plugin to wait
            return plugin.ObserveWork(() => WaitForUnload(plugin, callback));
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
        ) where T : class
        {
            /*
             * Get or create the library for the assembly path, but only load it once
             * Loading it on the plugin will also cause it be cleaned up when the plugin 
             * is unloaded.
             */
            ManagedLibrary manLib = _assemblyCache.GetOrAdd(assemblyDllName, (name) => LoadAssembly<T>(plugin, name, search, defaultCtx));
            Type[] matchingTypes = manLib.TryGetAllMatchingTypes<T>().ToArray();

            //try to get the first type that has the extern attribute, or fall back to the first public & concrete type
            Type? exported = matchingTypes.FirstOrDefault(t => t.GetCustomAttribute<ServiceExportAttribute>() != null)
                ?? matchingTypes.Where(t => !t.IsAbstract && t.IsPublic).FirstOrDefault();

            _ = exported ?? throw new TypeLoadException($"The desired external asset type {typeof(T).Name} is not exported as part of the assembly {manLib.Assembly.FullName}");

            //Try to get a configuration for the exported type
            if (plugin.Config().HasForType(exported))
            {
                //Get the config for the type and create the service
                return (T)CreateService(plugin, exported, plugin.Config().GetForType(exported));
            }

            //Create new instance of the desired type
            return (T)CreateService(plugin, exported, null);
        }

        /// <summary>
        /// Exports a service of the desired type to the host application. Once the plugin
        /// is done loading, the host will be able to access the service instance.
        /// <para>
        /// You should avoid mutating the service instance after the plugin has been 
        /// loaded, especially if you are using factory methods to create the service.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <param name="instance">The service instance to pass the host</param>
        /// <param name="flags">Optional export flags to pass to the host</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void ExportService<T>(this PluginBase plugin, T instance, ExportFlags flags = ExportFlags.None)
            where T : class => ExportService(plugin, typeof(T), instance, flags);

        /// <summary>
        /// Exports a service of the desired type to the host application. Once the plugin
        /// is done loading, the host will be able to access the service instance.
        /// <para>
        /// You should avoid mutating the service instance after the plugin has been 
        /// loaded, especially if you are using factory methods to create the service.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="type">The service type to export</param>
        /// <param name="instance">The service instance to pass the host</param>
        /// <param name="flags">Optional export flags to pass to the host</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void ExportService(this PluginBase plugin, Type type, object instance, ExportFlags flags = ExportFlags.None)
        {
            ArgumentNullException.ThrowIfNull(plugin, nameof(plugin));
            plugin.ThrowIfUnloaded();

            //Init new service wrapper
            ServiceExport export = new(type, instance, flags);
            plugin.Services.Add(export);
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
        public static T GetOrCreateSingleton<T>(this PluginBase plugin) 
            => GetOrCreateSingleton(plugin, CreateService<T>);

        /// <summary>
        /// <para>
        /// Gets or inializes a singleton service of the desired type.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
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
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
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
            return CreateService<T>(
                plugin,
                config: plugin.Config().TryGetForType<T>()
            );
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
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
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
            IConfigScope config = plugin.Config().Get(configName);
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
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
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
        public static T CreateService<T>(this PluginBase plugin, IConfigScope? config) 
            => (T)CreateService(plugin, typeof(T), config);

        /// <summary>
        /// <para>
        /// Creates and configures a new instance of the desired type, with the specified configuration scope
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derrives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
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
            ArgumentNullException.ThrowIfNull(plugin);
            ArgumentNullException.ThrowIfNull(serviceType);

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
                //Determine configuration requirments
                if (PluginConfigStore.ConfigurationRequired(serviceType) && config == null)
                {
                    PluginConfigStore.ThrowConfigNotFoundForType(serviceType);
                }

                service = InvokeServiceConstructor(serviceType, plugin, config);
            }
            catch (TargetInvocationException te) when (te.InnerException != null)
            {
                FindNestedConfigurationException(te);
                FindAndThrowInnerException(te);
                throw;
            }
            catch (Exception ex)
            {
                FindNestedConfigurationException(ex);
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
                _ = loading != null
                    ? loading.ContinueWith(t => ObserveWork(plugin, bw, randomDelay), TaskScheduler.Default)
                    : ObserveWork(plugin, bw, randomDelay);
            }

            //register dispose cleanup
            if (service is IDisposable disp)
            {
                _ = plugin.RegisterForUnload(disp.Dispose);
            }

            return service;
        }

        /*
         * Attempts to find the most appropriate constructor for the service type
         * if found, then invokes it to create the service instance
         */

        private static object InvokeServiceConstructor(Type serviceSType, PluginBase plugin, IConfigScope? config)
        {
            ConstructorInfo? constructor;

            /*
             * First try to load a constructor with the plugin and config scope
             */
            if (config != null)
            {
                constructor = serviceSType.GetConstructor([typeof(PluginBase), typeof(IConfigScope)]);

                if (constructor is not null)
                {
                    return constructor.Invoke([plugin, config]);
                }
            }

            //Try to get plugin only constructor
            constructor = serviceSType.GetConstructor([typeof(PluginBase)]);
            if (constructor is not null)
            {
                return constructor.Invoke([plugin]);
            }

            //Finally fall back to the empty constructor
            constructor = serviceSType.GetConstructor([]);

            return constructor is not null
                ? constructor.Invoke(null)
                : throw new MissingMemberException($"No constructor found for {serviceSType.Name}");
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

        internal static void FindNestedConfigurationException(Exception ex)
        {
            if (ex is ConfigurationException ce)
            {
                ExceptionDispatchInfo.Throw(ce);
            }

            //Recurse
            if (ex.InnerException is not null)
            {
                FindNestedConfigurationException(ex.InnerException);
            }

            //No more exceptions
        }

        private sealed class PluginLocalCache
        {
            private readonly PluginBase _plugin;
            private readonly Dictionary<Type, Lazy<object>> _store;

            private PluginLocalCache(PluginBase plugin)
            {
                _plugin = plugin;
                _store = [];
                //Register cleanup on unload
                _ = _plugin.RegisterForUnload(() => _store.Clear());
            }

            public static PluginLocalCache Create(PluginBase plugin) => new(plugin);

            /*
             * Service code should not be executed in multiple threads, so no need to lock
             * 
             * However if a service is added because it does not exist, the second call to 
             * get service, will invoke the creation callback. Which may be "recursive" 
             * as child dependencies required more services.
             */

            public object GetOrCreateService(Type serviceType, Func<PluginBase, object> ctor)
            {
                Lazy<object>? lazyService;

                lock (_store)
                {
                    lazyService = _store
                        .Where(t => t.Key.IsAssignableTo(serviceType))
                        .Select(static tk => tk.Value)
                        .FirstOrDefault();

                    if (lazyService is null)
                    {
                        lazyService = new Lazy<object>(() => ctor(_plugin));
                        //add to pool
                        _store.Add(serviceType, lazyService);
                    }
                }

                //Return the service instance
                return lazyService.Value;
            }
        }
    }
}
