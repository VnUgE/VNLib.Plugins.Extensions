using System;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;

using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Specifies a configuration variable name in the plugin's configuration 
    /// containing data specific to the type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigurationNameAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        public readonly string ConfigVarName;

        /// <summary>
        /// Initializes a new <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <param name="configVarName">The name of the configuration variable for the class</param>
        public ConfigurationNameAttribute(string configVarName)
        {
            ConfigVarName = configVarName;
        }
    }
    
    /// <summary>
    /// Contains extensions for plugin configuration specifc extensions
    /// </summary>
    public static class ConfigurationExtensions
    {
        public const string S3_CONFIG = "s3_config";
        public const string S3_SECRET_KEY = "s3_secret";

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <typeparam name="T">The type to get the configuration of</typeparam>
        /// <param name="plugin"></param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IReadOnlyDictionary<string, JsonElement> GetConfigForType<T>(this PluginBase plugin)
        {
            Type t = typeof(T);
            return plugin.GetConfigForType(t);
        }
        /// <summary>
        /// Retrieves a top level configuration dictionary of elements with the specified property name,
        /// from the plugin config first, or falls back to the host config file
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="propName">The config property name to retrieve</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IReadOnlyDictionary<string, JsonElement> GetConfig(this PluginBase plugin, string propName)
        {
            plugin.ThrowIfUnloaded();
            try
            {
                //Try to get the element from the plugin config first
                if (!plugin.PluginConfig.TryGetProperty(propName, out JsonElement el))
                {
                    //Fallback to the host config
                    el = plugin.HostConfig.GetProperty(propName);
                }
                //Get the top level config as a dictionary
                return el.EnumerateObject().ToDictionary(static k => k.Name, static k => k.Value);
            }
            catch(KeyNotFoundException)
            {
                throw new KeyNotFoundException($"Missing required top level configuration object '{propName}', in host/plugin configuration files");
            }
        }
        /// <summary>
        /// Retrieves a top level configuration dictionary of elements with the specified property name,
        /// from the plugin config first, or falls back to the host config file
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="propName">The config property name to retrieve</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IReadOnlyDictionary<string, JsonElement>? TryGetConfig(this PluginBase plugin, string propName)
        {
            plugin.ThrowIfUnloaded();
            //Try to get the element from the plugin config first, or fallback to host
            if (plugin.PluginConfig.TryGetProperty(propName, out JsonElement el) || plugin.HostConfig.TryGetProperty(propName, out el))
            {
                //Get the top level config as a dictionary
                return el.EnumerateObject().ToDictionary(static k => k.Name, static k => k.Value);
            }
            //No config found
            return null;
        }

        /// <summary>
        /// Retrieves a top level configuration dictionary of elements for the specified type.
        /// The type must contain a <see cref="ConfigurationNameAttribute"/>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="type">The type to get configuration data for</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IReadOnlyDictionary<string, JsonElement> GetConfigForType(this PluginBase plugin, Type type)
        {
            //Get config name attribute from plugin type
            ConfigurationNameAttribute? configName = type.GetCustomAttribute<ConfigurationNameAttribute>();
            return configName?.ConfigVarName == null
                ? throw new KeyNotFoundException("No configuration attribute set")
                : plugin.GetConfig(configName.ConfigVarName);
        }

        /// <summary>
        /// Shortcut extension for <see cref="GetConfigForType{T}(PluginBase)"/> to get 
        /// config of current class
        /// </summary>
        /// <param name="obj">The object that a configuration can be retrieved for</param>
        /// <param name="plugin">The plugin containing configuration variables</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> of top level configuration elements for the type</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IReadOnlyDictionary<string, JsonElement> GetConfig(this PluginBase plugin, object obj)
        {
            Type t = obj.GetType();
            return plugin.GetConfigForType(t);
        }

        /// <summary>
        /// Determines if the current plugin configuration contains the require properties to initialize 
        /// the type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="plugin"></param>
        /// <returns>True if the plugin config contains the require configuration property</returns>
        public static bool HasConfigForType<T>(this PluginBase plugin) 
        {
            Type type = typeof(T);
            ConfigurationNameAttribute? configName = type.GetCustomAttribute<ConfigurationNameAttribute>();
            //See if the plugin contains a configuration varables
            return configName != null ? plugin.PluginConfig.TryGetProperty(configName.ConfigVarName, out _) : false;
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
            IReadOnlyDictionary<string, JsonElement>? s3conf = plugin.TryGetConfig(S3_CONFIG);
            if(s3conf == null)
            {
                return null;
            }

            //Try get the elements
            return new()
            {
                BaseBucket = s3conf.GetPropString("bucket"),
                ClientId = s3conf.GetPropString("access_key"),
                ServerAddress = s3conf.GetPropString("server_address"),
                UseSsl = s3conf.TryGetValue("use_ssl", out JsonElement el) && el.GetBoolean(),
                ClientSecret = plugin.TryGetSecretAsync(S3_SECRET_KEY).Result,
                Region = s3conf.GetPropString("region"),
            };
        }
    }
}
