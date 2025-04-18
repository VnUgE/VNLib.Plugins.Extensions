﻿/*
* Copyright (c) 2025 Vaughn Nugent
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

        private UserManager(PluginBase plugin, string asmPath)
        {
            //Load the assembly
            InternalManager = LoadUserAssembly(plugin, asmPath);
        }

        public UserManager(PluginBase plugin)
            : this(plugin, DEFAULT_USER_ASM)
        { }

        public UserManager(PluginBase plugin, IConfigScope config)
            : this(
                 plugin,
                 //Get custom assembly, or default
                 asmPath: config.GetValueOrDefault(USER_CUSTOM_ASSEMBLY, DEFAULT_USER_ASM) ?? DEFAULT_USER_ASM 
            )
        { }

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
        public IUserManager InternalManager { get; }

        ///<inheritdoc/>
        public IPasswordHashingProvider? GetHashProvider()
        {
            return InternalManager.GetHashProvider();
        }

        ///<inheritdoc/>
        public Task<long> GetUserCountAsync(CancellationToken cancellation = default)
        {
            return InternalManager.GetUserCountAsync(cancellation);
        }

        ///<inheritdoc/>
        public Task<IUser?> GetUserFromUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            return InternalManager.GetUserFromUsernameAsync(username, cancellationToken);
        }

        ///<inheritdoc/>
        public Task<IUser?> GetUserFromIDAsync(string userId, CancellationToken cancellationToken = default)
        {
            return InternalManager.GetUserFromIDAsync(userId, cancellationToken);
        }

        ///<inheritdoc/>
        public Task<PrivateString?> RecoverPasswordAsync(IUser user, CancellationToken cancellation = default)
        {
            return InternalManager.RecoverPasswordAsync(user, cancellation);
        }

        ///<inheritdoc/>
        public string ComputeSafeUserId(string input)
        {
            return InternalManager.ComputeSafeUserId(input);
        }

        ///<inheritdoc/>
        public Task<IUser> CreateUserAsync(IUserCreationRequest creation, string? userId, IPasswordHashingProvider? hashProvider, CancellationToken cancellation = default)
        {
            return InternalManager.CreateUserAsync(creation, userId, hashProvider, cancellation);
        }

        ///<inheritdoc/>
        public Task<ERRNO> ValidatePasswordAsync(IUser user, PrivateString password, IPasswordHashingProvider? hashProvider, CancellationToken cancellation = default)
        {
            return InternalManager.ValidatePasswordAsync(user, password, hashProvider, cancellation);
        }

        ///<inheritdoc/>
        public Task<ERRNO> UpdatePasswordAsync(IUser user, PrivateString newPass, IPasswordHashingProvider? hashProvider, CancellationToken cancellation = default)
        {
            return InternalManager.UpdatePasswordAsync(user, newPass, hashProvider, cancellation);
        }
    }
}