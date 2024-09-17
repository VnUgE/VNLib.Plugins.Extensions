/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: ConfigurationExtensions.cs 
*
* ConfigurationExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Extensions.Loading.Configuration;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Specifies a configuration variable name in the plugin's configuration 
    /// containing data specific to the type
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="ConfigurationNameAttribute"/>
    /// </remarks>
    /// <param name="configVarName">The name of the configuration variable for the class</param>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigurationNameAttribute(string configVarName) : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        public string ConfigVarName { get; } = configVarName;

        /// <summary>
        /// When true or not configured, signals that the type requires a configuration scope
        /// when loaded. When false, and configuration is not found, signals to the service loading
        /// system to continue without configuration
        /// </summary>
        public bool Required { get; init; } = true;
    }

    /// <summary>
    /// Contains extensions for plugin configuration specifc extensions
    /// </summary>
    public static class ConfigurationExtensions
    {
        public const string S3_CONFIG = "s3_config";
        public const string S3_SECRET_KEY = "s3_secret";
        public const string PLUGIN_ASSET_KEY = "assets";
        public const string PLUGINS_HOST_KEY = "plugins";


        /// <summary>
        /// Retrieves a top level configuration dictionary of elements with the specified property name,
        /// or null if no configuration could be found
        /// </summary>
        /// <remarks>
        /// Search order: Plugin config, fall back to host config, null not found
        /// </remarks>
        /// <param name="plugin"></param>
        /// <param name="propName">The config property name to retrieve</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope? TryGetConfig(this PluginBase plugin, string propName)
        {
            plugin.ThrowIfUnloaded();
            //Try to get the element from the plugin config first, or fallback to host
            if (plugin.PluginConfig.TryGetProperty(propName, out JsonElement el)
                || plugin.HostConfig.TryGetProperty(propName, out el))
            {
                //Get the top level config as a dictionary
                return new ConfigScope(el, propName);
            }
            //No config found
            return null;
        }

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="type">The class type to get the configuration scope variable from</param>
        /// <returns>A <see cref="IConfigScope"/> for the desired top-level configuration scope</returns>
        /// <exception cref="ConfigurationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope? TryGetConfigForType(this PluginBase plugin, Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            string? configName = GetConfigNameForType(type);

            return configName != null 
                ? TryGetConfig(plugin, configName) 
                : null;
        }

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <typeparam name="T">The type to get the configuration of</typeparam>
        /// <param name="plugin"></param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ConfigurationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope? TryGetConfigForType<T>(this PluginBase plugin)
        {
            return TryGetConfigForType(plugin, typeof(T));
        }

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="type">The type to get configuration data for</param>
        /// <returns>A <see cref="IConfigScope"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope GetConfigForType(this PluginBase plugin, Type type)
        {
            return TryGetConfigForType(plugin, type)
                ?? throw new ConfigurationException($"Missing required configuration key for type {type.Name}");
        }

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <typeparam name="T">The type to get the configuration of</typeparam>
        /// <param name="plugin"></param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ConfigurationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope GetConfigForType<T>(this PluginBase plugin)
        {
            return GetConfigForType(plugin, typeof(T));
        }

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements with the specified property name.
        /// </summary>
        /// <remarks>
        /// Search order: Plugin config, fall back to host config, throw if not found
        /// </remarks>
        /// <param name="plugin"></param>
        /// <param name="propName">The config property name to retrieve</param>
        /// <returns>A <see cref="IConfigScope"/> of top level configuration elements for the type</returns>
        /// <exception cref="ConfigurationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope GetConfig(this PluginBase plugin, string propName)
        {      
            return TryGetConfig(plugin, propName) 
                ?? throw new ConfigurationException($"Missing required top level configuration object '{propName}', in host/plugin configuration files");
        }
      

        /// <summary>
        /// Gets a required configuration property from the specified configuration scope
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the property to get</param>
        /// <param name="getter">A function to get the value from the json type</param>
        /// <returns>The property value</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static T? GetProperty<T>(this IConfigScope config, string property, Func<JsonElement, T> getter)
        {
            //Check null
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(getter);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);
            return !config.TryGetValue(property, out JsonElement el) 
                ? default 
                : getter(el);
        }

        /// <summary>
        /// Gets a required configuration property from the specified configuration scope
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the property to get</param>
        /// <param name="getter">A function to get the value from the json type</param>
        /// <returns>The property value</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ConfigurationException"></exception>
        public static T GetRequiredProperty<T>(this IConfigScope config, string property, Func<JsonElement, T> getter)
        {
            //Check null
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(getter);

            //Get the property
            bool hasValue = config.TryGetValue(property, out JsonElement el);
            Validate.Assert(hasValue, $"Missing required configuration property '{property}' in config {config.ScopeName}");

            T? value = getter(el);
            Validate.Assert(value is not null, $"Missing required configuration property '{property}' in config {config.ScopeName}");

            //Attempt to validate if the configuration inherits the interface
            TryValidateConfig(value);

            return value;
        }

        /// <summary>
        /// Gets a required configuration property from the specified configuration scope
        /// and deserializes the json type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the property to get</param>
        /// <returns>The property value deserialzied into the desired object</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ConfigurationException"></exception>
        public static T GetRequiredProperty<T>(this IConfigScope config, string property)
        {
            return GetRequiredProperty(
                config, 
                property, 
                static p => p.Deserialize<T>()!
            );
        }

        /// <summary>
        /// Attempts to get a configuration property from the specified configuration scope
        /// and invokes your callback function on the element if found to transform the 
        /// output value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="getter">The function used to set the desired value from the config element</param>
        /// <param name="value">The output value to set</param>
        /// <returns>A value that indicates if the property was found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryGetProperty<T>(this IConfigScope config, string property, Func<JsonElement, T> getter, out T? value)
        {
            //Check null
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(getter);

            //Get the property
            if (config.TryGetValue(property, out JsonElement el))
            {
                //Safe to invoke callback function on the element and set the return value
                value = getter(el);
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to get a configuration property from the specified configuration scope
        /// and deserializes the json type.
        /// output value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="value">The output value to set</param>
        /// <returns>A value that indicates if the property was found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryGetProperty<T>(this IConfigScope config, string property, out T? value)
        {
            return TryGetProperty(
                config, 
                property, 
                static p => p.Deserialize<T>(), 
                out value
            );
        }

        /// <summary>
        /// Attempts to get a configuration property from the specified configuration scope
        /// and returns the string value if found, or null if not found.
        /// output value
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="value">The output value to set</param>
        /// <returns>A value that indicates if the property was found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryGetProperty(this IConfigScope config, string property, out string? value)
        {
            return TryGetProperty(
                config, 
                property, 
                static p => p.GetString(), 
                out value
            );
        }

        /// <summary>
        /// Attempts to get a configuration property from the specified configuration scope
        /// and returns the int32 value if found, or null if not found.
        /// output value
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="value">The output value to set</param>
        /// <returns>A value that indicates if the property was found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryGetProperty(this IConfigScope config, string property, out int? value)
        {
            return TryGetProperty(
                config, 
                property, 
                static p => p.GetInt32(), 
                out value
            );
        }

        /// <summary>
        /// Attempts to get a configuration property from the specified configuration scope
        /// and returns the uint32 value if found, or null if not found.
        /// output value
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="value">The output value to set</param>
        /// <returns>A value that indicates if the property was found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryGetProperty(this IConfigScope config, string property, out uint? value)
        {
            return TryGetProperty(
                config, 
                property, 
                static p => p.GetUInt32(), 
                out value
            );
        }

        /// <summary>
        /// Attempts to get a configuration property from the specified configuration scope
        /// and returns the boolean value if found, or null if not found.
        /// output value
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="value">The output value to set</param>
        /// <returns>A value that indicates if the property was found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryGetProperty(this IConfigScope config, string property, out bool? value)
        {
            return TryGetProperty(
                config, 
                property, 
                static p => p.GetBoolean(), 
                out value
            );
        }

        /// <summary>
        /// Gets a configuration property from the specified configuration scope
        /// and invokes your callback function on the element if found to transform the
        /// output value, or returns the default value if the property is not found.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="getter">The function used to set the desired value from the config element</param>
        /// <param name="defaultValue">The default value to return</param>
        /// <returns>The property value returned from your getter callback, or the default value if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [return:NotNullIfNotNull(nameof(defaultValue))]
        public static T? GetValueOrDefault<T>(this IConfigScope config, string property, Func<JsonElement, T> getter, T defaultValue)
        {
            return TryGetProperty(config, property, getter, out T? value) ? value : defaultValue;
        }

        /// <summary>
        /// Gets a configuration property from the specified configuration scope
        /// and invokes your callback function on the element if found to transform the
        /// output value, or returns the default value if the property is not found.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="defaultValue">The default value to return</param>
        /// <returns>The property value returned from your getter callback, or the default value if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [return: NotNullIfNotNull(nameof(defaultValue))]
        public static T? GetValueOrDefault<T>(this IConfigScope config, string property, T defaultValue)
        {
            return GetValueOrDefault(
                config, 
                property, 
                static p => p.Deserialize<T>(), 
                defaultValue
            );
        }

        /// <summary>
        /// Gets a configuration property from the specified configuration scope
        /// and deserializes the json element if found, or returns the default value 
        /// if the property is not found.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="defaultValue">The default value to return</param>
        /// <returns>The property value returned from your getter callback, or the default value if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [return: NotNullIfNotNull(nameof(defaultValue))]
        public static string? GetValueOrDefault(this IConfigScope config, string property, string defaultValue)
        {
            return GetValueOrDefault(
                config,
                property,
                static p => p.GetString(),
                defaultValue
            );
        }

        /// <summary>
        /// Gets a configuration property of type int32 from the specified configuration 
        /// scope and, or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="defaultValue">The default value to return</param>
        /// <returns>The property value returned from your getter callback, or the default value if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static int GetValueOrDefault(this IConfigScope config, string property, int defaultValue)
        {
            return GetValueOrDefault(
                config,
                property,
                static p => p.GetInt32(),
                defaultValue
            );
        }

        /// <summary>
        /// Gets a configuration property of type uint32 from the specified configuration 
        /// scope and, or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="defaultValue">The default value to return</param>
        /// <returns>The property value returned from your getter callback, or the default value if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static uint GetValueOrDefault(this IConfigScope config, string property, uint defaultValue)
        {
            return GetValueOrDefault(
                config,
                property,
                static p => p.GetUInt32(),
                defaultValue
            );
        }

        /// <summary>
        /// Gets a configuration property of type boolean from the specified configuration 
        /// scope and, or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="property">The name of the configuration element to get</param>
        /// <param name="defaultValue">The default value to return</param>
        /// <returns>The property value returned from your getter callback, or the default value if not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool GetValueOrDefault(this IConfigScope config, string property, bool defaultValue)
        {
            return GetValueOrDefault(
                config,
                property,
                static p => p.GetBoolean(),
                defaultValue
            );
        }

        /// <summary>
        /// Gets the configuration property name for the type
        /// </summary>
        /// <param name="type">The type to get the configuration name for</param>
        /// <returns>The configuration property element name</returns>
        public static string? GetConfigNameForType(Type type)
        {
            //Get config name attribute from plugin type
            return type.GetCustomAttribute<ConfigurationNameAttribute>()?.ConfigVarName;
        }

        /// <summary>
        /// Determines if the type requires a configuration element.
        /// </summary>
        /// <param name="type">The type to determine config required status</param>
        /// <returns>
        /// True if the configuration is required, or false if the <see cref="ConfigurationNameAttribute"/> 
        /// was not declared, or <see cref="ConfigurationNameAttribute.Required"/> is false
        /// </returns>
        public static bool ConfigurationRequired(Type type)
        {
            return type.GetCustomAttribute<ConfigurationNameAttribute>()?.Required ?? false;
        }

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

        /// <summary>
        /// Shortcut extension for <see cref="GetConfigForType{T}(PluginBase)"/> to get 
        /// config of current class
        /// </summary>
        /// <param name="obj">The object that a configuration can be retrieved for</param>
        /// <param name="plugin">The plugin containing configuration variables</param>
        /// <returns>A <see cref="IConfigScope"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IConfigScope GetConfig(this PluginBase plugin, object obj)
        {
            Type t = obj.GetType();
            return plugin.GetConfigForType(t);
        }

        /// <summary>
        /// Deserialzes the configuration to the desired object and calls its
        /// <see cref="IOnConfigValidation.OnValidate"/> method. Validation exceptions 
        /// are wrapped in a <see cref="ConfigurationValidationException"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="scope"></param>
        /// <returns></returns>
        /// <exception cref="ConfigurationValidationException"></exception>
        public static T DeserialzeAndValidate<T>(this IConfigScope scope) where T : IOnConfigValidation
        {
            T conf = scope.Deserialze<T>();
            
            TryValidateConfig(conf);

            return conf;
        }

        /// <summary>
        /// Determines if the current plugin configuration contains the require properties to initialize 
        /// the type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <returns>True if the plugin config contains the require configuration property</returns>
        public static bool HasConfigForType<T>(this PluginBase plugin) => HasConfigForType(plugin, typeof(T));

        /// <summary>
        /// Determines if the current plugin configuration contains the require properties to initialize 
        /// the type
        /// </summary>
        /// <param name="type">The type to get the configuration for</param>
        /// <param name="plugin"></param>
        /// <returns>True if the plugin config contains the require configuration property</returns>
        public static bool HasConfigForType(this PluginBase plugin, Type type)
        {
            ConfigurationNameAttribute? configName = type.GetCustomAttribute<ConfigurationNameAttribute>();
            //See if the plugin contains a configuration varables
            return configName != null && (
                    plugin.PluginConfig.TryGetProperty(configName.ConfigVarName, out _) ||
                    plugin.HostConfig.TryGetProperty(configName.ConfigVarName, out _)
                );
        }

        /// <summary>
        /// Gets a given configuration element from the global configuration scope
        /// and deserializes it into the desired type. 
        /// <para>
        /// If the type inherits <see cref="IOnConfigValidation"/> the <see cref="IOnConfigValidation.OnValidate"/>
        /// method is invoked, and exceptions are warpped in <see cref="ConfigurationValidationException"/>
        /// </para>
        /// <para>
        /// If the type inherits <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
        /// method is called by the service scheduler
        /// </para>
        /// </summary>
        /// <typeparam name="TConfig">The configuration type</typeparam>
        /// <param name="plugin"></param>
        /// <returns>The deserialzed configuration element</returns>
        /// <exception cref="ConfigurationValidationException"></exception>
        public static TConfig GetConfigElement<TConfig>(this PluginBase plugin)
        {
            //Deserialze the element
            TConfig config = plugin.GetConfigForType<TConfig>()
                .Deserialze<TConfig>();

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
        /// method is invoked, and exceptions are warpped in <see cref="ConfigurationValidationException"/>
        /// </para>
        /// <para>
        /// If the type inherits <see cref="IAsyncConfigurable"/> the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
        /// method is called by the service scheduler
        /// </para>
        /// </summary>
        /// <typeparam name="TConfig">The configuration type</typeparam>
        /// <param name="plugin"></param>
        /// <param name="elementName">The configuration element name override</param>
        /// <returns>The deserialzed configuration element</returns>
        /// <exception cref="ConfigurationValidationException"></exception>
        public static TConfig GetConfigElement<TConfig>(this PluginBase plugin, string elementName)
        {
            //Deserialze the element
            TConfig config = plugin.GetConfig(elementName)
                .Deserialze<TConfig>();

            TryValidateConfig(config);

            //If async config, load async
            if (config is IAsyncConfigurable ac)
            {
                _ = plugin.ConfigureServiceAsync(ac);
            }

            return config;
        }

        private static void TryValidateConfig<TConfig>(TConfig config)
        {
            //If the type is validatable, validate it
            if (config is IOnConfigValidation conf)
            {
                try
                {
                    conf.OnValidate();
                }
                catch (Exception ex)
                {
                    throw new ConfigurationValidationException($"Configuration validation failed for type {typeof(TConfig).Name}", ex);
                }
            }
        }

        /// <summary>
        /// Attempts to load the basic S3 configuration variables required
        /// for S3 client access
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The S3 configuration object found in the plugin/host configuration</returns>
        public static S3Config? TryGetS3Config(this PluginBase plugin)
        {
            //Try get the config
            IConfigScope? s3conf = plugin.TryGetConfig(S3_CONFIG);
            return s3conf?.Deserialze<S3Config>();
        }

        /// <summary>
        /// Trys to get the optional assets directory from the plugin configuration
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The absolute path to the assets directory if defined, null otherwise</returns>
        public static string? GetAssetsPath(this PluginBase plugin)
        { 
            //Get global plugin config element
            IConfigScope config = plugin.GetConfig(PLUGINS_HOST_KEY);

            //Try to get the assets path if its defined
            string? assetsPath = config.GetPropString(PLUGIN_ASSET_KEY);

            //Try to get the full path for the assets if we can
            return assetsPath != null ? Path.GetFullPath(assetsPath) : null;
        }

        /// <summary>
        /// Gets the absolute path to the plugins directory as defined in the host configuration
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The absolute path to the directory containing all plugins</returns>
        public static string[] GetPluginSearchDirs(this PluginBase plugin)
        {
            //Get global plugin config element
            IConfigScope config = plugin.GetConfig(PLUGINS_HOST_KEY);

            /*
             * Hosts are allowed to define mutliple plugin loading paths. A
             * single path is supported for compat. Multi path takes precidence 
             * of course so attempt to load a string array first
             */

            if (!config.TryGetValue("paths", out JsonElement searchPaths)
                && !config.TryGetValue("path", out searchPaths))
            {
                return [];
            }

            switch (searchPaths.ValueKind)
            {
                case JsonValueKind.Array:

                    //Get the plugins path or throw because it should ALWAYS be defined if this method is called
                    return searchPaths.EnumerateArray()
                        .Select(static p => p.GetString()!)
                        .Select(Path.GetFullPath)   //Get absolute file paths
                        .ToArray();

                case JsonValueKind.String:
                    return [ Path.GetFullPath(searchPaths.GetString()!) ];

                default:
                    return [];
            }          
        }
    }
}
