/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ManagedPasswordHashing.cs 
*
* ManagedPasswordHashing.cs is part of VNLib.Plugins.Extensions.Loading which 
* is part of the larger VNLib collection of libraries and utilities.
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
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Accounts;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// A plugin configurable <see cref="IPasswordHashingProvider"/> managed implementation. Users may load custom 
    /// assemblies backing instances of this class or configure the <see cref="PasswordHashing"/> implementation
    /// </summary>
    [ConfigurationName(LoadingExtensions.PASSWORD_HASHING_KEY, Required = false)]
    public sealed class ManagedPasswordHashing : IPasswordHashingProvider
    {
        public ManagedPasswordHashing(PluginBase plugin, IConfigScope config)
        {
            //Check for custom hashing assembly
            if (config.TryGetValue(LoadingExtensions.CUSTOM_PASSWORD_ASM_KEY, out JsonElement el))
            {
                string customAsm = el.GetString() ?? throw new KeyNotFoundException("You must specify a string file path for your custom password hashing assembly");

                //Load the custom assembly
                AssemblyLoader<IPasswordHashingProvider> prov = plugin.LoadAssembly<IPasswordHashingProvider>(customAsm);

                //Configure async
                if (prov.Resource is IAsyncConfigurable ac)
                {
                    //Configure async
                    _ = plugin.ConfigureServiceAsync(ac);
                }

                //Store
                Passwords = new CustomPasswordHashingAsm(prov);
            }
            else
            {
                Passwords = plugin.GetOrCreateSingleton<SecretProvider>().Passwords;
            }
        }

        public ManagedPasswordHashing(PluginBase plugin)
        {
            //Only configure a default password impl
            Passwords = plugin.GetOrCreateSingleton<SecretProvider>().Passwords;
        }

        /// <summary>
        /// The underlying <see cref="IPasswordHashingProvider"/>
        /// </summary>
        public IPasswordHashingProvider Passwords { get; }

        ///<inheritdoc/>
        public bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password) => Passwords.Verify(passHash, password);

        ///<inheritdoc/>
        public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password) => Passwords.Verify(passHash, password);

        ///<inheritdoc/>
        public PrivateString Hash(ReadOnlySpan<char> password) => Passwords.Hash(password);

        ///<inheritdoc/>
        public PrivateString Hash(ReadOnlySpan<byte> password) => Passwords.Hash(password);

        ///<inheritdoc/>
        public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput) => Passwords.Hash(password, hashOutput);

        sealed class CustomPasswordHashingAsm : IPasswordHashingProvider
        {
            private readonly AssemblyLoader<IPasswordHashingProvider> _loader;

            public CustomPasswordHashingAsm(AssemblyLoader<IPasswordHashingProvider> loader)
            {
                _loader = loader;
            }

            /*
             * Password hashing isnt a super high performance system
             * so adding method overhead shouldnt be a large issue for the 
             * asm wrapper providing unload protection
             */

            public PrivateString Hash(ReadOnlySpan<char> password) => _loader.Resource.Hash(password);

            public PrivateString Hash(ReadOnlySpan<byte> password) => _loader.Resource.Hash(password);

            public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput) => _loader.Resource.Hash(password, hashOutput);

            public bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password) => _loader.Resource.Verify(passHash, password);

            public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password) => _loader.Resource.Verify(passHash, password);
        }

        private sealed class SecretProvider : VnDisposeable, ISecretProvider
        {
            private readonly IAsyncLazy<byte[]> _pepper;

            public SecretProvider(PluginBase plugin, IConfigScope config)
            {
                if (config.TryGetValue("args", out JsonElement el))
                {
                    //Convert to dict
                    IReadOnlyDictionary<string, JsonElement> hashingArgs = el.EnumerateObject().ToDictionary(static k => k.Name, static v => v.Value);

                    //Get hashing arguments
                    uint saltLen = hashingArgs["salt_len"].GetUInt32();
                    uint hashLen = hashingArgs["hash_len"].GetUInt32();
                    uint timeCost = hashingArgs["time_cost"].GetUInt32();
                    uint memoryCost = hashingArgs["memory_cost"].GetUInt32();
                    uint parallelism = hashingArgs["parallelism"].GetUInt32();
                    //Load passwords
                    Passwords = new(this, (int)saltLen, timeCost, memoryCost, parallelism, hashLen);
                }
                else
                {
                    Passwords = new(this);
                }

                //Get the pepper from secret storage
                _pepper = plugin.GetSecretAsync(LoadingExtensions.PASSWORD_HASHING_KEY)
                    .ToLazy(static sr => sr.GetFromBase64());
            }

            public SecretProvider(PluginBase plugin)
            {
                Passwords = new(this);

                //Get the pepper from secret storage
                _pepper = plugin.GetSecretAsync(LoadingExtensions.PASSWORD_HASHING_KEY)
                    .ToLazy(static sr => sr.GetFromBase64());
            }


            public PasswordHashing Passwords { get; }

            ///<inheritdoc/>
            public int BufferSize
            {
                get
                {
                    Check();
                    return _pepper.Value.Length;
                }
            }

            public ERRNO GetSecret(Span<byte> buffer)
            {
                Check();
                //Coppy pepper to buffer
                _pepper.Value.CopyTo(buffer);
                //Return pepper length
                return _pepper.Value.Length;
            }

            protected override void Check()
            {
                base.Check();
                _ = _pepper.Value;
            }

            protected override void Free()
            {
                //Clear the pepper if set
                MemoryUtil.InitializeBlock(_pepper.Value.AsSpan());
            }
        }
    }
}
