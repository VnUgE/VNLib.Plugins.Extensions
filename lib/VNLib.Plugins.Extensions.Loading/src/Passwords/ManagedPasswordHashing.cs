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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

using VNLib.Hashing;
using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Accounts;

/*
 *   TODO:
 *     This class was originally exposed in the VNLib.Plugins.Extensions.Loading
 *     even though the file has been moved to the Passwords directory. To maintain 
 *     backwards compatibility with existing user code, the namespace has not been changed.
 */
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
            PasswordConfigJson conf = config?.Deserialize<PasswordConfigJson>() ?? new();

            if (plugin.IsDebug())
            {
                plugin.Log.Debug("Password configuration: {pwd}", conf);
            }

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
                ISecretProvider? pepper = LoadPasswordPepper(plugin, conf.PepperMlockEnabled);

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

                            plugin.Log.Verbose("Loaded custom argon2 native hashing library: {path}", config.LibPath);
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

        private static ISecretProvider? LoadPasswordPepper(PluginBase plugin, bool useMlock)
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
                .ToBase64Bytes()
                .AsLazy();

            //Log errors at startup instead of deferring to when it's used
            _ = pepper.AsTask()
                .ContinueWith(secT => plugin.Log.Error("Failed to load password pepper: {reason}", secT.Exception?.Message),
                   cancellationToken: default,
                   TaskContinuationOptions.OnlyOnFaulted,  //Only run if an exception occurred to notify the user during startup
                   TaskScheduler.Default
               );

            if (useMlock)
            {                
                IAsyncLazy<MemoryLockedPasswordSecret> lockedPepper = pepper
                    .Transform(arr =>
                    {
                        bool isLocked = false;
                        MemoryLockedPasswordSecret secret = MemoryLockedPasswordSecret.Create(MemoryUtil.Shared, arr, ref isLocked);

                        //TODO: Handle the case where memory locking is not supported or fails
                        plugin.Log.Debug("Password pepper locked in memory: {locked}", isLocked ? "yes" : "no");

                        return secret;
                    });

                return new SecretProvider(lockedPepper);
            }
            else
            {
                // The default is mlock, so we don't need to inform the user because they knowingly disabled it
                return new RawPasswordSecret(pepper);
            }
        }

       

        private sealed class RawPasswordSecret(IAsyncLazy<byte[]> rawSecret) : ISecretProvider
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
            public int BufferSize => rawSecret.Value.Length;

            ///<inheritdoc/>
            public ERRNO GetSecret(Span<byte> buffer)
            {
                rawSecret.Value.CopyTo(buffer);
                return rawSecret.Value.Length;
            }
        }

        private sealed class SecretProvider(IAsyncLazy<MemoryLockedPasswordSecret> pepper) : ISecretProvider
        {
            ///<inheritdoc/>
            public int BufferSize => pepper.Value.BufferSize;

            ///<inheritdoc/>
            public ERRNO GetSecret(Span<byte> buffer) => pepper.Value.GetSecret(buffer);
        }

        private sealed class MemoryLockedPasswordSecret : IDisposable
        {

            private readonly int _actualSize;
            private readonly MemoryHandle<byte> _secretBuffer;

            private MemoryLockedPasswordSecret(MemoryHandle<byte> buffer, int actualSize)
            {
                _secretBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                _actualSize = actualSize;
            }           

            ///<inheritdoc/>
            public int BufferSize => _actualSize;

            ///<inheritdoc/>
            public ERRNO GetSecret(Span<byte> buffer)
            {
                MemoryUtil.Copy(
                    source:_secretBuffer, 
                    sourceOffset: 0, 
                    dest:buffer, 
                    destOffset: 0, 
                    _actualSize
                );              
             
                return _actualSize;
            }

            ///<inheritdoc/>
            public void Dispose()
            {
                // If the memory can be locked, it was locked, so we need to unlock it before disposing
                if (MemoryUtil.MemoryLockSupported)
                {
                    bool unlocked = MemoryUtil.UnlockMemory(_secretBuffer);
                    Debug.Assert(unlocked);
                }

                // Clear the memory to prevent it from being leaked
                MemoryUtil.InitializeBlock(ref _secretBuffer.GetReference(), _actualSize);

                _secretBuffer.Dispose();
            }

            public static MemoryLockedPasswordSecret Create(IUnmanagedHeap heap, byte[] secretData, ref bool locked)
            {
                ArgumentNullException.ThrowIfNull(heap, nameof(heap));
                ArgumentNullException.ThrowIfNull(secretData, nameof(secretData));                

                MemoryHandle<byte> handle = MemoryUtil.SafeAllocNearestPage<byte>(heap, secretData.Length);

                try
                {
                    //Attempt to lock the memory to prevent it from being swapped out to disk (not supported on all platforms)
                    if (MemoryUtil.MemoryLockSupported)
                    {
                        //Lock the memory to prevent it from being swapped out to disk
                        locked = MemoryUtil.LockMemory(handle);
                    }

                    MemoryUtil.CopyArray(
                        source:secretData,                       
                        sourceOffset: 0,
                        dest:handle,
                        destOffset: 0, 
                        (nuint)secretData.Length
                    );

                    // Clear the original array to prevent it from floating around in memory
                    MemoryUtil.InitializeBlock(secretData);

                    //Return the memory locked secret
                    return new MemoryLockedPasswordSecret(handle, secretData.Length);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }
        }

        private sealed record class PasswordConfigJson
        {
            /// <summary>
            /// The name of the internal password provider, currently only
            /// supports "argon2" as a valid provider name.
            /// </summary>
            [JsonPropertyName("provider_name")]
            public string ProviderName { get; set; } = "argon2";

            /// <summary>
            /// Allows users to specify a custom assembly path to load a 
            /// password hashing provider from.
            /// </summary>
            [JsonPropertyName("custom_assembly")]
            public string? CustomLibAsmPath { get; set; }

            /// <summary>
            /// Disables the password pepper. This is not recommended as it 
            /// reduces the security of the password hashing.
            /// </summary>
            [JsonPropertyName("disable_pepper")]
            public bool DisablePepper { get; set; } = false;

            /// <summary>
            /// Optionally allows users to specify custom Argon2 parameters
            /// </summary>
            [JsonPropertyName("argon2_options")]
            public Argon2Arguments? Argon2Args { get; set; }

            /// <summary>
            /// The path to the custom Argon2 library to load. If not specified, 
            /// the default library will be used. (Environment variable used)
            /// </summary>
            [JsonPropertyName("argon2_lib_path")]
            public string? LibPath { get; set; }

            /// <summary>
            /// Specifies whether the password pepper should be locked in memory using mlock or 
            /// similar functionality.
            /// </summary>
            [JsonPropertyName("pepper_mlock_enabled")]
            public bool PepperMlockEnabled { get; set; } = true; // Default to true, can be overridden by user config
        }

        /// <summary>
        /// Class is meant to map to the <see cref="Argon2ConfigParams"/>
        /// structure.
        /// </summary>
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
