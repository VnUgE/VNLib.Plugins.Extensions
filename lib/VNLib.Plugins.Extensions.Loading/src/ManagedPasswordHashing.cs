/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
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
                Passwords = new CustomPasswordHashingAsm(
                    plugin.CreateServiceExternal<IPasswordHashingProvider>(customAsm)
                );

                plugin.Log.Verbose("Loading custom password hashing assembly: {path}", customAsm);
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
        public bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password) 
            => Passwords.Verify(passHash, password);

        ///<inheritdoc/>
        public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password) 
            => Passwords.Verify(passHash, password);

        ///<inheritdoc/>
        public PrivateString Hash(ReadOnlySpan<char> password) 
            => Passwords.Hash(password);

        ///<inheritdoc/>
        public PrivateString Hash(ReadOnlySpan<byte> password) 
            => Passwords.Hash(password);

        ///<inheritdoc/>
        public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput) 
            => Passwords.Hash(password, hashOutput);

        sealed class CustomPasswordHashingAsm(IPasswordHashingProvider loader) : IPasswordHashingProvider
        {
            private readonly IPasswordHashingProvider _provider = loader;

            /*
             * Password hashing isnt a super high performance system
             * so adding method overhead shouldnt be a large issue for the 
             * asm wrapper providing unload protection
             */

            public PrivateString Hash(ReadOnlySpan<char> password) 
                => _provider.Hash(password);

            public PrivateString Hash(ReadOnlySpan<byte> password) 
                => _provider.Hash(password);

            public ERRNO Hash(ReadOnlySpan<byte> password, Span<byte> hashOutput) 
                => _provider.Hash(password, hashOutput);

            public bool Verify(ReadOnlySpan<char> passHash, ReadOnlySpan<char> password) 
                => _provider.Verify(passHash, password);

            public bool Verify(ReadOnlySpan<byte> passHash, ReadOnlySpan<byte> password) 
                => _provider.Verify(passHash, password);
        }

      
        private sealed class SecretProvider : VnDisposeable, ISecretProvider
        {
            private readonly IAsyncLazy<byte[]> _pepper;

            public PasswordHashing Passwords { get; }

            public SecretProvider(PluginBase plugin, IConfigScope config)
            {                
                Argon2ConfigParams costParams = new();

                if (config.TryGetValue("args", out JsonElement el) && el.ValueKind == JsonValueKind.Object)
                {
                    Argon2Arguments userArgs = el.Deserialize<Argon2Arguments>()!;

                    costParams = new()
                    {
                        HashLen         = userArgs.HashLen,
                        MemoryCost      = userArgs.MemoryCost,
                        Parallelism     = userArgs.Parallelism,
                        SaltLen         = (int)userArgs.SaltLen,
                        TimeCost        = userArgs.TimeCost
                    };
                }

                if(
                    config.TryGetValue("lib_path", out JsonElement manualLibPath) 
                    && manualLibPath.ValueKind == JsonValueKind.String
                )
                {
                    SafeArgon2Library lib = VnArgon2.LoadCustomLibrary(
                        dllPath:manualLibPath.GetString()!, 
                        System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories
                    );
                    
                    //Dynamically loaded lib must be disposed manually
                    _ = plugin.RegisterForUnload(lib.Dispose);

                    //Create passwords with the configuration and library
                    Passwords = PasswordHashing.Create(lib, this, in costParams);

                    plugin.Log.Verbose("Loaded custom password hashing library: {path}", manualLibPath.GetString());
                }
                else
                {
                    //Load default library if the user did not explictly specify one
                    Passwords = PasswordHashing.Create(this, in costParams);
                }

                //Get the pepper from secret storage
                _pepper = plugin.Secrets()
                    .GetSecretAsync(LoadingExtensions.PASSWORD_HASHING_KEY)
                    .ToLazy(static sr => sr.GetFromBase64());

                _ = _pepper.AsTask()
                    .ContinueWith(secT => {
                            plugin.Log.Error("Failed to load password pepper: {reason}", secT.Exception?.Message);
                        }, 
                        cancellationToken: default, 
                        TaskContinuationOptions.OnlyOnFaulted,  //Only run if an exception occured to notify the user during startup
                        TaskScheduler.Default
                    );
            }

            public SecretProvider(PluginBase plugin)
            {
                //Load passwords with default config
                Passwords = PasswordHashing.Create(this, new Argon2ConfigParams());

                //Get the pepper from secret storage
                _pepper = plugin.Secrets()
                    .GetSecretAsync(LoadingExtensions.PASSWORD_HASHING_KEY)
                    .ToBase64Bytes()
                    .AsLazy();
            }

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

            protected override void Free()
            {
                Task pepperTask = _pepper.AsTask();
                //Only zero pepper value if the pepper was retrieved successfully
                if (pepperTask.IsCompletedSuccessfully)
                {                   
                    MemoryUtil.InitializeBlock(_pepper.Value.AsSpan());
                }
            }
        }

        private sealed class Argon2Arguments
        {
            [JsonPropertyName("hash_len")]
            public required uint HashLen { get; set; }

            [JsonPropertyName("memory_cost")]
            public required uint MemoryCost { get; set; }

            [JsonPropertyName("parallelism")]
            public required uint Parallelism { get; set; }

            [JsonPropertyName("salt_len")]
            public required uint SaltLen { get; set; }

            [JsonPropertyName("time_cost")]
            public required uint TimeCost { get; set; }
        }

    }
}
