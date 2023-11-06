﻿/*
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

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Users;
using VNLib.Plugins.Essentials.Accounts;

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

        private readonly IUserManager _dynamicLoader;

        public UserManager(PluginBase plugin)
        {
            //Load the default user assembly
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
            IUserManager externManager = plugin.CreateServiceExternal<IUserManager>(customAsm);    

            if (plugin.IsDebug())
            {
                plugin.Log.Debug("Loading user manager from assembly {name}", externManager.GetType().AssemblyQualifiedName);
            }

            return externManager;
        }

        /// <summary>
        /// Gets the underlying <see cref="IUserManager"/> that was dynamically loaded.
        /// </summary>
        /// <returns>The user manager instance</returns>
        public IUserManager InternalManager => _dynamicLoader;

        ///<inheritdoc/>
        public Task<IUser> CreateUserAsync(IUserCreationRequest creation, string? userId, CancellationToken cancellation = default)
        {
            return _dynamicLoader.CreateUserAsync(creation, userId, cancellation);
        }

        ///<inheritdoc/>
        public IPasswordHashingProvider? GetHashProvider()
        {
            return _dynamicLoader.GetHashProvider();
        }

        ///<inheritdoc/>
        public Task<long> GetUserCountAsync(CancellationToken cancellation = default)
        {
            return _dynamicLoader.GetUserCountAsync(cancellation);
        }

        ///<inheritdoc/>
        public Task<IUser?> GetUserFromEmailAsync(string emailAddress, CancellationToken cancellationToken = default)
        {
            return _dynamicLoader.GetUserFromEmailAsync(emailAddress, cancellationToken);
        }

        ///<inheritdoc/>
        public Task<IUser?> GetUserFromIDAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _dynamicLoader.GetUserFromIDAsync(userId, cancellationToken);
        }

        ///<inheritdoc/>
        public Task<PrivateString?> RecoverPasswordAsync(IUser user, CancellationToken cancellation = default)
        {
            return _dynamicLoader.RecoverPasswordAsync(user, cancellation);
        }

        ///<inheritdoc/>
        public Task<ERRNO> UpdatePasswordAsync(IUser user, PrivateString newPass, CancellationToken cancellation = default)
        {
            return _dynamicLoader.UpdatePasswordAsync(user, newPass, cancellation);
        }

        ///<inheritdoc/>
        public Task<ERRNO> ValidatePasswordAsync(IUser user, PrivateString password, PassValidateFlags flags, CancellationToken cancellation = default)
        {
            return _dynamicLoader.ValidatePasswordAsync(user, password, flags, cancellation);
        }

        ///<inheritdoc/>
        public string ComputeSafeUserId(string input)
        {
            return _dynamicLoader.ComputeSafeUserId(input);
        }
    }
}