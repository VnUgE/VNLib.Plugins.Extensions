/*
* Copyright (c) 2026 Vaughn Nugent
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
using VNLib.Utils.Async;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

// TODO: TEMPORARY remove in v0.2.0
using VNLib.Plugins.Essentials.Accounts;
using VNLib.Plugins.Extensions.Loading.Configuration;
using VNLib.Plugins.Extensions.Loading.Secrets;

namespace VNLib.Plugins.Extensions.Loading.Passwords
{
    /// <summary>
    /// Provides a plugin-configurable managed implementation of <see cref="IPasswordHashingProvider"/>.
    /// </summary>
    [ConfigurationName(PASSWORD_CONFIG_KEY, Required = false)]
    public sealed class ManagedPasswordHashing : IPasswordHashingProvider
    {
        /// <summary>
        /// Configuration property name for the "passwords" configuration object in the 
        /// plugin config store.
        /// </summary>
        const string PASSWORD_CONFIG_KEY = "passwords";

        /// <summary>
        /// Configuration property name (key) within the secrets store
        /// that holds the password secret aka pepper. 
        /// </summary>
        const string PASSWORD_SECRET_CONFIG_KEY = "passwords";

        private readonly IAsyncLazy<IPasswordHashingProvider> _provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedPasswordHashing"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance.</param>
        /// <param name="config">The configuration scope for password settings.</param>
        public ManagedPasswordHashing(PluginBase plugin, IConfigScope? config)
        {
            PasswordConfigJson conf = config?.DeserializeAndValidate<PasswordConfigJson>() ?? new();

            if (plugin.IsDebug())
            {
                plugin.Log.Debug("Password configuration: {pwd}", conf);
            }

            //Check for custom hashing assembly
            if (!string.IsNullOrWhiteSpace(conf.CustomLibAsmPath))
            {
                IPasswordHashingProvider prov = plugin.Deps()
                    .LoadExternal<IPasswordHashingProvider>(conf.CustomLibAsmPath);

                //Load the custom assembly
                _provider = Task.FromResult(prov).AsLazy();

                plugin.Log.Verbose("Loading custom password hashing assembly: {path}", conf.CustomLibAsmPath);
            }
            // Allow the user to explicitly disable pepper
            else if (conf.DisablePepper)
            {
                IPasswordHashingProvider prov = LoadHashingLibrary(plugin, conf, pepper: null);

                _provider = Task.FromResult(prov).AsLazy();
            }
            else
            {
                /*
                 * Lazy load the pepper and then use lazy transform to wrap the hashing library 
                 * around the pepper
                 */

                _provider = LoadPasswordPepperAsync(plugin, conf.PepperMlockEnabled)
                    .AsLazy()
                    .Transform(pepper => LoadHashingLibrary(plugin, conf, pepper));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedPasswordHashing"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance.</param>
        // Defaults constructor if user defined config does not exist
        public ManagedPasswordHashing(PluginBase plugin) : this(plugin, null)
        { }

        /// <summary>
        /// Gets the underlying <see cref="IPasswordHashingProvider"/>.
        /// </summary>
        public IPasswordHashingProvider Passwords => _provider.Value;

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

        private static IPasswordHashingProvider LoadHashingLibrary(
            PluginBase plugin,
            PasswordConfigJson config,
            ISecretProvider? pepper
        )
        {
            IPasswordHashingProvider passwords;

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
                    plugin.Log.Verbose("Attempting to load default password hashing library: argon2");
                    goto case "argon2";

                case "argon2":
                    {
                        // If the user did not specify argon2 options, use the defaults
                        Argon2Config libConfig = config.Argon2Args ?? new ();

                        IArgon2Library argonLib;

                        //See if the user wants to load a custom argon2 library
                        if (!string.IsNullOrWhiteSpace(config.LibPath))
                        {
                            SafeArgon2Library lib = VnArgon2.LoadCustomLibrary(
                                dllPath: config.LibPath,
                                System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories
                            );

                            //Dynamically loaded lib must be disposed manually
                            _ = plugin.Tasks()
                                .RegisterForUnload(lib.Dispose);

                            argonLib = lib;

                            plugin.Log.Debug("Loaded custom argon2 native hashing library: {path}", config.LibPath);
                        }
                        else
                        {
                            //Load default library if the user did not explicitly specify one
                            argonLib = VnArgon2.GetOrLoadSharedLib();
                        }

                        passwords = new Argon2HashProvider(argonLib, libConfig, MemoryUtil.Shared, pepper);

                        if (plugin.IsDebug())
                        {
                            plugin.Log.Verbose("Argon2 parameters: {params}", libConfig);
                        }

                        break;
                    }

                default:
                    throw new ConfigurationException($"Invalid password hashing provider specified: {config.ProviderName}");

            }

            return passwords;
        }

        private static async Task<ISecretProvider?> LoadPasswordPepperAsync(PluginBase plugin, bool useMlock)
        {
            //If no secret was set for the password hashing key, return null
            if (!plugin.Secrets().IsSet(PASSWORD_SECRET_CONFIG_KEY))
            {
                return null;
            }

            try
            {
                //Get the pepper from secret storage
                byte[] rawPepper = await plugin.Secrets()
                    .GetAsync(PASSWORD_SECRET_CONFIG_KEY)
                    .ToBase64Bytes()
                    .ConfigureAwait(false);

                if (useMlock)
                {
                    if (MemoryUtil.MemoryLockSupported)
                    {
                        bool isLocked = false;
                        MemoryLockedPasswordSecret secret = MemoryLockedPasswordSecret.Create(MemoryUtil.Shared, rawPepper, ref isLocked);

                        // Cleanup the pepper when plugin unloads
                        _ = plugin.Tasks()
                            .RegisterForUnload(secret.Dispose);

                        // TODO Decide if continuing is acceptable if locking fails.
                        if (!isLocked)
                        {
                            plugin.Log.Error("Failed to lock password pepper into memory on supported system");
                        }
                        else
                        {
                            plugin.Log.Debug("Password pepper locked in memory successfully");
                        }

                        return secret;
                    }

                    plugin.Log.Warn("Pepper mlock was requested but the platform does not support mlock, falling back");
                }

                return new RawPasswordSecret(rawPepper);
            }
            catch (Exception ex)
            {
                //Log errors now but also re-throw so _provider.Value rethrows the exception
                plugin.Log.Error(ex, "Failed to load password pepper");
                throw;
            }
        }


        private sealed class RawPasswordSecret(byte[] rawSecret) : ISecretProvider
        {
            /*
             * Originally this wrapper contained code to zero the pepper buffer
             * when the plugin unloaded. It was removed because
             * 
             * In production a plugin only exits when the process has requested a clean
             * exit, otherwise the process is terminated, and memory is no longer
             * our issue. This memory will be returned to the OS and out of our control.
             * 
             * I may reimplement if it's a concern that the OS will leak memory 
             * reclaimed to another process after it exits
             */

            ///<inheritdoc/>
            public ReadOnlySpan<byte> Secret => rawSecret.AsSpan();
        }

        private sealed class MemoryLockedPasswordSecret : IDisposable, ISecretProvider
        {
            private readonly int _actualSize;
            private readonly MemoryHandle<byte> _secretBuffer;

            // Tracks if the memory block was successfully locked
            private readonly bool _isLocked;

            private MemoryLockedPasswordSecret(MemoryHandle<byte> buffer, int actualSize, bool isLocked)
            {
                _secretBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                _actualSize = actualSize;
                _isLocked = isLocked;
            }

            ///<inheritdoc/>
            public ReadOnlySpan<byte> Secret => _secretBuffer.AsSpan(0, _actualSize);

            ///<inheritdoc/>
            public void Dispose()
            {
                try
                {
                    if (_isLocked)
                    {

#pragma warning disable CA1416 // Validate platform compatibility
                        bool unlocked = MemoryUtil.UnlockMemory(_secretBuffer);
#pragma warning restore CA1416 // Validate platform compatibility

                        Debug.Assert(unlocked);
                    }

                    // Clear the memory to prevent it from being leaked
                    MemoryUtil.InitializeBlock(ref _secretBuffer.GetReference(), _actualSize);
                }
                finally
                {
                    _secretBuffer.Dispose();
                }
            }

            /// <summary>
            /// Creates a new instance of <see cref="MemoryLockedPasswordSecret"/> by allocating memory for the secret data and optionally locking it in memory.
            /// </summary>
            /// <remarks>
            /// The original secret data array is cleared after copying to prevent it from remaining in memory.
            /// Always use <see cref="MemoryUtil.MemoryLockSupported"/> to determine if memory locking is supported on the current platform.
            /// </remarks>
            /// <param name="heap">The heap to allocate memory from.</param>
            /// <param name="secretData">The secret data to copy and lock.</param>
            /// <param name="locked">A value that indicates whether the memory was successfully locked.</param>
            /// <returns>A new <see cref="MemoryLockedPasswordSecret"/> instance.</returns>
            public static MemoryLockedPasswordSecret Create(IUnmanagedHeap heap, byte[] secretData, ref bool locked)
            {
                ArgumentNullException.ThrowIfNull(heap, nameof(heap));
                ArgumentNullException.ThrowIfNull(secretData, nameof(secretData));

                MemoryHandle<byte> handle = MemoryUtil.SafeAllocNearestPage<byte>(heap, secretData.Length);

                try
                {
                    // Lock the memory to prevent it from being swapped out to disk
                    // When supported == true this call is supported on the current platform

#pragma warning disable CA1416 // Validate platform compatibility
                    locked = MemoryUtil.LockMemory(handle);
#pragma warning restore CA1416 // Validate platform compatibility

                    MemoryUtil.CopyArray(
                        source: secretData,
                        sourceOffset: 0,
                        dest: handle,
                        destOffset: 0,
                        (nuint)secretData.Length
                    );

                    // Clear the original array to prevent it from floating around in memory
                    MemoryUtil.InitializeBlock(secretData);

                    //Return the memory locked secret
                    return new MemoryLockedPasswordSecret(handle, secretData.Length, locked);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }
        }

        private sealed record class PasswordConfigJson : IOnConfigValidation
        {
            /// <summary>
            /// Gets the path to the custom Argon2 native library.
            /// </summary>
            /// <remarks>
            /// If not specified, the default library will be used. An environment variable may also be used to specify the path.
            /// </remarks>
            [JsonPropertyName("argon2_lib_path")]
            public string? LibPath { get; init; }

            /// <summary>
            /// Gets the custom Argon2 parameters.
            /// </summary>
            [JsonPropertyName("argon2_options")]
            public Argon2Config? Argon2Args { get; init; }

            /// <summary>
            /// Gets the path to a custom password hashing provider assembly.
            /// </summary>
            [JsonPropertyName("custom_assembly")]
            public string? CustomLibAsmPath { get; init; }

            /// <summary>
            /// Gets a value that indicates whether the password pepper is disabled.
            /// </summary>
            /// <remarks>
            /// Disabling the pepper is not recommended as it reduces the security of password hashing.
            /// </remarks>
            [JsonPropertyName("disable_pepper")]
            public bool DisablePepper { get; init; } = false;

            /// <summary>
            /// Gets a value that indicates whether the password pepper is locked in memory using mlock.
            /// </summary>
            [JsonPropertyName("pepper_mlock_enabled")]
            public bool PepperMlockEnabled { get; init; } = true; // Default to true, can be overridden by user config

            /// <summary>
            /// Gets the name of the internal password hashing provider.
            /// </summary>
            /// <remarks>
            /// Currently only supports "argon2" as a valid provider name.
            /// </remarks>
            [JsonPropertyName("provider_name")]
            public string ProviderName { get; init; } = "argon2";

            public void OnValidate()
            {
                if (!string.IsNullOrWhiteSpace(CustomLibAsmPath))
                {
                    Validate.Matches(CustomLibAsmPath, pattern: ".*\\.dll$", "Custom password hashing assembly path must be a .dll file");
                }

                if (!string.IsNullOrWhiteSpace(LibPath))
                {
                    // Argon2 lib must be a shared library (.dll, .so, .dylib)
                    Validate.Matches(LibPath, pattern: ".*\\.(dll|so|so2|dylib)$", "Custom Argon2 library path must be a .dll, .so, or .dylib file");
                }

                Argon2Args?.OnValidate();
            }
        }
    }
}
