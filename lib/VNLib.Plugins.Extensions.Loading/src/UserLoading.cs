/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: UserLoading.cs 
*
* UserLoading.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Users;

namespace VNLib.Plugins.Extensions.Loading.Users
{
    /// <summary>
    /// Contains extension methods for plugins to load the "users" system
    /// </summary>
    public static class UserLoading
    {
        public const string USER_CUSTOM_ASSEMBLY = "user_custom_asm";
        public const string DEFAULT_USER_ASM = "VNLib.Plugins.Essentials.Users.dll";
        public const string ONLOAD_METHOD_NAME = "OnPluginLoading";
       

        /// <summary>
        /// Gets or loads the plugin's ambient <see cref="IUserManager"/>, with the specified user-table name,
        /// or the default table name
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="IUserManager"/> for the current plugin</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IUserManager GetUserManager(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Get stored or load
            return LoadingExtensions.GetOrCreateSingleton(plugin, LoadUsers);
        }

        private static IUserManager LoadUsers(PluginBase pbase)
        {
            //Try to load a custom user assembly for exporting IUserManager
            string? customAsm = pbase.PluginConfig.GetPropString(USER_CUSTOM_ASSEMBLY);
            //See if host config defined the path
            customAsm ??= pbase.HostConfig.GetPropString(USER_CUSTOM_ASSEMBLY);
            //Finally default
            customAsm ??= DEFAULT_USER_ASM;

            //Try to load a custom assembly
            AssemblyLoader<IUserManager> loader = pbase.LoadAssembly<IUserManager>(customAsm);
            try
            {
                //Try to get the onload method
                Action<object>? onLoadMethod = loader.TryGetMethod<Action<object>>(ONLOAD_METHOD_NAME);

                //Call the onplugin load method
                onLoadMethod?.Invoke(pbase);

                if (pbase.IsDebug())
                {
                    pbase.Log.Verbose("Loading user manager from assembly {name}", loader.Resource.GetType().AssemblyQualifiedName);
                }

                //Return the loaded instance (may raise exception)
                return loader.Resource;
            }
            catch
            {
                loader.Dispose();
                throw;
            }
        }
    }
}