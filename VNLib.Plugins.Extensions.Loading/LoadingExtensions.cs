﻿using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Accounts;
using VNLib.Plugins.Extensions.Loading.Configuration;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Provides common loading (and unloading when required) extensions for plugins
    /// </summary>
    public static class LoadingExtensions
    {
        public const string DEBUG_CONFIG_KEY = "debug";
        public const string SECRETS_CONFIG_KEY = "secrets";
        public const string PASSWORD_HASHING_KEY = "passwords";
    

        private static readonly ConditionalWeakTable<PluginBase, Lazy<PasswordHashing>> LazyPasswordTable = new();
   

        /// <summary>
        /// Gets the plugins ambient <see cref="PasswordHashing"/> if loaded, or loads it if required. This class will
        /// be unloaded when the plugin us unloaded.
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="PasswordHashing"/></returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static PasswordHashing GetPasswords(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Get/load the passwords one time only
            return LazyPasswordTable.GetValue(plugin, LoadPasswords).Value;
        }
        private static Lazy<PasswordHashing> LoadPasswords(PluginBase plugin)
        {
            //Lazy load func
            PasswordHashing Load()
            {
                PasswordHashing Passwords;
                //Get the global password system secret (pepper)
                string pepperEl = plugin.TryGetSecretAsync(PASSWORD_HASHING_KEY).Result ?? throw new KeyNotFoundException($"Missing required key '{PASSWORD_HASHING_KEY}' in secrets");
                
                byte[] pepper = Convert.FromBase64String(pepperEl);
                
                //wipe the pepper string
                Utils.Memory.Memory.UnsafeZeroMemory<char>(pepperEl);

                ERRNO cb(Span<byte> buffer)
                {
                    pepper.CopyTo(buffer);
                    return pepper.Length;
                }

                //See hashing params are defined
                IReadOnlyDictionary<string, JsonElement>? hashingArgs = plugin.TryGetConfig(PASSWORD_HASHING_KEY);
                if (hashingArgs is not null)
                {
                    //Get hashing arguments
                    uint saltLen = hashingArgs["salt_len"].GetUInt32();
                    uint hashLen = hashingArgs["hash_len"].GetUInt32();
                    uint timeCost = hashingArgs["time_cost"].GetUInt32();
                    uint memoryCost = hashingArgs["memory_cost"].GetUInt32();
                    uint parallelism = hashingArgs["parallelism"].GetUInt32();
                    //Load passwords
                    Passwords = new(cb, pepper.Length, (int)saltLen, timeCost, memoryCost, parallelism, hashLen);
                }
                else
                {
                    //Init default password hashing
                    Passwords = new(cb, pepper.Length);
                }
               
                //Register event to cleanup the password class
                _ = plugin.UnloadToken.RegisterUnobserved(() =>
                {
                    //Zero the pepper
                    CryptographicOperations.ZeroMemory(pepper);
                    LazyPasswordTable.Remove(plugin);
                });
                //return
                return Passwords;
            }
            //Return new lazy for 
            return new Lazy<PasswordHashing>(Load);
        }


        /// <summary>
        /// Loads an assembly into the current plugins AppDomain and will unload when disposed
        /// or the plugin is unloaded from the host application. 
        /// </summary>
        /// <typeparam name="T">The desired exported type to load from the assembly</typeparam>
        /// <param name="plugin"></param>
        /// <param name="assemblyName">The name of the assembly (ex: 'file.dll') to search for</param>
        /// <param name="dirSearchOption">Directory/file search option</param>
        /// <returns>The <see cref="AssemblyLoader{T}"/> managing the loaded assmbly in the current AppDomain</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="EntryPointNotFoundException"></exception>
        public static AssemblyLoader<T> LoadAssembly<T>(this PluginBase plugin, string assemblyName, SearchOption dirSearchOption = SearchOption.AllDirectories)
        {
            plugin.ThrowIfUnloaded();
            _ = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
            //get plugin directory from config
            string? pluginsBaseDir = plugin.GetConfig("plugins")["path"].GetString();
            
            /*
             * This should never happen since this method can only be called from a
             * plugin context, which means this path was used to load the current plugin
             */            
            _ = pluginsBaseDir ?? throw new ArgumentNullException("path", "No plugin path is defined for the current host configuration, this is likely a bug");
            
            //Get the first file that matches the search file
            string? asmFile =  Directory.EnumerateFiles(pluginsBaseDir, assemblyName, dirSearchOption).FirstOrDefault();
            _ = asmFile ?? throw new FileNotFoundException($"Failed to load custom assembly {assemblyName} from plugin directory");
            
            //Load the assembly
            return AssemblyLoader<T>.Load(asmFile, plugin.UnloadToken);
        }

        /// <summary>
        /// Determintes if the current plugin config has a debug propety set
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>True if debug mode is enabled, false otherwise</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static bool IsDebug(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Check for debug element
            return plugin.PluginConfig.TryGetProperty(DEBUG_CONFIG_KEY, out JsonElement dbgEl) && dbgEl.GetBoolean();
        }
        /// <summary>
        /// Internal exception helper to raise <see cref="ObjectDisposedException"/> if the plugin has been unlaoded
        /// </summary>
        /// <param name="plugin"></param>
        /// <exception cref="ObjectDisposedException"></exception>
        public static void ThrowIfUnloaded(this PluginBase plugin)
        {
            //See if the plugin was unlaoded
            if (plugin.UnloadToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException("The plugin has been unloaded");
            }
        }
    }
}
