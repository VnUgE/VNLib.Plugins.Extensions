/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginDependencyExtensions.cs 
*
* PluginDependencyExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Threading;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using VNLib.Utils.Logging;
using VNLib.Utils.Resources;

namespace VNLib.Plugins.Extensions.Loading
{
    using static PluginConfigExtensions;

    using AssemblyCache = Dictionary<string, ManagedLibrary>;

    /// <summary>
    /// Declares a class as an external service provider.
    /// </summary>
    /// <remarks>
    /// <para>If an assembly contains multiple classes that have the same base type as a desired 
    /// type, this attribute can be declared to indicate the type takes precedence over other types.</para>
    /// <para>This attribute should be placed on classes that expect to be dynamically loaded. It provides 
    /// a hint to the loader that the type should take precedence over other types that may be found 
    /// that match the desired type. This is especially useful when multiple implementations of the 
    /// same service interface exist.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ServiceExportAttribute() : Attribute
    { }

    public static class PluginDependencyExtensions
    {
        private static readonly ConditionalWeakTable<PluginBase, SingletonCache> _singletons = [];
        private static readonly ConditionalWeakTable<PluginBase, AssemblyCache> _assemblyCaches = [];

        /// <summary>
        /// Gets a <see cref="PluginDependencies"/> ref struct for the plugin that provides scoped access
        /// to dependency injection, service lifecycle, and singleton management operations.
        /// </summary>
        /// <param name="plugin">The plugin instance to scope dependency operations to.</param>
        /// <returns>A new <see cref="PluginDependencies"/> for the plugin instance.</returns>
        /// <exception cref="ArgumentNullException">when <paramref name="plugin"/> is <see langword="null"/>.</exception>
        public static PluginDependencies Deps(this PluginBase plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            return new(plugin);
        }

        /// <summary>
        /// Provides scoped access to dependency injection primitives by creating and configuring commonly 
        /// described service types, and managing their lifecycles according to the plugin's lifecycle.
        /// </summary>
        /// <param name="plugin">The plugin instance associated with this dependency scope.</param>
        public readonly ref struct PluginDependencies(PluginBase plugin)
        {
            private readonly PluginBase _plugin = plugin;

            /// <summary>
            /// Creates and configures a new instance of the desired type with the specified configuration scope.
            /// </summary>
            /// <param name="serviceType">The service type to instantiate.</param>
            /// <param name="config">A configuration scope to pass directly to the new instance, or <see langword="null"/> if no configuration is required.</param>
            /// <returns>A new instance of the configured service.</returns>
            /// <exception cref="KeyNotFoundException">when the required configuration key is not found for the service type.</exception>
            /// <exception cref="ObjectDisposedException">when the plugin has been unloaded.</exception>
            /// <exception cref="EntryPointNotFoundException">when the service constructor cannot be resolved.</exception>
            /// <exception cref="NotSupportedException"></exception>
            /// <remarks>
            /// <para>If the type derives <see cref="IAsyncConfigurable"/>, the <see cref="IAsyncConfigurable.ConfigureServiceAsync"/> method is called once when the instance is loaded, and observed on the plugin scheduler.</para>
            /// <para>If the type derives <see cref="IAsyncBackgroundWork"/>, the <see cref="IAsyncBackgroundWork.DoWorkAsync(ILogProvider, CancellationToken)"/> method is called once when the instance is loaded, and observed on the plugin scheduler.</para>
            /// </remarks>
            public readonly object Create(Type serviceType, IConfigScope? config)
            {
                ArgumentNullException.ThrowIfNull(_plugin);
                ArgumentNullException.ThrowIfNull(serviceType);

                _plugin.ThrowIfUnloaded();

                //The requested service is not a class, so see if we can find a default implementation in assembly
                if (serviceType.IsAbstract || serviceType.IsInterface)
                {
                    throw new NotSupportedException("Requested service creation is an abstract type, you must specify a concrete implementation");
                }

                object service;

                try
                {
                    //Determine configuration requirements
                    if (PluginConfigStore.ConfigurationRequired(serviceType) && config == null)
                    {
                        PluginConfigStore.ThrowConfigNotFoundForType(serviceType);
                    }

                    service = InvokeServiceConstructor(serviceType, _plugin, config);
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
                    loading = _plugin.Tasks().ConfigureServiceAsync(asc, randomDelay);
                }

                //Allow background work loading
                if (service is IAsyncBackgroundWork bw)
                {

#pragma warning disable CA5394 // Do not use insecure randomness
                    int randomDelay = Random.Shared.Next(10, 200);
#pragma warning restore CA5394 // Do not use insecure randomness

                    PluginBase plugin = _plugin;

                    //If the instances supports async loading, don't start work until its loaded
                    _ = loading != null
                        ? loading.ContinueWith(t => plugin.Tasks().ObserveWork(bw, randomDelay), TaskScheduler.Default)
                        : _plugin.Tasks().ObserveWork(bw, randomDelay);
                }

                //register dispose cleanup
                if (service is IDisposable disp)
                {
                    _ = _plugin.Tasks().RegisterForUnload(disp.Dispose);
                }

                return service;
            }

            /// <summary>
            /// Creates and configures a new instance of the desired type, attempting to capture the 
            /// configuration information from the plugin configuration store from the type information.
            /// </summary>
            /// <inheritdoc cref="Create(Type, IConfigScope?)"/>
            public readonly object Create(Type serviceType)
            {
                return Create(
                    serviceType,
                    config: _plugin.Config().TryGetForType(serviceType)
                );
            }

            /// <summary>
            /// Creates and configures a new instance of the desired type with the specified configuration scope.
            /// </summary>
            /// <typeparam name="T">The service type to instantiate.</typeparam>
            /// <param name="config">A configuration scope to pass directly to the new instance, or <see langword="null"/> if no configuration is required.</param>
            /// <inheritdoc cref="Create(Type, IConfigScope?)"/>
            public readonly T Create<T>(IConfigScope? config) => (T)Create(typeof(T), config);

            /// <summary>
            /// Creates and configures a new instance of the desired type with the specified configuration property name.
            /// </summary>
            /// <param name="configName">A configuration element name to pass to the new instance.</param>
            /// <inheritdoc cref="Create{T}(IConfigScope?)"/>
            public readonly T Create<T>(string configName)
            {
                IConfigScope config = _plugin.Config().Get(configName);
                return Create<T>(config);
            }

            /// <summary>
            /// Creates and configures a new instance of the desired type, capturing the configuration 
            /// information from the plugin configuration store.
            /// </summary>
            /// <inheritdoc cref="Create{T}(IConfigScope?)"/>
            public readonly T Create<T>()
            {
                return Create<T>(
                    config: _plugin.Config().TryGetForType<T>()
                );
            }

            /// <summary>
            /// Gets a previously cached service singleton for the desired plugin, or creates a new one using the specified factory.
            /// </summary>
            /// <param name="serviceType">The service instance type.</param>
            /// <param name="serviceFactory">A factory method to produce the singleton when not yet cached.</param>
            /// <returns>An existing instance of a cache singleton, or the newly generated one.</returns>
            /// <inheritdoc cref="Create(Type)"/>
            public readonly object GetOrCreateSingleton(Type serviceType, Func<PluginBase, object> serviceFactory)
            {
                //Get local cache
                SingletonCache pc = _singletons.GetValue(_plugin, SingletonCache.Create);
                return pc.GetOrCreateService(serviceType, serviceFactory);
            }

            /// <typeparam name="T">The service type to get or create.</typeparam>
            /// <inheritdoc cref="GetOrCreateSingleton(Type, Func{PluginBase, object})"/>
            public readonly T GetOrCreateSingleton<T>(Func<PluginBase, T> serviceFactory)
                => (T)GetOrCreateSingleton(typeof(T), p => serviceFactory(p)!);

            /// <inheritdoc cref="GetOrCreateSingleton{T}(Func{PluginBase, T})"/>
            public readonly T GetOrCreateSingleton<T>()
            {
                PluginBase plugin = _plugin;

                T serviceFactory(PluginBase p) => new PluginDependencies(plugin).Create<T>();

                return GetOrCreateSingleton(serviceFactory);
            }

            /// <summary>
            /// Gets or initializes a singleton service of the desired type with the specified configuration name.
            /// </summary>
            /// <param name="configName">A configuration property name that overrides the default configuration lookup.</param>
            /// <inheritdoc cref="GetOrCreateSingleton{T}()"/>
            public readonly T GetOrCreateSingleton<T>(string configName)
            {
                PluginBase plugin = _plugin;

                T serviceFactory(PluginBase p) => new PluginDependencies(plugin).Create<T>(configName);

                return GetOrCreateSingleton(serviceFactory);
            }

            /// <summary>
            /// Publishes an existing instance of the desired discovery type to the internal singleton 
            /// cache for consumer use later in the program lifetime.
            /// </summary>
            /// <param name="type">The service type to declare the instance as</param>
            /// <param name="instance">The service object instance to cache</param>
            /// <returns>
            /// The current structure for fluent api chaining.
            /// </returns>
            /// <exception cref="ArgumentNullException"></exception>
            /// <remarks>
            /// NOTE! If an existing instance of the service type (or derived types) have already been added 
            /// to cache or created with <see cref="GetOrCreateSingleton{T}()"/> methods, this function silently
            /// ignores your request. Similarly, calls to <see cref="UseSingleton(Type, object)"/> will suppress 
            /// the creation of any object with a converging type created with <see cref="GetOrCreateSingleton{T}()"/>
            /// </remarks>
            public readonly PluginDependencies UseSingleton(Type type, object instance)
            {
                ArgumentNullException.ThrowIfNull(type);
                ArgumentNullException.ThrowIfNull(instance);

                SingletonCache cache = _singletons.GetValue(_plugin, SingletonCache.Create);

                _ = cache.GetOrCreateService(type, (_) => instance);

                return this;
            }

            /// <typeparam name="T">The service type to capture at compile time</typeparam>
            /// <inheritdoc cref="UseSingleton(Type, object)"/>
            public readonly PluginDependencies UseSingleton<T>(T instance) where T: class
                => UseSingleton(typeof(T), instance!);

            /// <summary>
            /// Attempts to retrieve an existing singleton service from the internal cache
            /// without creating a new instance. 
            /// </summary>
            /// <param name="type">The service type to recover from the cache</param>
            /// <returns>The existing service instance if found</returns>
            /// <exception cref="ArgumentNullException"></exception>
            public readonly object? TryGetSingleton(Type type)
            {
                ArgumentNullException.ThrowIfNull(type);

                SingletonCache cache = _singletons.GetValue(_plugin, SingletonCache.Create);
                return cache.TryGetService(type);
            }

            /// <inheritdoc cref="TryGetSingleton(Type)"/>
            public readonly T? TryGetSingleton<T>() where T : class
                => (T?)TryGetSingleton(typeof(T));

            /// <summary>
            /// Loads a managed assembly into the current plugin's load context that will unload when disposed
            /// or when the plugin is unloaded from the host application.
            /// </summary>
            /// <typeparam name="T">The desired exported type to load from the assembly.</typeparam>
            /// <param name="assemblyName">The name of the assembly (for example, 'file.dll') to search for.</param>
            /// <param name="dirSearchOption">A directory and file search option.</param>
            /// <param name="explicitAlc">
            /// An explicit <see cref="AssemblyLoadContext"/> to load the assembly and its dependencies into. 
            /// If <see langword="null"/>, uses the plugin's load context.
            /// </param>
            /// <returns>An <see cref="AssemblyLoader{T}"/> managing the loaded assembly in the current AppDomain.</returns>
            /// <exception cref="ArgumentNullException">when <paramref name="assemblyName"/> is <see langword="null"/>.</exception>
            /// <exception cref="FileNotFoundException">when the specified assembly file cannot be found.</exception>
            /// <remarks>
            /// The assembly is searched within the 'assets' directory specified in the plugin config
            /// or the global plugins ('path' key) directory if an assets directory is not defined.
            /// </remarks>
            public readonly AssemblyLoader<T> LoadAssembly<T>(
                string assemblyName,
                SearchOption dirSearchOption = SearchOption.AllDirectories,
                AssemblyLoadContext? explicitAlc = null
            )
            {
                //Get the file path for the assembly
                string asmFile = _plugin.Config().GetAssetFilePath(assemblyName, dirSearchOption)
                 ?? throw new FileNotFoundException($"Failed to find custom assembly {assemblyName} from plugin directory");

                //Get the plugin's load context if not explicitly supplied
                explicitAlc ??= GetPluginLoadContext();

                if (_plugin.IsDebug())
                {
                    _plugin.Log.Verbose("Loading assembly {asm}: from file {file}", assemblyName, asmFile);
                }

                //Load the assembly
                return AssemblyLoader<T>.Load(asmFile, explicitAlc, _plugin.UnloadToken);
            }

            /// <returns>A <see cref="ManagedLibrary"/> managing the loaded assembly in the current AppDomain.</returns>
            /// <inheritdoc cref="LoadAssembly{T}(string, SearchOption, AssemblyLoadContext?)"/>
            public readonly ManagedLibrary LoadAssembly(
                string assemblyName,
                SearchOption dirSearchOption = SearchOption.AllDirectories,
                AssemblyLoadContext? explicitAlc = null
            )
            {
                /*
                 * Using an assembly loader instance instead of managed library, so it respects 
                 * the plugin's unload events. Returning the managed library instance will
                 * hide the overloads that would cause possible type load issues, so using
                 * an object as the generic type parameter shouldn't be an issue.
                 */
                return LoadAssembly<object>(assemblyName, dirSearchOption, explicitAlc);
            }

            /// <summary>
            /// Creates a new instance of the desired service type from an external assembly and caches 
            /// the loaded assembly so it is never loaded more than once. Managed assembly lifecycles 
            /// are managed by the plugin. Instances are treated as services and their service hooks 
            /// will be called like any internal service.
            /// </summary>
            /// <typeparam name="T">The service type, which may be an interface or abstract type.</typeparam>
            /// <param name="assemblyDllName">The name of the assembly that contains the desired type to search for.</param>
            /// <param name="search">A directory search method.</param>
            /// <param name="defaultCtx">An <see cref="AssemblyLoadContext"/> to load the assembly into. Defaults to the plugin's current load context.</param>
            /// <returns>A new instance of the desired service type.</returns>
            /// <exception cref="TypeLoadException">when the desired external asset type is not exported from the assembly.</exception>
            public readonly T LoadExternal<T>(
                string assemblyDllName,
                SearchOption search = SearchOption.AllDirectories,
                AssemblyLoadContext? defaultCtx = null
            ) where T : class
            {
                /*
                 * Get or create the library for the assembly path, but only load it once
                 * Loading it on the plugin will also cause it to be cleaned up when the plugin 
                 * is unloaded.
                 */

                ManagedLibrary? manLib;

                 AssemblyCache asmCache = _assemblyCaches.GetOrCreateValue(_plugin);

                lock (asmCache)
                {
                    if (!asmCache.TryGetValue(assemblyDllName, out manLib))
                    {
                        manLib = LoadAssembly<T>(assemblyDllName, search, defaultCtx);

                        // Add to cache store
                        asmCache.Add(assemblyDllName, manLib);
                    }
                }

                Type[] matchingTypes = manLib.TryGetAllMatchingTypes<T>().ToArray();

                //try to get the first type that has the extern attribute, or fall back to the first public & concrete type
                Type? exported = matchingTypes.FirstOrDefault(t => t.GetCustomAttribute<ServiceExportAttribute>() != null)
                ?? matchingTypes.Where(t => !t.IsAbstract && t.IsPublic).FirstOrDefault();

                _ = exported ?? throw new TypeLoadException($"The desired external asset type {typeof(T).Name} is not exported as part of the assembly {manLib.Assembly.FullName}");

                // Create with default configuration search.
                return (T)Create(exported);
            }

            /// <summary>
            /// Gets the current plugin's <see cref="AssemblyLoadContext"/>.
            /// </summary>
            /// <returns>The <see cref="AssemblyLoadContext"/> for the current plugin.</returns>
            /// <exception cref="InvalidOperationException">when the plugin's assembly load context cannot be resolved.</exception>
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
            /// Gets a single concrete type implementation of the specified abstract type from the current assembly.
            /// </summary>
            /// <param name="abstractType">The abstract type to resolve a concrete implementation for.</param>
            /// <returns>The concrete type that implements the specified abstract type.</returns>
            /// <exception cref="ConcreteTypeNotFoundException">when no concrete implementation of the abstract type is found in the current assembly.</exception>
            /// <exception cref="ConcreteTypeAmbiguousMatchException">when multiple concrete implementations of the abstract type are found in the current assembly.</exception>
            [Obsolete("Abstract type loading is no longer supported")]
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
                        $"Failed to load implementation of abstract type {abstractType} because no concrete implementations were found in this assembly"
                    );
                }

                if (concreteTypes.Length > 1)
                {
                    throw new ConcreteTypeAmbiguousMatchException(
                        $"Failed to load implementation of abstract type {abstractType} because multiple concrete implementations were found in this assembly"
                    );
                }

                //Get the only concrete type
                return concreteTypes[0];
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
                // Recursively search for the innermost exception of a TIE
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
        }

        private sealed class SingletonCache
        {
            private readonly PluginBase _plugin;
            private readonly Dictionary<Type, Lazy<object>> _store;

            private SingletonCache(PluginBase plugin)
            {
                _plugin = plugin;
                _store = [];

                //Register cleanup on unload
                _ = _plugin
                    .Tasks()
                    .RegisterForUnload(_store.Clear);
            }
           
            private Lazy<object>? TryGetLazyServiceInternal(Type serviceType)
            {
                return _store
                       .Where(t => t.Key.IsAssignableTo(serviceType))
                       .Select(static tk => tk.Value)
                       .FirstOrDefault();
            }

            public object? TryGetService(Type serviceType)
            {
                Lazy<object>? lazyService;

                lock (_store)
                {
                    lazyService = TryGetLazyServiceInternal(serviceType);
                }

                //Return the service instance
                return lazyService?.Value;
            }

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
                    lazyService = TryGetLazyServiceInternal(serviceType);

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

            public static SingletonCache Create(PluginBase plugin) => new(plugin);
        }
    }
}
