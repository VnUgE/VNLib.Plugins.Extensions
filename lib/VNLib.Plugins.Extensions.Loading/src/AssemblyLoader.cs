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
using System.Threading;
using System.Reflection;
using System.Runtime.Loader;

using VNLib.Utils.IO;
using VNLib.Utils.Resources;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// <para>
    /// Represents a disposable assembly loader wrapper for 
    /// exporting a single type from a loaded assembly
    /// </para>
    /// <para>
    /// If the loaded type implements <see cref="IDisposable"/> the 
    /// dispose method is called when the loader is disposed
    /// </para>
    /// </summary>
    /// <typeparam name="T">The exported type to manage</typeparam>
    public sealed class AssemblyLoader<T> : ManagedLibrary, IDisposable
    {
        private readonly CancellationTokenRegistration _reg;
        private readonly Lazy<T> _instance;
        private bool disposedValue;

        /// <summary>
        /// The instance of the loaded type
        /// </summary>
        public T Resource => _instance.Value;

        private AssemblyLoader(string assemblyPath, AssemblyLoadContext parentContext, CancellationToken unloadToken)
            :base(assemblyPath, parentContext)
        {
            //Init lazy type loader
            _instance = new(LoadTypeFromAssembly<T>, LazyThreadSafetyMode.PublicationOnly);            
            //Register dispose
            _reg = unloadToken.Register(Dispose);
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
            return Resource.GetType()
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.CreateDelegate<TDelegate>(Resource);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                //Call base unload during dispose (or finalize)
                OnUnload();

                //Always cleanup registration
                _reg.Dispose();

                if (disposing)
                {
                    //If the instance is disposable, call its dispose method on unload
                    if (_instance.IsValueCreated && _instance.Value is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
               
                disposedValue = true;
            }
        }

        /// <summary>
        /// Cleans up any unused internals
        /// </summary>
        ~AssemblyLoader()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

       
        /// <summary>
        /// Disposes the assembly loader and cleans up resources. If the <typeparamref name="T"/> 
        /// inherits <see cref="IDisposable"/> the intrance is disposed.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new loader for the desired assembly. The assembly and its dependencies
        /// will be loaded into the specified context. If no context is specified the current assemblie's load
        /// context is captured.
        /// </summary>
        /// <param name="assemblyName">The name of the assmbly within the current plugin directory</param>
        /// <param name="unloadToken">The plugin unload token</param>
        /// <param name="loadContext">The assembly load context to load the assmbly into</param>
        /// <exception cref="FileNotFoundException"></exception>
        internal static AssemblyLoader<T> Load(string assemblyName, AssemblyLoadContext loadContext, CancellationToken unloadToken)
        {
            _ = loadContext ?? throw new ArgumentNullException(nameof(loadContext));

            //Make sure the file exists
            if (!FileOperations.FileExists(assemblyName))
            {
                throw new FileNotFoundException($"The desired assembly {assemblyName} could not be found at the file path");
            }

            //Create the loader from its absolute file path
            FileInfo fi = new(assemblyName);
            return new(fi.FullName, loadContext, unloadToken);
        }
    }
}
