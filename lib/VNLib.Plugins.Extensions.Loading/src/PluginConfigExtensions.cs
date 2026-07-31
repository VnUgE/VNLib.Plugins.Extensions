/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginConfigExtensions.cs 
*
* PluginConfigExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using VNLib.Utils.Extensions;

/*
 * TODO: (03-23-2026) 
 * Im preparing release 0.2.0 and that is a big breaking release. However currently there are already a bunch of
 * breaking namespace changes, and for now there is enough usage of the .Config() type for nearly all extensions,
 * we will keep the Loading namespace as the default. 
 * 
 * Consider moving to Loading.Config namespace in v0.3.0
 */

namespace VNLib.Plugins.Extensions.Loading
{
    using Configuration;

    /// <summary>
    /// Provides extension methods for plugin configuration.
    /// </summary>
    public static class PluginConfigExtensions
    {
        /// <summary>
        /// Creates a <see cref="PluginConfigStore"/> for the given plugin, providing
        /// convenient access to the plugin's configuration data.
        /// </summary>
        /// <remarks>
        /// This extension method is the primary entry point for accessing plugin configuration.
        /// It wraps the plugin instance in a configuration store struct that provides
        /// a comprehensive set of methods for retrieving and managing configuration data.
        /// The returned struct is lightweight (readonly struct) and should be used inline
        /// or stored in a local variable, rather than kept as a long-lived field.
        /// </remarks>
        /// <param name="plugin">The plugin instance to create a configuration store for</param>
        /// <returns>A <see cref="PluginConfigStore"/> struct for accessing the plugin's configuration</returns>
        /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is <see langword="null"/>.</exception>
        public static PluginConfigStore Config(this PluginBase plugin) => new(plugin);

        /// <summary>
        /// Gets a configuration property from the specified configuration scope.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the property to get.</param>
        /// <param name="getter">A function that extracts the value from the JSON element.</param>
        /// <returns>The property value, or the default value for <typeparamref name="T"/> if the property is not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/>, <paramref name="property"/>, or <paramref name="getter"/> is <see langword="null"/>.</exception>
        public static T? GetProperty<T>(this IConfigScope config, string property, Func<JsonElement, T> getter)
        {            
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(getter);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);

            return !config.TryGetValue(property, out JsonElement el)
                ? default
                : getter(el);
        }

        /// <summary>
        /// Gets a required configuration property from the specified configuration scope.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the property to get.</param>
        /// <param name="getter">A function that extracts the value from the JSON element.</param>
        /// <returns>The property value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/>, <paramref name="property"/>, or <paramref name="getter"/> is <see langword="null"/>.</exception>
        /// <exception cref="ConfigurationException">The specified property is not found or the value is <see langword="null"/>.</exception>
        public static T GetRequiredProperty<T>(this IConfigScope config, string property, Func<JsonElement, T> getter)
        {            
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(getter);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);

            //Get the property
            bool hasValue = config.TryGetValue(property, out JsonElement el);
            Validate.Assert(hasValue, $"Missing required configuration property '{property}' in config {config.ScopeName}");

            T? value = getter(el);
            Validate.Assert(value is not null, $"Required configuration property '{property}' returned a null value in config {config.ScopeName}");

            //Attempt to validate if the configuration inherits the interface
            PluginConfigStore.TryValidateConfig(value);

            return value;
        }

        /// <summary>
        /// Gets a required configuration property from the specified configuration scope and deserializes the JSON value.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the property value into.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the property to get.</param>
        /// <returns>The property value deserialized into the desired type.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
        /// <exception cref="ConfigurationException">The specified property is not found or the value is <see langword="null"/>.</exception>
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
        /// and invokes the callback function on the element if found to transform the output value.
        /// </summary>
        /// <typeparam name="T">The type of the transformed value.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="getter">A function that transforms the JSON element into the desired value.</param>
        /// <param name="value">The transformed value if the property was found; otherwise, the default value for <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/>, <paramref name="property"/>, or <paramref name="getter"/> is <see langword="null"/>.</exception>
        public static bool TryGetProperty<T>(this IConfigScope config, string property, Func<JsonElement, T> getter, out T? value)
        {
            //Check null
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(getter);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);

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
        /// Attempts to get a configuration property from the specified configuration scope and deserializes the JSON value.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the property value into.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="value">The deserialized value if the property was found; otherwise, the default value for <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Attempts to get a configuration property from the specified configuration scope as a string value.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="value">The string value if the property was found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Attempts to get a configuration property from the specified configuration scope as an <see cref="int"/> value.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="value">The integer value if the property was found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Attempts to get a configuration property from the specified configuration scope as a <see cref="uint"/> value.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="value">The unsigned integer value if the property was found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Attempts to get a configuration property from the specified configuration scope as a <see cref="bool"/> value.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="value">The boolean value if the property was found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// and invokes the callback function on the element if found to transform the output value,
        /// or returns the default value if the property is not found.
        /// </summary>
        /// <typeparam name="T">The type of the transformed value.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="getter">A function that transforms the JSON element into the desired value.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The property value returned from the getter callback, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/>, <paramref name="property"/>, or <paramref name="getter"/> is <see langword="null"/>.</exception>
        [return: NotNullIfNotNull(nameof(defaultValue))]
        public static T? GetValueOrDefault<T>(this IConfigScope config, string property, Func<JsonElement, T> getter, T defaultValue)
        {
            return TryGetProperty(config, property, getter, out T? value) ? value : defaultValue;
        }

        /// <summary>
        /// Gets a configuration property from the specified configuration scope
        /// and deserializes the JSON element if found, or returns the default value if the property is not found.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the property value into.</typeparam>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The deserialized property value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Gets a configuration property from the specified configuration scope as a string,
        /// or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The string property value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Gets a configuration property of type <see cref="int"/> from the specified configuration scope,
        /// or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The integer property value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Gets a configuration property of type <see cref="uint"/> from the specified configuration scope,
        /// or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The unsigned integer property value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Gets a configuration property of type <see cref="bool"/> from the specified configuration scope,
        /// or returns the default value if the property is not found.
        /// </summary>
        /// <param name="config">The configuration scope to read from.</param>
        /// <param name="property">The name of the configuration element to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The boolean property value, or <paramref name="defaultValue"/> if not found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
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
        /// Deserializes the configuration to the desired object and calls its
        /// <see cref="IOnConfigValidation.OnValidate"/> method. Validation exceptions
        /// are wrapped in a <see cref="ConfigurationValidationException"/>.
        /// </summary>
        /// <typeparam name="T">The type of the configuration object to deserialize.</typeparam>
        /// <param name="scope">The configuration scope to deserialize from.</param>
        /// <returns>The deserialized and validated configuration object.</returns>
        /// <exception cref="ConfigurationValidationException">Configuration validation fails.</exception>
        public static T DeserializeAndValidate<T>(this IConfigScope scope) where T : IOnConfigValidation
        {
            T conf = scope.Deserialize<T>();

            PluginConfigStore.TryValidateConfig(conf);

            return conf;
        }

        /// <summary>
        /// Deserializes the configuration to the desired object and calls its
        /// <see cref="IOnConfigValidation.OnValidate"/> method. Validation exceptions
        /// are wrapped in a <see cref="ConfigurationValidationException"/>.
        /// </summary>
        /// <typeparam name="T">The type of the configuration object to deserialize.</typeparam>
        /// <param name="scope">The configuration scope to deserialize from.</param>
        /// <returns>The deserialized and validated configuration object.</returns>
        /// <exception cref="ConfigurationValidationException">Configuration validation fails.</exception>
        [Obsolete("This method has been renamed to DeserializeAndValidate. Please use DeserializeAndValidate instead.")]
        public static T DeserialzeAndValidate<T>(this IConfigScope scope) where T : IOnConfigValidation 
            => DeserializeAndValidate<T>(scope);

        /// <summary>
        /// A lightweight readonly structure that provides comprehensive access to plugin configuration data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="PluginConfigStore"/> is the primary façade for interacting with plugin and host configuration.
        /// This struct is designed to be created inline via the <see cref="PluginConfigExtensions.Config(PluginBase)"/> 
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
        /// <exception cref="ArgumentNullException">If the <paramref name="plugin"/> argument is null</exception>
        public readonly ref struct PluginConfigStore(PluginBase plugin)
        {
            public const string S3_CONFIG = "s3_config";
            public const string S3_SECRET_KEY = "s3_secret";
            public const string PLUGIN_ASSET_KEY = "assets";
            public const string PLUGINS_HOST_KEY = "plugins";

            private readonly void TryConfigureAsync<TConfig>(TConfig config)
            {
                /* 
                 * If the config supports async initialization, schedule it on the
                 * plugin's task scheduler. The plugin's lifecycle controller observes
                 * the task, so we don't need to await it here. 
                 */
                
                if (config is IAsyncConfigurable ac)
                {
                    _ = plugin
                        .Tasks()
                        .ConfigureServiceAsync(ac);
                }
            }         

            /// <summary>
            /// Retrieves a top-level configuration scope with the specified property name,
            /// or <see langword="null"/> if no configuration is found.
            /// </summary>
            /// <remarks>
            /// Search order: Plugin config, fall back to host config, <see langword="null"/> if not found.
            /// </remarks>
            /// <param name="propName">The configuration property name to retrieve.</param>
            /// <returns>An <see cref="IConfigScope"/> for the specified property, or <see langword="null"/> if not found.</returns>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly IConfigScope? TryGet(string propName)
            {
                plugin.ThrowIfUnloaded();

                // Try to get the element from the plugin config first, or fallback to host
                if
                (
                    plugin.PluginConfig.TryGetProperty(propName, out JsonElement el) ||
                    plugin.HostConfig.TryGetProperty(propName, out el)
                )
                {
                    // Get the top level config as a dictionary
                    return new ConfigScope(el, propName);
                }
                // No config found
                return null;
            }

            /// <summary>
            /// Retrieves a top-level configuration scope with the specified property name.
            /// </summary>
            /// <remarks>
            /// Search order: Plugin config, fall back to host config; throws if not found.
            /// </remarks>
            /// <param name="propName">The configuration property name to retrieve.</param>
            /// <returns>An <see cref="IConfigScope"/> for the specified property.</returns>
            /// <exception cref="ConfigurationException">The specified configuration property is not found.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly IConfigScope Get(string propName)
            {
                return TryGet(propName)
                    ?? throw new ConfigurationException($"Missing required top level configuration object '{propName}', in host/plugin configuration files");
            }

            /// <summary>
            /// Retrieves a top-level configuration scope for the specified type.
            /// The type must be decorated with a <see cref="ConfigurationNameAttribute"/>.
            /// </summary>
            /// <param name="type">The class type to get the configuration scope for.</param>
            /// <returns>An <see cref="IConfigScope"/> for the desired top-level configuration scope, or <see langword="null"/> if not found.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly IConfigScope? TryGetForType(Type type)
            {
                ArgumentNullException.ThrowIfNull(type);

                string? configName = GetConfigNameForType(type);

                return configName != null
                    ? TryGet(configName)
                    : null;
            }

            /// <inheritdoc cref="TryGetForType(Type)"/>
            /// <typeparam name="T">The type to get the configuration scope for.</typeparam>
            /// <returns>An <see cref="IConfigScope"/> for the type, or <see langword="null"/> if not found.</returns>
            public readonly IConfigScope? TryGetForType<T>()
                => TryGetForType(typeof(T));

            /// <summary>
            /// Retrieves a top-level configuration scope for the specified type.
            /// The type must be decorated with a <see cref="ConfigurationNameAttribute"/>.
            /// </summary>
            /// <param name="type">The type to get configuration data for.</param>
            /// <returns>An <see cref="IConfigScope"/> for the type.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
            /// <exception cref="ConfigurationException">Configuration for the specified type is not found.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly IConfigScope GetForType(Type type)
            {
                return TryGetForType(type)
                    ?? throw new ConfigurationException($"Missing required configuration key for type {type.Name}");
            }

            /// <inheritdoc cref="GetForType(Type)"/>
            /// <typeparam name="T">The type to get the configuration scope for.</typeparam>
            /// <returns>An <see cref="IConfigScope"/> for the type.</returns>
            public readonly IConfigScope GetForType<T>()
                => GetForType(typeof(T));

            /// <summary>
            /// Gets a configuration scope for the specified object's type using <see cref="TryGetForType{T}()"/>.
            /// </summary>
            /// <param name="obj">The object whose type is used to retrieve a configuration scope.</param>
            /// <returns>
            /// An <see cref="IConfigScope"/> for the object's type if found; otherwise, <see langword="null"/>.
            /// </returns>
            /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly IConfigScope? TryGetFor(object obj)
            {
                ArgumentNullException.ThrowIfNull(obj);
                return TryGetForType(obj.GetType());
            }

            /// <summary>
            /// Gets a configuration scope for the specified object's type using <see cref="GetForType{T}()"/>.
            /// </summary>
            /// <param name="obj">The object whose type is used to retrieve a configuration scope.</param>
            /// <returns>An <see cref="IConfigScope"/> for the object's type.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/>.</exception>
            /// <exception cref="ConfigurationException">Configuration for the object's type is not found.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly IConfigScope GetFor(object obj)
            {
                ArgumentNullException.ThrowIfNull(obj);
                return GetForType(obj.GetType());
            }

            /// <summary>
            /// Attempts to get a configuration element from the global configuration scope
            /// and deserialize it into the desired type.
            /// </summary>
            /// <typeparam name="TConfig">The configuration type to deserialize.</typeparam>
            /// <returns>The deserialized configuration element if found; otherwise, <see langword="null"/>.</returns>
            /// <remarks>
            /// <para>
            /// If the type implements <see cref="IOnConfigValidation"/>, the <see cref="IOnConfigValidation.OnValidate"/>
            /// method is invoked, and exceptions are wrapped in <see cref="ConfigurationValidationException"/>.
            /// </para>
            /// <para>
            /// If the type implements <see cref="IAsyncConfigurable"/>, the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
            /// method is called by the service scheduler.
            /// </para>
            /// </remarks>
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
                TryConfigureAsync(config);

                return config;
            }

            /// <summary>
            /// Gets a configuration element from the global configuration scope
            /// and deserializes it into the desired type.
            /// </summary>
            /// <typeparam name="TConfig">The configuration type to deserialize.</typeparam>
            /// <returns>The deserialized configuration element.</returns>
            /// <exception cref="ConfigurationException">The configuration element is not found.</exception>
            /// <exception cref="ConfigurationValidationException">Configuration validation fails.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            /// <remarks>
            /// <para>
            /// If the type implements <see cref="IOnConfigValidation"/>, the <see cref="IOnConfigValidation.OnValidate"/>
            /// method is invoked, and exceptions are wrapped in <see cref="ConfigurationValidationException"/>.
            /// </para>
            /// <para>
            /// If the type implements <see cref="IAsyncConfigurable"/>, the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
            /// method is called by the service scheduler.
            /// </para>
            /// </remarks>
            public readonly TConfig GetElement<TConfig>()
            {
                //Deserialize the element
                TConfig config = GetForType<TConfig>().Deserialize<TConfig>();

                TryValidateConfig(config);

                //If async config, load async
                TryConfigureAsync(config);

                return config;
            }

            /// <summary>
            /// Gets a configuration element with the specified name from the global configuration scope
            /// and deserializes it into the desired type.
            /// </summary>
            /// <typeparam name="TConfig">The configuration type to deserialize.</typeparam>
            /// <param name="elementName">The configuration element name override.</param>
            /// <returns>The deserialized configuration element.</returns>
            /// <exception cref="ConfigurationException">The specified configuration element is not found.</exception>
            /// <exception cref="ConfigurationValidationException">Configuration validation fails.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            /// <remarks>
            /// <para>
            /// If the type implements <see cref="IOnConfigValidation"/>, the <see cref="IOnConfigValidation.OnValidate"/>
            /// method is invoked, and exceptions are wrapped in <see cref="ConfigurationValidationException"/>.
            /// </para>
            /// <para>
            /// If the type implements <see cref="IAsyncConfigurable"/>, the <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/>
            /// method is called by the service scheduler.
            /// </para>
            /// </remarks>
            public readonly TConfig GetElement<TConfig>(string elementName)
            {
                //Deserialize the element
                TConfig config = Get(elementName).Deserialize<TConfig>();

                TryValidateConfig(config);

                //If async config, load async
                TryConfigureAsync(config);

                return config;
            }


            /// <summary>
            /// Determines whether the current plugin configuration contains the required properties to initialize the specified type.
            /// </summary>
            /// <typeparam name="T">The type to check for configuration.</typeparam>
            /// <returns><see langword="true"/> if the plugin config contains the required configuration property; otherwise, <see langword="false"/>.</returns>
            public readonly bool HasForType<T>()
                => HasForType(typeof(T));

            /// <inheritdoc cref="HasForType{T}()"/>
            /// <param name="type">The type to check for configuration.</param>
            /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
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
            /// Attempts to load the basic S3 configuration variables required for S3 client access.
            /// </summary>
            /// <returns>The S3 configuration object found in the plugin/host configuration, or <see langword="null"/> if not found.</returns>
            [Obsolete("This method is deprecated, S3 built-in config support is being removed")]
            public readonly S3Config? TryGetS3()
                => TryGet(S3_CONFIG)?.Deserialize<S3Config>();

            /// <summary>
            /// Loads the basic S3 configuration variables required for S3 client access.
            /// </summary>
            /// <returns>The S3 configuration object found in the plugin/host configuration.</returns>
            /// <exception cref="ConfigurationException">The S3 configuration is not found.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            [Obsolete("This method is deprecated, S3 built-in config support is being removed")]
            public readonly S3Config GetS3()
                => Get(S3_CONFIG).Deserialize<S3Config>();

            /// <summary>
            /// Attempts to get the optional assets directory from the plugin configuration.
            /// </summary>
            /// <returns>The absolute path to the assets directory if defined; otherwise, <see langword="null"/>.</returns>
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
            /// Gets the assets directory from the plugin configuration.
            /// </summary>
            /// <returns>The absolute path to the assets directory.</returns>
            /// <exception cref="ConfigurationException">The plugins configuration section or assets path is not configured.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
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
            /// Attempts to get the absolute paths to the plugin directories as defined in the host configuration.
            /// </summary>
            /// <returns>The absolute paths to directories containing plugins, or an empty array if not configured.</returns>
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
                    !config.TryGetValue("paths", out JsonElement searchPathEl) &&
                    !config.TryGetValue("path", out searchPathEl))
                {
                    return [];
                }

                // Element may be array or a single string
                switch (searchPathEl.ValueKind)
                {
                    case JsonValueKind.Array:
                        return searchPathEl.EnumerateArray()
                            .Select(static p =>
                            {
                                string? path = p.GetString();
                                Validate.NotNull(path, $"Plugins {PLUGINS_HOST_KEY}.paths array contains a null or empty element");
                                return Path.GetFullPath(path);
                            })
                            .ToArray();

                    case JsonValueKind.String:
                        return [Path.GetFullPath(searchPathEl.GetString()!)];

                    default:
                        return [];
                }
            }

            /// <summary>
            /// Gets the absolute paths to the plugin directories as defined in the host configuration.
            /// </summary>
            /// <returns>The absolute paths to directories containing plugins.</returns>
            /// <exception cref="ConfigurationException">Plugin search paths are not configured.</exception>
            /// <exception cref="ObjectDisposedException">The plugin is unloaded.</exception>
            public readonly string[] GetPluginSearchDirs()
            {
                string[] paths = TryGetPluginSearchDirs();

                return paths.Length == 0
                    ? throw new ConfigurationException("Plugin search paths ('path' or 'paths') not configured in host configuration")
                    : paths;
            }

            /// <summary>
            /// Gets the full file path for the assembly asset file name within the assets directory.
            /// </summary>
            /// <param name="assemblyName">The name of the assembly file (e.g., 'file.dll') to search for.</param>
            /// <param name="searchOption">The directory search option to use.</param>
            /// <returns>The full path to the assembly asset file, or <see langword="null"/> if the file does not exist.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="assemblyName"/> is <see langword="null"/>.</exception>
            public readonly string? GetAssetFilePath(string assemblyName, SearchOption searchOption)
            {
                ArgumentNullException.ThrowIfNull(assemblyName);

                string[] searchDirs;

                /*
                 * Allow an assets directory to limit the scope of the search for the desired
                 * assembly, otherwise search all plugins directories
                 */

                string? assetDir = TryGetAssetsPath();

                searchDirs = assetDir is null
                    ? GetPluginSearchDirs()
                    : ([assetDir]);

                /*
                * This should never happen since this method can only be called from a
                * plugin context, which means this path was used to load the current plugin
                */
                if (searchDirs.Length == 0)
                {
                    throw new ConfigurationException("No plugin asset directory is defined for the current host configuration, this is likely a bug");
                }

                //Get the first file that matches the search file
                return searchDirs
                    .SelectMany(d => Directory.EnumerateFiles(d, assemblyName, searchOption))
                    .FirstOrDefault();
            }

            /// <summary>
            /// Gets the <see cref="ConfigurationNameAttribute"/> from the specified type, if present.
            /// </summary>
            /// <remarks>
            /// This is a helper method used internally to retrieve configuration metadata from a type.
            /// Most callers should use <see cref="GetConfigNameForType(Type)"/> or <see cref="ConfigurationRequired(Type)"/>
            /// instead, which provide more convenient access to the attribute's properties.
            /// </remarks>
            /// <param name="type">The type to inspect for the configuration attribute.</param>
            /// <returns>
            /// The <see cref="ConfigurationNameAttribute"/> if the type is decorated with one; otherwise, <see langword="null"/>.
            /// </returns>
            /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
            public static ConfigurationNameAttribute? GetConfigurationNameAttribute(Type type)
            {
                ArgumentNullException.ThrowIfNull(type);
                return type.GetCustomAttribute<ConfigurationNameAttribute>();
            }

            /// <summary>
            /// Gets the configuration property name for the specified type.
            /// </summary>
            /// <param name="type">The type to get the configuration name for.</param>
            /// <returns>The configuration property element name, or <see langword="null"/> if the type is not decorated with a <see cref="ConfigurationNameAttribute"/>.</returns>
            public static string? GetConfigNameForType(Type type)
                => GetConfigurationNameAttribute(type)?.ConfigVarName;

            /// <summary>
            /// Determines whether the specified type requires a configuration element.
            /// </summary>
            /// <param name="type">The type to check for a required configuration.</param>
            /// <returns>
            /// <see langword="true"/> if the configuration is required; otherwise, <see langword="false"/>
            /// if the <see cref="ConfigurationNameAttribute"/> was not declared or
            /// <see cref="ConfigurationNameAttribute.Required"/> is <see langword="false"/>.
            /// </returns>
            public static bool ConfigurationRequired(Type type)
                => GetConfigurationNameAttribute(type)?.Required ?? false;

            /// <summary>
            /// Throws a <see cref="ConfigurationException"/> with diagnostic information
            /// for missing configuration for a given type.
            /// </summary>
            /// <param name="type">The type to raise an exception for.</param>
            /// <exception cref="ConfigurationException">Always thrown; the configuration for the specified type is missing.</exception>
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
}
