/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: UserManager.cs 
*
* UserManager.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Users;

namespace VNLib.Plugins.Extensions.Loading.Users
{
    /// <summary>
    /// Provides a singleton <see cref="IUserManager"/> service that dynamically loads 
    /// a user manager for the plugin.
    /// </summary>
    [ConfigurationName("users", Required = false)]
    public class UserManager : IUserManager
    {
        public const string USER_CUSTOM_ASSEMBLY = "custom_assembly";
        public const string DEFAULT_USER_ASM = "VNLib.Plugins.Essentials.Users.dll";
        public const string ONLOAD_METHOD_NAME = "OnPluginLoading";

        private readonly IUserManager _dynamicLoader;

        public UserManager(PluginBase plugin)
        {
            _dynamicLoader = LoadUserAssembly(plugin, DEFAULT_USER_ASM);
        }

        public UserManager(PluginBase plugin, IConfigScope config)
        {
            //Get the service configuration
            string customAsm = config[USER_CUSTOM_ASSEMBLY].GetString() ?? DEFAULT_USER_ASM;
            //Load the assembly
            _dynamicLoader = LoadUserAssembly(plugin, customAsm);
        }

        private static IUserManager LoadUserAssembly(PluginBase plugin, string customAsm)
        {
            //Try to load a custom assembly
            AssemblyLoader<IUserManager> loader = plugin.LoadAssembly<IUserManager>(customAsm);
            try
            {
                //Try to get the onload method
                Action<object>? onLoadMethod = loader.TryGetMethod<Action<object>>(ONLOAD_METHOD_NAME);

                //Call the onplugin load method
                onLoadMethod?.Invoke(plugin);

                if (plugin.IsDebug())
                {
                    plugin.Log.Debug("Loading user manager from assembly {name}", loader.Resource.GetType().AssemblyQualifiedName);
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

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<IUser> CreateUserAsync(string userid, string emailAddress, ulong privilages, PrivateString passHash, CancellationToken cancellation = default)
        {
            return _dynamicLoader.CreateUserAsync(userid, emailAddress, privilages, passHash, cancellation);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<IUser?> GetUserAndPassFromEmailAsync(string emailAddress, CancellationToken cancellationToken = default)
        {
            return _dynamicLoader.GetUserAndPassFromEmailAsync(emailAddress, cancellationToken);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<IUser?> GetUserAndPassFromIDAsync(string userid, CancellationToken cancellation = default)
        {
            return _dynamicLoader.GetUserAndPassFromIDAsync(userid, cancellation);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<long> GetUserCountAsync(CancellationToken cancellation = default)
        {
            return _dynamicLoader.GetUserCountAsync(cancellation);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<IUser?> GetUserFromEmailAsync(string emailAddress, CancellationToken cancellationToken = default)
        {
            return _dynamicLoader.GetUserFromEmailAsync(emailAddress, cancellationToken);
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<IUser?> GetUserFromIDAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _dynamicLoader.GetUserFromIDAsync(userId, cancellationToken);
        }
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<ERRNO> UpdatePassAsync(IUser user, PrivateString newPass, CancellationToken cancellation = default)
        {
            return _dynamicLoader.UpdatePassAsync(user, newPass, cancellation);
        }
    }
}