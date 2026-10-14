/*
* Copyright (c) 2026 Vaughn Nugent
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
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

using VNLib.Utils.IO;
using VNLib.Utils.Resources;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// Represents a disposable assembly loader wrapper for exporting a single type from a loaded assembly.
    /// </summary>
    /// <typeparam name="T">The exported type to manage.</typeparam>
    /// <remarks>
    /// If the loaded type implements <see cref="IDisposable"/>, the dispose method is called when the loader is disposed.
    /// </remarks>
    public sealed class AssemblyLoader<T> : ManagedLibrary, IDisposable
    {
        private readonly CancellationTokenRegistration _reg;
        private readonly LazyInitializer<T> _instance;
        private bool disposedValue;

        /// <summary>
        /// Gets the instance of the loaded type.
        /// </summary>
        public T Resource => _instance.Instance;

        private AssemblyLoader(string assemblyPath, AssemblyLoadContext parentContext, CancellationToken unloadToken)
            :base(assemblyPath, parentContext)
        {
            //Init lazy type loader
            _instance = new(LoadTypeFromAssembly<T>);
            //Register dispose
            _reg = unloadToken.Register(Dispose);
        }
      

        /// <summary>
        /// Creates a method delegate for the specified method name from the instance wrapped by the current loader.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type to create.</typeparam>
        /// <param name="methodName">The name of the method to recover.</param>
        /// <returns>The delegate method wrapper if found; otherwise, <see langword="null"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="methodName"/> is <see langword="null"/>.</exception>
        /// <exception cref="AmbiguousMatchException">More than one matching method is found.</exception>
        public TDelegate? TryGetMethod<TDelegate>(string methodName) where TDelegate : Delegate
        {
            T resource = Resource!; // Guaranteed not null after instance is loaded

            //get the type info of the actual resource
            return resource.GetType()
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.CreateDelegate<TDelegate>(resource);
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
                    if (_instance.IsLoaded && _instance.Instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
               
                disposedValue = true;
            }
        }

        /// <summary>
        /// Cleans up any unused internals.
        /// </summary>
        ~AssemblyLoader()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

       
        /// <summary>
        /// Disposes the assembly loader and cleans up resources.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> implements <see cref="IDisposable"/>, the instance is disposed.
        /// </remarks>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new loader for the desired assembly.
        /// </summary>
        /// <remarks>
        /// The assembly and its dependencies are loaded into the specified context.
        /// If no context is specified, the current assembly's load context is captured.
        /// </remarks>
        /// <param name="assemblyName">The name of the assembly within the current plugin directory.</param>
        /// <param name="unloadToken">A plugin unload token.</param>
        /// <param name="loadContext">The assembly load context to load the assembly into.</param>
        /// <exception cref="FileNotFoundException">The specified assembly file cannot be found.</exception>
        internal static AssemblyLoader<T> Load(string assemblyName, AssemblyLoadContext loadContext, CancellationToken unloadToken)
        {
            ArgumentNullException.ThrowIfNull(loadContext);

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

