/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Text.Json;
using System.Threading;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Utils.Resources;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading
{
    using static PluginDependencyExtensions;

    /// <summary>
    /// Provides common loading and unloading extensions for plugins.
    /// </summary>
    public static class LoadingExtensions
    {
        /// <summary>
        /// Represents a key in the 'plugins' configuration object that specifies 
        /// an asset search directory.
        /// </summary>
        public const string DEBUG_CONFIG_KEY = "debug";       

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the plugin has been unloaded.
        /// </summary>
        /// <param name="plugin">The plugin to check for unload status.</param>
        /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        public static void ThrowIfUnloaded(this PluginBase? plugin)
        {
            //See if the plugin was unloaded
            ArgumentNullException.ThrowIfNull(plugin);
            ObjectDisposedException.ThrowIf(plugin.UnloadToken.IsCancellationRequested, plugin);
        }

        /// <summary>
        /// Determines whether the current plugin configuration has the debug property set.
        /// </summary>
        /// <param name="plugin">The plugin whose configuration to inspect.</param>
        /// <returns>A value indicating whether debug mode is enabled in the plugin configuration.</returns>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        public static bool IsDebug(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Check for debug element
            return plugin.PluginConfig.TryGetProperty(DEBUG_CONFIG_KEY, out JsonElement dbgEl) && dbgEl.GetBoolean();
        }               

        /// <summary>
        /// Gets a previously cached service singleton for the desired plugin or creates a new singleton instance.
        /// </summary>
        /// <param name="serviceType">The service instance type.</param>
        /// <param name="plugin">The plugin to obtain or build the singleton for.</param>
        /// <param name="serviceFactory">The method to produce the singleton.</param>
        /// <returns>The cached or newly created singleton.</returns>
        [Obsolete("Prefer plugin.Deps().GetOrCreateSingleton() instead")]
        public static object GetOrCreateSingleton(PluginBase plugin, Type serviceType, Func<PluginBase, object> serviceFactory)
            => plugin.Deps().GetOrCreateSingleton(serviceType, serviceFactory);

        /// <summary>
        /// Gets a previously cached service singleton for the desired plugin or creates a new singleton instance.
        /// </summary>
        /// <typeparam name="T">The type of the service singleton to get or create.</typeparam>
        /// <param name="plugin">The plugin to obtain or build the singleton for.</param>
        /// <param name="serviceFactory">The method to produce the singleton.</param>
        /// <returns>The cached or newly created singleton.</returns>
        [Obsolete("Prefer plugin.Deps().GetOrCreateSingleton() instead")]
        public static T GetOrCreateSingleton<T>(PluginBase plugin, Func<PluginBase, T> serviceFactory)
            => plugin.Deps().GetOrCreateSingleton(serviceFactory);

        /// <summary>
        /// Gets the current plugin's <see cref="AssemblyLoadContext"/>.
        /// </summary>
        /// <returns>The <see cref="AssemblyLoadContext"/> associated with the current plugin.</returns>
        /// <exception cref="InvalidOperationException">No plugin load context is available for the current execution context.</exception>
        [Obsolete("Prefer PluginDependencies.GetPluginLoadContext() instead")]
        public static AssemblyLoadContext GetPluginLoadContext()
            => PluginDependencies.GetPluginLoadContext();

        /// <summary>
        /// Gets the single concrete type that implements the specified abstract type from the current assembly.
        /// </summary>
        /// <param name="abstractType">The abstract type to find the concrete implementation of.</param>
        /// <returns>The concrete type that implements the abstract type.</returns>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the abstract type is found in the current assembly.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the abstract type are found in the current assembly.</exception>
        [Obsolete("Prefer PluginDependencies.GetTypeImplFromCurrentAssembly() instead")]
        public static Type GetTypeImplFromCurrentAssembly(Type abstractType)
            => PluginDependencies.GetTypeImplFromCurrentAssembly(abstractType);       

        /// <summary>
        /// Schedules an asynchronous callback function to run and observes its results
        /// when the operation completes, or when the plugin is unloading.
        /// </summary>
        /// <param name="plugin">The plugin that observes the asynchronous work.</param>
        /// <param name="asyncTask">The asynchronous operation to observe.</param>
        /// <param name="delayMs">An optional startup delay for the operation, in milliseconds.</param>
        /// <returns>A task that completes when the deferred task completes.</returns>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        [Obsolete("Prefer plugin.Tasks().ObserveWork() instead")]
        public static Task ObserveWork(this PluginBase plugin, Func<Task> asyncTask, int delayMs = 0)
            => plugin.Tasks().ObserveWork(asyncTask, delayMs);

        /// <summary>
        /// Schedules work to begin after the specified delay to be observed by the plugin while 
        /// passing plugin specified information. Exceptions are logged to the default plugin log.
        /// </summary>
        /// <param name="plugin">The plugin that observes the background work.</param>
        /// <param name="work">The work to be observed.</param>
        /// <param name="delayMs">The time, in milliseconds, to delay dispatching the work item.</param>
        /// <returns>The task that represents the scheduled work.</returns>
        [Obsolete("Prefer plugin.Tasks().ObserveWork() instead")]
        public static Task ObserveWork(this PluginBase plugin, IAsyncBackgroundWork work, int delayMs = 0)
            => plugin.Tasks().ObserveWork(work, delayMs);

        /// <summary>
        /// Registers a callback to occur when the plugin is unloaded on a background thread
        /// and causes the <see cref="PluginBase.Unload"/> method to block until the callback completes.
        /// </summary>
        /// <param name="plugin">The plugin to register the unload callback on.</param>
        /// <param name="callback">The method to call when the plugin is unloaded.</param>
        /// <returns>A task that represents the registered work.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        [Obsolete("Prefer plugin.Tasks().RegisterForUnload() instead")]
        public static Task RegisterForUnload(this PluginBase plugin, Action callback)
        {
            plugin.Tasks().RegisterForUnload(callback);
            return plugin.UnloadToken.WaitHandle.WaitAsync();
        }

        /// <summary>
        /// Exports a service of the desired type to the host application.
        /// </summary>
        /// <typeparam name="T">The type of the service to export.</typeparam>
        /// <param name="plugin">The plugin exporting the service.</param>
        /// <param name="instance">The service instance to pass to the host.</param>
        /// <param name="flags">Optional export flags to pass to the host.</param>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <remarks>
        /// You should avoid mutating the service instance after the plugin has been 
        /// loaded, especially if you are using factory methods to create the service.
        /// </remarks>
        [Obsolete("Prefer plugin.Host().Services().Export<T>() instead")]
        public static void ExportService<T>(this PluginBase plugin, T instance, ExportFlags flags = ExportFlags.None)
            where T : class => plugin.Host().Services().Export<T>(instance, flags);

        /// <summary>
        /// Exports a service of the desired type to the host application.
        /// </summary>
        /// <param name="plugin">The plugin exporting the service.</param>
        /// <param name="type">The service type to export.</param>
        /// <param name="instance">The service instance to pass to the host.</param>
        /// <param name="flags">Optional export flags to pass to the host.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="instance"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <remarks>
        /// You should avoid mutating the service instance after the plugin has been 
        /// loaded, especially if you are using factory methods to create the service.
        /// </remarks>
        [Obsolete("Prefer plugin.Host().Services().Export() instead")]
        public static void ExportService(this PluginBase plugin, Type type, object instance, ExportFlags flags = ExportFlags.None)
            => plugin.Host().Services().Export(type, instance, flags);

        /// <summary>
        /// Loads a managed assembly into the current plugin's load context and will unload when disposed
        /// or the plugin is unloaded from the host application.
        /// </summary>
        /// <typeparam name="T">The desired exported type to load from the assembly.</typeparam>
        /// <param name="plugin">The plugin that owns the load context.</param>
        /// <param name="assemblyName">The name of the assembly (e.g. 'file.dll') to search for.</param>
        /// <param name="dirSearchOption">The directory search option to use when locating the assembly file.</param>
        /// <param name="explicitAlc">
        /// An explicit <see cref="AssemblyLoadContext"/> to load the assembly and its dependencies into.
        /// If <see langword="null"/>, uses the plugin's load context.
        /// </param>
        /// <returns>The <see cref="AssemblyLoader{T}"/> managing the loaded assembly in the current AppDomain.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="assemblyName"/> is <see langword="null"/>.</exception>
        /// <exception cref="FileNotFoundException">The specified assembly file cannot be found.</exception>
        /// <remarks>
        /// The assembly is searched within the 'assets' directory specified in the plugin config
        /// or the global plugins ('path' key) directory if an assets directory is not defined.
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().LoadAssembly<T>() instead")]
        public static AssemblyLoader<T> LoadAssembly<T>(
            this PluginBase plugin,
            string assemblyName,
            SearchOption dirSearchOption = SearchOption.AllDirectories,
            AssemblyLoadContext? explicitAlc = null
        )
            => plugin.Deps().LoadAssembly<T>(assemblyName, dirSearchOption, explicitAlc);

        /// <summary>
        /// Loads a managed assembly into the current plugin's load context and will unload when disposed
        /// or the plugin is unloaded from the host application.
        /// </summary>
        /// <param name="plugin">The plugin that owns the load context.</param>
        /// <param name="assemblyName">The name of the assembly (e.g. 'file.dll') to search for.</param>
        /// <param name="dirSearchOption">The directory search option to use when locating the assembly file.</param>
        /// <param name="explicitAlc">
        /// An explicit <see cref="AssemblyLoadContext"/> to load the assembly and its dependencies into.
        /// If <see langword="null"/>, uses the plugin's load context.
        /// </param>
        /// <returns>The <see cref="ManagedLibrary"/> managing the loaded assembly in the current AppDomain.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="assemblyName"/> is <see langword="null"/>.</exception>
        /// <exception cref="FileNotFoundException">The specified assembly file cannot be found.</exception>
        /// <remarks>
        /// The assembly is searched within the 'assets' directory specified in the plugin config
        /// or the global plugins ('path' key) directory if an assets directory is not defined.
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().LoadAssembly() instead")]
        public static ManagedLibrary LoadAssembly(
            this PluginBase plugin,
            string assemblyName,
            SearchOption dirSearchOption = SearchOption.AllDirectories,
            AssemblyLoadContext? explicitAlc = null
        )
            => plugin.Deps().LoadAssembly(assemblyName, dirSearchOption, explicitAlc);      

        /// <summary>
        /// Configures the service asynchronously on the plugin's scheduler and returns a task
        /// that represents the configuration work.
        /// </summary>
        /// <typeparam name="T">The service type to configure.</typeparam>
        /// <param name="plugin">The plugin that owns the service.</param>
        /// <param name="service">The service to configure.</param>
        /// <param name="delayMs">The time, in milliseconds, to delay the configuration task.</param>
        /// <returns>A task that completes when the configuration operation completes.</returns>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        [Obsolete("Prefer plugin.Tasks().ConfigureServiceAsync() instead")]
        public static Task ConfigureServiceAsync<T>(this PluginBase plugin, T service, int delayMs = 0) where T : IAsyncConfigurable
            => plugin.Tasks().ConfigureServiceAsync(service, delayMs);

        /// <summary>
        /// Creates a new instance of the desired service type from an external assembly and 
        /// caches the loaded assembly so it is never loaded more than once.
        /// </summary>
        /// <typeparam name="T">The service type, which may be an interface or abstract type.</typeparam>
        /// <param name="plugin">The plugin that manages the loaded assembly lifetime.</param>
        /// <param name="assemblyDllName">The name of the assembly that contains the desired type to search for.</param>
        /// <param name="search">The directory search method to use when locating the assembly.</param>
        /// <param name="defaultCtx">An <see cref="AssemblyLoadContext"/> to load the assembly into. If <see langword="null"/>, uses the plugin's current load context.</param>
        /// <returns>A new instance of the desired service type.</returns>
        /// <exception cref="TypeLoadException">The specified type cannot be found or loaded from the assembly.</exception>
        /// <remarks>
        /// Managed assembly lifecycles are managed by the plugin. Instances are treated as services and 
        /// their service hooks will be called like any internal service.
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().CreateExternal<T>() instead")]
        public static T CreateServiceExternal<T>(
            this PluginBase plugin,
            string assemblyDllName,
            SearchOption search = SearchOption.AllDirectories,
            AssemblyLoadContext? defaultCtx = null
        ) where T : class
            => plugin.Deps().LoadExternal<T>(assemblyDllName, search, defaultCtx);

        /// <summary>
        /// Gets or initializes a singleton service of the desired type.
        /// </summary>
        /// <typeparam name="T">The type of the service singleton to get or create.</typeparam>
        /// <param name="plugin">The plugin that owns the singleton.</param>
        /// <returns>The cached or newly created service singleton.</returns>
        /// <exception cref="KeyNotFoundException">The configuration key for the specified type is not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <exception cref="EntryPointNotFoundException">The type has no valid public constructor found.</exception>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the specified service type is found.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the specified service type are found.</exception>
        /// <remarks>
        /// <para>
        /// If the type derives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, System.Threading.CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().GetOrCreateSingleton() instead")]
        public static T GetOrCreateSingleton<T>(this PluginBase plugin)
            => plugin.Deps().GetOrCreateSingleton<T>();

        /// <summary>
        /// Gets or initializes a singleton service of the desired type with a custom configuration name.
        /// </summary>
        /// <typeparam name="T">The type of the service singleton to get or create.</typeparam>
        /// <param name="plugin">The plugin that owns the singleton.</param>
        /// <param name="configName">The configuration property name that overrides the default.</param>
        /// <returns>The configured service singleton.</returns>
        /// <exception cref="KeyNotFoundException">The configuration key for the specified type is not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <exception cref="EntryPointNotFoundException">The type has no valid public constructor found.</exception>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the specified service type is found.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the specified service type are found.</exception>
        /// <remarks>
        /// <para>
        /// If the type derives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().GetOrCreateSingleton() instead")]
        public static T GetOrCreateSingleton<T>(this PluginBase plugin, string configName)
            => plugin.Deps().GetOrCreateSingleton<T>(configName);

        /// <summary>
        /// Creates and configures a new instance of the desired type and captures the configuration
        /// information from the type.
        /// </summary>
        /// <typeparam name="T">The service type to create.</typeparam>
        /// <param name="plugin">The plugin that manages the created service.</param>
        /// <returns>A new configured instance of the service type.</returns>
        /// <exception cref="KeyNotFoundException">The configuration key for the specified type is not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <exception cref="EntryPointNotFoundException">The type has no valid public constructor found.</exception>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the specified service type is found.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the specified service type are found.</exception>
        /// <remarks>
        /// <para>
        /// If the type derives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IDisposable"/> the <see cref="IDisposable.Dispose"/> method is called once when 
        /// the plugin is unloaded.
        /// </para>
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().Create() instead")]
        public static T CreateService<T>(this PluginBase plugin)
            => plugin.Deps().Create<T>();

        /// <summary>
        /// Creates and configures a new instance of the desired type with the specified configuration property name.
        /// </summary>
        /// <typeparam name="T">The service type to create.</typeparam>
        /// <param name="plugin">The plugin that manages the created service.</param>
        /// <param name="configName">The configuration element name to pass to the new instance.</param>
        /// <returns>A new configured instance of the service type.</returns>
        /// <exception cref="KeyNotFoundException">The configuration key for the specified type is not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <exception cref="EntryPointNotFoundException">The type has no valid public constructor found.</exception>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the specified service type is found.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the specified service type are found.</exception>
        /// <remarks>
        /// <para>
        /// If the type derives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().Create() instead")]
        public static T CreateService<T>(this PluginBase plugin, string configName)
            => plugin.Deps().Create<T>(configName);

        /// <summary>
        /// Creates and configures a new instance of the desired type with the specified configuration scope.
        /// </summary>
        /// <typeparam name="T">The service type to create.</typeparam>
        /// <param name="plugin">The plugin that manages the created service.</param>
        /// <param name="config">The configuration scope to pass directly to the new instance.</param>
        /// <returns>A new configured instance of the service type.</returns>
        /// <exception cref="KeyNotFoundException">The configuration key for the specified type is not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <exception cref="EntryPointNotFoundException">The type has no valid public constructor found.</exception>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the specified service type is found.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the specified service type are found.</exception>
        /// <remarks>
        /// <para>
        /// If the type derives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().Create() instead")]
        public static T CreateService<T>(this PluginBase plugin, IConfigScope? config)
            => plugin.Deps().Create<T>(config);

        /// <summary>
        /// Creates and configures a new instance of the desired type with the specified configuration scope.
        /// </summary>
        /// <param name="plugin">The plugin that manages the created service.</param>
        /// <param name="serviceType">The service type to instantiate.</param>
        /// <param name="config">The configuration scope to pass directly to the new instance.</param>
        /// <returns>A new configured instance of the service type.</returns>
        /// <exception cref="KeyNotFoundException">The configuration key for the specified type is not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been unloaded or disposed.</exception>
        /// <exception cref="EntryPointNotFoundException">The type has no valid public constructor found.</exception>
        /// <exception cref="ConcreteTypeNotFoundException">No concrete type implementing the specified service type is found.</exception>
        /// <exception cref="ConcreteTypeAmbiguousMatchException">Multiple concrete types implementing the specified service type are found.</exception>
        /// <remarks>
        /// <para>
        /// If the type derives <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// <para>
        /// If the type derives <see cref="IAsyncBackgroundWork"/> the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/>
        /// method is called once when the instance is loaded, and observed on the plugin scheduler.
        /// </para>
        /// </remarks>
        [Obsolete("Prefer plugin.Deps().Create() instead")]
        public static object CreateService(this PluginBase plugin, Type serviceType, IConfigScope? config)
            => plugin.Deps().Create(serviceType, config);      
    }
}

