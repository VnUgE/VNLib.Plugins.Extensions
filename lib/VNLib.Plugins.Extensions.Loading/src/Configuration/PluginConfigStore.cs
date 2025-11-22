/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginConfigStore.cs 
*
* PluginConfigStore.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// A lightweight readonly structure that provides comprehensive access to plugin configuration data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PluginConfigStore"/> is the primary facade for interacting with plugin and host configuration.
    /// It implements a consistent Try*/Get* API pattern where:
    /// <list type="bullet">
    /// <item><description><c>Try*</c> methods return nullable types and never throw configuration-related exceptions</description></item>
    /// <item><description><c>Get*</c> methods throw <see cref="ConfigurationException"/> when configuration is not found</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This struct is designed to be created inline via the <see cref="ConfigurationExtensions.Config(PluginBase)"/> 
    /// extension method. 
    /// </para>
    /// <para>
    /// Configuration lookup follows a consistent search order:
    /// <list type="number">
    /// <item><description>Plugin-specific configuration (from <see cref="PluginBase.PluginConfig"/>)</description></item>
    /// <item><description>Host-wide configuration (from <see cref="PluginBase.HostConfig"/>)</description></item>
    /// </list>
    /// This allows plugins to override host settings while providing fallback to shared configuration.
    /// </para>
    /// <para>
    /// For complex types, configuration is retrieved using the <see cref="ConfigurationNameAttribute"/>
    /// which decorates classes to specify their configuration property name. Methods like 
    /// <see cref="GetElement{TConfig}()"/> also support configuration validation (via <see cref="IOnConfigValidation"/>)
    /// and asynchronous initialization (via <see cref="IAsyncConfigurable"/>).
    /// </para>
    /// </remarks>
    public readonly struct PluginConfigStore(PluginBase plugin)
    {
        public const string S3_CONFIG = "s3_config";
        public const string S3_SECRET_KEY = "s3_secret";
        public const string PLUGIN_ASSET_KEY = "assets";
        public const string PLUGINS_HOST_KEY = "plugins";

        /// <summary>
        /// Retrieves a top level configuration scope with the specified property name,
        /// or null if no configuration could be found
        /// </summary>
        /// <remarks>
        /// Search order: Plugin config, fall back to host config, null not found
        /// </remarks>
        /// <param name="propName">The config property name to retrieve</param>
        /// <returns>An <see cref="IConfigScope"/> for the specified property, or null if not found</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly IConfigScope? TryGet(string propName)
        {
            plugin.ThrowIfUnloaded();
            //Try to get the element from the plugin config first, or fallback to host
            if 
            (
                plugin.PluginConfig.TryGetProperty(propName, out JsonElement el) || 
                plugin.HostConfig.TryGetProperty(propName, out el)
            )
            {
                //Get the top level config as a dictionary
                return new ConfigScope(el, propName);
            }
            //No config found
            return null;
        }

        /// <summary>
        /// Retrieves a top level configuration scope with the specified property name.
        /// </summary>
        /// <remarks>
        /// Search order: Plugin config, fall back to host config, throw if not found
        /// </remarks>
        /// <param name="propName">The config property name to retrieve</param>
        /// <returns>An <see cref="IConfigScope"/> for the specified property</returns>
        /// <exception cref="ConfigurationException">Thrown when the configuration property is not found</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly IConfigScope Get(string propName)
        {
            return TryGet(propName)
                ?? throw new ConfigurationException($"Missing required top level configuration object '{propName}', in host/plugin configuration files");
        }     

        /// <summary>
        /// Retrieves a top level configuration scope for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <param name="type">The class type to get the configuration scope variable from</param>
        /// <returns>An <see cref="IConfigScope"/> for the desired top-level configuration scope, or null if not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
        public readonly IConfigScope? TryGetForType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            string? configName = GetConfigNameForType(type);

            return configName != null
                ? TryGet(configName)
                : null;
        }

        /// <summary>
        /// Retrieves a top level configuration scope for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <typeparam name="T">The type to get the configuration of</typeparam>
        /// <returns>An <see cref="IConfigScope"/> for the type, or null if not found</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly IConfigScope? TryGetForType<T>() 
            => TryGetForType(typeof(T));

        /// <summary>
        /// Retrieves a top level configuration scope for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <param name="type">The type to get configuration data for</param>
        /// <returns>An <see cref="IConfigScope"/> for the type</returns>
        /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
        /// <exception cref="ConfigurationException">Thrown when the configuration for the type is not found</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly IConfigScope GetForType(Type type)
        {
            return TryGetForType(type)
                ?? throw new ConfigurationException($"Missing required configuration key for type {type.Name}");
        }

        /// <summary>
        /// Retrieves a top level configuration scope for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <typeparam name="T">The type to get the configuration of</typeparam>
        /// <returns>An <see cref="IConfigScope"/> for the type</returns>
        /// <exception cref="ConfigurationException">Thrown when the configuration for the type is not found</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly IConfigScope GetForType<T>() 
            => GetForType(typeof(T));

        /// <summary>
        /// Shortcut extension for <see cref="TryGetForType{T}()"/> to get 
        /// config for the desired object
        /// </summary>
        /// <param name="obj">The object that a configuration can be retrieved for</param>
        /// <returns>
        /// An <see cref="IConfigScope"/> for the object's type if found, null otherwise
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when obj is null</exception>
        public readonly IConfigScope? TryGetFor(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return TryGetForType(obj.GetType());
        }

        /// <summary>
        /// Shortcut extension for <see cref="GetForType{T}()"/> to get 
        /// config for the desired object
        /// </summary>
        /// <param name="obj">The object that a configuration can be retrieved for</param>
        /// <returns>An <see cref="IConfigScope"/> for the object's type</returns>
        /// <exception cref="ArgumentNullException">Thrown when obj is null</exception>
        /// <exception cref="ConfigurationException">Thrown when the configuration for the type is not found</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly IConfigScope GetFor(object obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GetForType(obj.GetType());
        }

        /// <summary>
        /// Attempts to get a configuration element from the global configuration scope
        /// and deserialize it into the desired type. 
        /// <para>
        /// If the type inherits <see cref="IOnConfigValidation"/> the <see cref="IOnConfigValidation.OnValidate"/>
        /// method is invoked, and exceptions are wrapped in <see cref="ConfigurationValidationException"/>
        /// </para>
        /// <para>
        /// If the type inherits <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
        /// method is called by the service scheduler
        /// </para>
        /// </summary>
        /// <typeparam name="TConfig">The configuration type</typeparam>
        /// <returns>The deserialized configuration element if found, null otherwise</returns>
        public readonly TConfig? TryGetElement<TConfig>() where TConfig : class
        {
            TConfig? config = TryGetForType<TConfig>()
                                ?.Deserialize<TConfig>();
            
            if (config is null)
            {
                return null;
            }           

            TryValidateConfig(config);

            //If async config, load async
            if (config is IAsyncConfigurable ac)
            {
                _ = plugin.ConfigureServiceAsync(ac);
            }

            return config;
        }

        /// <summary>
        /// Gets a given configuration element from the global configuration scope
        /// and deserializes it into the desired type. 
        /// <para>
        /// If the type inherits <see cref="IOnConfigValidation"/> the <see cref="IOnConfigValidation.OnValidate"/>
        /// method is invoked, and exceptions are wrapped in <see cref="ConfigurationValidationException"/>
        /// </para>
        /// <para>
        /// If the type inherits <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
        /// method is called by the service scheduler
        /// </para>
        /// </summary>
        /// <typeparam name="TConfig">The configuration type</typeparam>
        /// <returns>The deserialized configuration element</returns>
        /// <exception cref="ConfigurationException">Thrown when the configuration is not found</exception>
        /// <exception cref="ConfigurationValidationException">Thrown when configuration validation fails</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly TConfig GetElement<TConfig>()
        {
            //Deserialize the element
            TConfig config = GetForType<TConfig>().Deserialize<TConfig>();

            TryValidateConfig(config);

            //If async config, load async
            if (config is IAsyncConfigurable ac)
            {
                _ = plugin.ConfigureServiceAsync(ac);
            }

            return config;
        }

        /// <summary>
        /// Gets a given configuration element from the global configuration scope
        /// and deserializes it into the desired type. 
        /// <para>
        /// If the type inherits <see cref="IOnConfigValidation"/> the <see cref="IOnConfigValidation.OnValidate"/>
        /// method is invoked, and exceptions are wrapped in <see cref="ConfigurationValidationException"/>
        /// </para>
        /// <para>
        /// If the type inherits <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
        /// method is called by the service scheduler
        /// </para>
        /// </summary>
        /// <typeparam name="TConfig">The configuration type</typeparam>
        /// <param name="elementName">The configuration element name override</param>
        /// <returns>The deserialized configuration element</returns>
        /// <exception cref="ConfigurationException">Thrown when the configuration element is not found</exception>
        /// <exception cref="ConfigurationValidationException">Thrown when configuration validation fails</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly TConfig GetElement<TConfig>(string elementName)
        {
            //Deserialize the element
            TConfig config = Get(elementName).Deserialize<TConfig>();

            TryValidateConfig(config);

            //If async config, load async
            if (config is IAsyncConfigurable ac)
            {
                _ = plugin.ConfigureServiceAsync(ac);
            }

            return config;
        }


        /// <summary>
        /// Determines if the current plugin configuration contains the required properties to initialize 
        /// the type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the plugin config contains the required configuration property</returns>
        public readonly bool HasForType<T>()
            => HasForType(typeof(T));

        /// <summary>
        /// Determines if the current plugin configuration contains the required properties to initialize 
        /// the type
        /// </summary>
        /// <param name="type">The type to get the configuration for</param>
        /// <returns>True if the plugin config contains the required configuration property</returns>
        /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
        public readonly bool HasForType(Type type)
        {
            ConfigurationNameAttribute? configName = GetConfigurationNameAttribute(type);

            //See if the plugin contains a configuration variables
            return configName != null && (
                plugin.PluginConfig.TryGetProperty(configName.ConfigVarName, out _) ||
                plugin.HostConfig.TryGetProperty(configName.ConfigVarName, out _)
            );
        }

        /// <summary>
        /// Attempts to load the basic S3 configuration variables required
        /// for S3 client access
        /// </summary>
        /// <returns>The S3 configuration object found in the plugin/host configuration, or null if not found</returns>
        public readonly S3Config? TryGetS3() 
            => TryGet(S3_CONFIG)?.Deserialize<S3Config>();

        /// <summary>
        /// Loads the basic S3 configuration variables required for S3 client access
        /// </summary>
        /// <returns>The S3 configuration object found in the plugin/host configuration</returns>
        /// <exception cref="ConfigurationException">Thrown when S3 configuration is not found</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly S3Config GetS3()
            => Get(S3_CONFIG).Deserialize<S3Config>();

        /// <summary>
        /// Attempts to get the optional assets directory from the plugin configuration
        /// </summary>
        /// <returns>The absolute path to the assets directory if defined, null otherwise</returns>
        public readonly string? TryGetAssetsPath()
        {
            //Try to get the assets path if defined
            IConfigScope? config = TryGet(PLUGINS_HOST_KEY);
            if (config is null)
            {
                return null;
            }

            string? assetsPath = config.GetPropString(PLUGIN_ASSET_KEY);

            //Return the full path for the assets if defined
            return assetsPath != null ? Path.GetFullPath(assetsPath) : null;
        }

        /// <summary>
        /// Gets the assets directory from the plugin configuration
        /// </summary>
        /// <returns>The absolute path to the assets directory</returns>
        /// <exception cref="ConfigurationException">Thrown when the plugins configuration section or assets path is not configured</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly string GetAssetsPath()
        {
            //Get the plugins host config (throws if not present)
            string? assetsPath = Get(PLUGINS_HOST_KEY)
                                .GetPropString(PLUGIN_ASSET_KEY);

            return assetsPath != null 
                ? Path.GetFullPath(assetsPath) 
                : throw new ConfigurationException($"Assets path '{PLUGIN_ASSET_KEY}' not configured in '{PLUGINS_HOST_KEY}' configuration section");
        }

        /// <summary>
        /// Attempts to get the absolute paths to the plugin directories as defined in the host configuration
        /// </summary>
        /// <returns>The absolute paths to directories containing plugins, or empty array if not configured</returns>
        public readonly string[] TryGetPluginSearchDirs()
        {
            //Try to get global plugin config element
            IConfigScope? config = TryGet(PLUGINS_HOST_KEY);
            if (config is null)
            {
                return [];
            }

            /*
             * Hosts are allowed to define multiple plugin loading paths. A
             * single path is supported for compat. Multi path takes precedence 
             * of course so attempt to load a string array first
             */

            if (
                !config.TryGetValue("paths", out JsonElement searchPaths) && 
                !config.TryGetValue("path", out searchPaths))
            {
                return [];
            }

            switch (searchPaths.ValueKind)
            {
                case JsonValueKind.Array:
                    return searchPaths.EnumerateArray()
                        .Select(static p => p.GetString()!)
                        .Select(Path.GetFullPath)   //Get absolute file paths
                        .ToArray();

                case JsonValueKind.String:
                    return [Path.GetFullPath(searchPaths.GetString()!)];

                default:
                    return [];
            }
        }

        /// <summary>
        /// Gets the absolute paths to the plugin directories as defined in the host configuration
        /// </summary>
        /// <returns>The absolute paths to directories containing plugins</returns>
        /// <exception cref="ConfigurationException">Thrown when plugin search paths are not configured</exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public readonly string[] GetPluginSearchDirs()
        {
            string[] paths = TryGetPluginSearchDirs();

            return paths.Length == 0
                ? throw new ConfigurationException("Plugin search paths ('path' or 'paths') not configured in host configuration")
                : paths;
        }

        /// <summary>
        /// Gets the <see cref="ConfigurationNameAttribute"/> from the specified type, if present.
        /// </summary>
        /// <remarks>
        /// This is a helper method used internally to retrieve configuration metadata from a type.
        /// Most callers should use <see cref="GetConfigNameForType(Type)"/> or <see cref="ConfigurationRequired(Type)"/>
        /// instead, which provide more convenient access to the attribute's properties.
        /// </remarks>
        /// <param name="type">The type to inspect for the configuration attribute</param>
        /// <returns>
        /// The <see cref="ConfigurationNameAttribute"/> if the type is decorated with one, null otherwise
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
        public static ConfigurationNameAttribute? GetConfigurationNameAttribute(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return type.GetCustomAttribute<ConfigurationNameAttribute>();
        }

        /// <summary>
        /// Gets the configuration property name for the type
        /// </summary>
        /// <param name="type">The type to get the configuration name for</param>
        /// <returns>The configuration property element name</returns>
        public static string? GetConfigNameForType(Type type) 
            => GetConfigurationNameAttribute(type)?.ConfigVarName;

        /// <summary>
        /// Determines if the type requires a configuration element.
        /// </summary>
        /// <param name="type">The type to determine config required status</param>
        /// <returns>
        /// True if the configuration is required, or false if the <see cref="ConfigurationNameAttribute"/> 
        /// was not declared, or <see cref="ConfigurationNameAttribute.Required"/> is false
        /// </returns>
        public static bool ConfigurationRequired(Type type)
            => GetConfigurationNameAttribute(type)?.Required ?? false;

        /// <summary>
        /// Throws a <see cref="KeyNotFoundException"/> with proper diagnostic information
        /// for missing configuration for a given type
        /// </summary>
        /// <param name="type">The type to raise exception for</param>
        /// <exception cref="ConfigurationException"></exception>
        [DoesNotReturn]
        public static void ThrowConfigNotFoundForType(Type type)
        {
            //Try to get the config property name for the type
            string? configName = GetConfigNameForType(type);
            if (configName != null)
            {
                throw new ConfigurationException($"Missing required configuration key '{configName}' for type {type.Name}");
            }
            else
            {
                throw new ConfigurationException($"Missing required configuration key for type {type.Name}");
            }
        }

        internal static void TryValidateConfig<TConfig>(TConfig config)
        {
            //If the type is validatable, validate it
            if (config is IOnConfigValidation conf)
            {
                try
                {
                    conf.OnValidate();
                }
                catch (ConfigurationValidationException)
                {
                    //Rethrow validation exceptions as is
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ConfigurationValidationException($"Configuration validation failed for type {typeof(TConfig).Name}", ex);
                }
            }
        }
    }
}
