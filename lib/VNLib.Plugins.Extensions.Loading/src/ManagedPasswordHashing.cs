/*
* Copyright (c) 2025 Vaughn Nugent
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
    /// assemblies backing instances of this class or configure the <see cref="Argon2HashProvider"/> implementation
    /// </summary>
    [ConfigurationName(LoadingExtensions.PASSWORD_HASHING_KEY, Required = false)]
    public sealed class ManagedPasswordHashing : IPasswordHashingProvider
    {
        public ManagedPasswordHashing(PluginBase plugin, IConfigScope? config)
        {
            PasswordConfigJson conf = config?.Deserialze<PasswordConfigJson>() ?? new();

            //Check for custom hashing assembly
            if (!string.IsNullOrWhiteSpace(conf.CustomLibAsmPath))
            {
                //Load the custom assembly
                Passwords = plugin.CreateServiceExternal<IPasswordHashingProvider>(conf.CustomLibAsmPath);

                plugin.Log.Verbose("Loading custom password hashing assembly: {path}", conf.CustomLibAsmPath);
            }
            //Allow the user to explicitly disable pepper
            else if (conf.DisablePepper)
            {
                Passwords = LoadHashingLibrary(plugin, conf, pepper: null);
            }
            else
            {
                SecretProvider? pepper = LoadPasswordPepper(plugin);

                Passwords = LoadHashingLibrary(plugin, conf, pepper);
            }
        }

        public ManagedPasswordHashing(PluginBase plugin) : this(plugin, null)
        { }

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

        private static IPasswordHashingProvider LoadHashingLibrary(PluginBase plugin, PasswordConfigJson config, ISecretProvider? pepper)
        {
            IPasswordHashingProvider passwords;

            Argon2ConfigParams costParams = new();

            if (pepper is null)
            {
                plugin.Log.Warn(
                    "Password pepper is not defined. Your password database is more " +
                    "secure if you enable a pepper. If you meant to disable the password pepper you may ignore this message"
                );
            }

            switch (config.ProviderName)
            {
                //If no provider is specified or the provider is argon2
                case "":
                case null:
                    plugin.Log.Debug("Attempting to load default password hashing library: argon2");
                    goto case "argon2";

                case "argon2":
                    {
                        if (config.Argon2Args is not null)
                        {
                            costParams = new()
                            {
                                HashLen     = config.Argon2Args.HashLen,
                                MemoryCost  = config.Argon2Args.MemoryCost,
                                Parallelism = config.Argon2Args.Parallelism,
                                SaltLen     = (int)config.Argon2Args.SaltLen,
                                TimeCost    = config.Argon2Args.TimeCost
                            };
                        }

                        //See if the user want to load a custom argon2 library
                        if (!string.IsNullOrWhiteSpace(config.LibPath))
                        {
                            SafeArgon2Library lib = VnArgon2.LoadCustomLibrary(
                                dllPath: config.LibPath,
                                System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories
                            );

                            //Dynamically loaded lib must be disposed manually
                            _ = plugin.RegisterForUnload(lib.Dispose);

                            //Create passwords with the configuration and library
                            passwords = Argon2HashProvider.Create(lib, pepper, in costParams);

                            plugin.Log.Verbose("Loaded custom password hashing library: {path}", config.LibPath);
                        }
                        else
                        {
                            //Load default library if the user did not explictly specify one
                            passwords = Argon2HashProvider.Create(pepper, in costParams);
                        }

                        break;
                    }

                default:
                    throw new ConfigurationException($"Invalid password hashing provider specified: {config.ProviderName}");

            }

            if (plugin.IsDebug())
            {
                plugin.Log.Verbose("Argon2 parameters: {params}", costParams);
            }

            return passwords;
        }

        private static SecretProvider? LoadPasswordPepper(PluginBase plugin)
        {
            //If no secret was set for the password hashing key, return null
            if (!plugin.Secrets().IsSet(LoadingExtensions.PASSWORD_HASHING_KEY))
            {
                return null;
            }

            //Get the pepper from secret storage
            IAsyncLazy<byte[]> pepper = plugin
                .Secrets()
                .GetSecretAsync(LoadingExtensions.PASSWORD_HASHING_KEY)
                .ToLazy(static sr => sr.GetFromBase64());

            _ = pepper.AsTask()
                .ContinueWith(secT => plugin.Log.Error("Failed to load password pepper: {reason}", secT.Exception?.Message),
                    cancellationToken: default,
                    TaskContinuationOptions.OnlyOnFaulted,  //Only run if an exception occured to notify the user during startup
                    TaskScheduler.Default
                );

            return new(pepper);
        }

        private sealed class SecretProvider(IAsyncLazy<byte[]> pepper) : ISecretProvider
        {
            /*
             * Originally this wrapper contained code to zero the pepper buffer
             * when the plugin unloaded. It was removed because
             * 
             * In production a plugin only exits when the process has requested a clean
             * exit, otheriwse the process is terminated, and memory is no longer
             * our issue. This memory will be returned to the OS and out of our control.
             * 
             * I may reimplement if it's a concern that the OS will leak memory 
             * reclaimed to another process after it exits
             */

            ///<inheritdoc/>
            public int BufferSize => pepper.Value.Length;

            public ERRNO GetSecret(Span<byte> buffer)
            {
                //Coppy pepper to buffer
                pepper.Value.CopyTo(buffer);
                //Return pepper length
                return pepper.Value.Length;
            }
        }

        private sealed class PasswordConfigJson
        {
            [JsonPropertyName("provider_name")]
            public string ProviderName { get; set; } = "argon2";

            [JsonPropertyName("custom_assembly")]
            public string? CustomLibAsmPath { get; set; }

            [JsonPropertyName("disable_pepper")]
            public bool DisablePepper { get; set; } = false;

            [JsonPropertyName("argon2_options")]
            public Argon2Arguments? Argon2Args { get; set; }

            [JsonPropertyName("argon2_lib_path")]
            public string? LibPath { get; set; }
        }

        private sealed record class Argon2Arguments
        {
            [JsonPropertyName("hash_length")]
            public required uint HashLen { get; set; }

            [JsonPropertyName("memory_cost")]
            public required uint MemoryCost { get; set; }

            [JsonPropertyName("parallelism")]
            public required uint Parallelism { get; set; }

            [JsonPropertyName("salt_length")]
            public required uint SaltLen { get; set; }

            [JsonPropertyName("time_cost")]
            public required uint TimeCost { get; set; }
        }
    }
}
