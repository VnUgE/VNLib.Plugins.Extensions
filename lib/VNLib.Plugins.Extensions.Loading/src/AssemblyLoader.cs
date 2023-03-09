/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: AssemblyLoader.cs 
*
* AssemblyLoader.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Runtime.InteropServices;

using VNLib.Utils.IO;
using VNLib.Utils.Resources;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// <para>
    /// Represents a disposable assembly loader wrapper for 
    /// exporting a signle type from a loaded assembly
    /// </para>
    /// <para>
    /// If the loaded type implements <see cref="IDisposable"/> the 
    /// dispose method is called when the loader is disposed
    /// </para>
    /// </summary>
    /// <typeparam name="T">The exported type to manage</typeparam>
    public sealed class AssemblyLoader<T> : OpenResourceHandle<T>
    {
        private readonly CancellationTokenRegistration _reg;
        private readonly Lazy<T> _instance;
        private readonly AssemblyLoadContext _loadContext;
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _assemblyPath;

        /// <summary>
        /// The instance of the loaded type
        /// </summary>
        public override T Resource => _instance.Value;

        private AssemblyLoader(string assemblyPath, AssemblyLoadContext parentContext, CancellationToken unloadToken)
        {
            _loadContext = parentContext;
            _resolver = new(assemblyPath);
            _assemblyPath = assemblyPath;

            //Add resolver for context
            parentContext.Resolving += OnDependencyResolving;
            parentContext.ResolvingUnmanagedDll += OnNativeLibraryResolving;

            //Init lazy loader
            _instance = new(LoadAndGetExportedType, LazyThreadSafetyMode.PublicationOnly);
            //Register dispose
            _reg = unloadToken.Register(Dispose);
        }

        /*
         * Resolves a native libary isolated to the requested assembly, which 
         * should be isolated to this assembly or one of its dependencies.
         */
        private IntPtr OnNativeLibraryResolving(Assembly assembly, string libname)
        {
            //Resolve the desired asm dependency for the current context
            string? requestedDll = _resolver.ResolveUnmanagedDllToPath(libname);

            //if the dep is resolved, seach in the assembly directory for the manageed dll only
            return requestedDll == null ? IntPtr.Zero : NativeLibrary.Load(requestedDll, assembly, DllImportSearchPath.AssemblyDirectory);
        }

        private Assembly? OnDependencyResolving(AssemblyLoadContext context, AssemblyName asmName)
        {
            //Resolve the desired asm dependency for the current context
            string? desiredAsm = _resolver.ResolveAssemblyToPath(asmName);

            //If the asm exists in the dir, load it
            return desiredAsm == null ? null : _loadContext.LoadFromAssemblyPath(desiredAsm);
        }

        /// <summary>
        /// Loads the default assembly and gets the expected export type,
        /// creates a new instance, and calls its parameterless constructor
        /// </summary>
        /// <returns>The desired type instance</returns>
        /// <exception cref="EntryPointNotFoundException"></exception>
        private T LoadAndGetExportedType()
        {
            //Load the assembly into the parent context
            Assembly asm = _loadContext.LoadFromAssemblyPath(_assemblyPath);

            Type resourceType = typeof(T);

            //See if the type is exported
            Type exp = (from type in asm.GetExportedTypes()
                        where resourceType.IsAssignableFrom(type)
                        select type)
                        .FirstOrDefault()
                        ?? throw new EntryPointNotFoundException($"Imported assembly does not export desired type {resourceType.FullName}");
            //Create instance
            return (T)Activator.CreateInstance(exp)!;
        }

        /// <summary>
        /// Creates a method delegate for the given method name from
        /// the instance wrapped by the current loader
        /// </summary>
        /// <typeparam name="TDelegate"></typeparam>
        /// <param name="methodName">The name of the method to recover</param>
        /// <returns>The delegate method wrapper if found, null otherwise</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AmbiguousMatchException"></exception>
        public TDelegate? TryGetMethod<TDelegate>(string methodName) where TDelegate : Delegate
        {
            //get the type info of the actual resource
            return Resource!.GetType()
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.CreateDelegate<TDelegate>(Resource);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //Remove resolving event handlers
            _loadContext.Resolving -= OnDependencyResolving;
            _loadContext.ResolvingUnmanagedDll -= OnNativeLibraryResolving;

            //If the instance is disposable, call its dispose method on unload
            if (_instance.IsValueCreated && _instance.Value is IDisposable)
            {
                (_instance.Value as IDisposable)?.Dispose();
            }
            _reg.Dispose();
        }

        /// <summary>
        /// Creates a new loader for the desired assembly. The assembly and its dependencies
        /// will be loaded into the parent context, meaning all loaded types belong to the 
        /// current <see cref="AssemblyLoadContext"/> which belongs the current plugin instance.
        /// </summary>
        /// <param name="assemblyName">The name of the assmbly within the current plugin directory</param>
        /// <param name="unloadToken">The plugin unload token</param>
        /// <exception cref="FileNotFoundException"></exception>
        internal static AssemblyLoader<T> Load(string assemblyName, CancellationToken unloadToken)
        {
            //Make sure the file exists
            if (!FileOperations.FileExists(assemblyName))
            {
                throw new FileNotFoundException($"The desired assembly {assemblyName} could not be found at the file path");
            }

            /*
             * Dynamic assemblies are loaded directly to the exe assembly context.
             * This should always be the plugin isolated context.
             */
            
            Assembly executingAsm = Assembly.GetExecutingAssembly();
            AssemblyLoadContext currentCtx = AssemblyLoadContext.GetLoadContext(executingAsm) ?? throw new InvalidOperationException("Could not get default assembly load context");

            return new(assemblyName, currentCtx, unloadToken);
        }
      
    }
}
