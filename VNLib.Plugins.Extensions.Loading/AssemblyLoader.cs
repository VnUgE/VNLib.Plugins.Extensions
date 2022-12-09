/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: AssemblyLoader.cs 
*
* AssemblyLoader.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Extensions.Loading. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using System.Threading;
using System.Reflection;

using McMaster.NETCore.Plugins;
using System.Runtime.Loader;
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
    public class AssemblyLoader<T> : OpenResourceHandle<T>
    {
        private readonly PluginLoader _loader;
        private readonly Type _typeInfo;
        private readonly CancellationTokenRegistration _reg;
        private readonly Lazy<T> _instance;

        /// <summary>
        /// The instance of the loaded type
        /// </summary>
        public override T Resource => _instance.Value;

        private AssemblyLoader(PluginLoader loader, in CancellationToken unloadToken)
        {
            _typeInfo = typeof(T);
            _loader = loader;
            //Init lazy loader
            _instance = new(LoadAndGetExportedType, LazyThreadSafetyMode.PublicationOnly);
            //Register dispose
            _reg = unloadToken.Register(Dispose);
        }
        /// <summary>
        /// Loads the default assembly and gets the expected export type,
        /// creates a new instance, and calls its parameterless constructor
        /// </summary>
        /// <returns>The desired type instance</returns>
        /// <exception cref="EntryPointNotFoundException"></exception>
        private T LoadAndGetExportedType()
        {
            //Load the assembly
            Assembly asm = _loader.LoadDefaultAssembly();
          
            //See if the type is exported
            Type exp = (from type in asm.GetExportedTypes()
                        where _typeInfo.IsAssignableFrom(type)
                        select type)
                        .FirstOrDefault()
                        ?? throw new EntryPointNotFoundException($"Imported assembly does not export type {_typeInfo.FullName}");
            //Create instance
            return (T)Activator.CreateInstance(exp)!;
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            //If the instance is disposable, call its dispose method on unload
            if (_instance.IsValueCreated && _instance.Value is IDisposable)
            {
                (_instance.Value as IDisposable)?.Dispose();
            }
            _loader.Dispose();
            _reg.Dispose();
        }

        /// <summary>
        /// Creates a new assembly loader for the specified type and 
        /// </summary>
        /// <param name="assemblyName">The name of the assmbly within the current plugin directory</param>
        /// <param name="unloadToken">The plugin unload token</param>
        internal static AssemblyLoader<T> Load(string assemblyName, CancellationToken unloadToken)
        {
            PluginConfig conf = new(assemblyName)
            {
                IsUnloadable = true,
                EnableHotReload = false,
                PreferSharedTypes = true,
                DefaultContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()) ?? AssemblyLoadContext.Default
            };
            
            PluginLoader loader = new(conf);
            
            //Add the assembly the type originaged from
            conf.SharedAssemblies.Add(typeof(T).Assembly.GetName());

            return new(loader, in unloadToken);
        }
    }
}
