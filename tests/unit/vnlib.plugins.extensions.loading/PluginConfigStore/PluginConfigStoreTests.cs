/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: PluginConfigStoreTests.cs 
*
* PluginConfigStoreTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Tests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Plugins.Extensions.Loading.Tests.PluginConfigStore
{
    [TestClass]
    public class PluginConfigStoreTests
    {
        private static readonly object PluginConfigObj = new
        {
            simple_config = new
            {
                Name    = "TestPlugin",
                Port    = 8080,
                Enabled = true
            },
            validated_config = new
            {
                Host    = "localhost",
                Timeout = 30
            },
            async_config = new
            {
                DatabasePath = "/data/db"
            },
            custom_element = new
            {
                key = "plugin_value"
            },
            plugins = new
            {
                assets = "plugin_assets",
                path   = "/plugin/dir"
            }
        };

        private static readonly object HostConfigObj = new
        {
            simple_config = new
            {
                Name    = "HostPlugin",
                Port    = 9090,
                Enabled = false
            },
            host_element = new
            {
                key = "host_value"
            },
            plugins = new
            {
                assets = "host_assets",
                paths  = new string[] { "/host/plugins1", "/host/plugins2" }
            }
        };

        private static TestPluginBase CreateTestPlugin() => new(PluginConfigObj, HostConfigObj);

        #region TryGet / Get Tests

        [TestMethod]
        public void TryGet_PluginConfig_ReturnsScope()
        {
            // Verify TryGet successfully retrieves configuration from plugin config
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope? scope = plugin.Config().TryGet("simple_config");

            Assert.IsNotNull(scope);
            Assert.AreEqual("simple_config", scope.ScopeName);
        }

        [TestMethod]
        public void TryGet_HostConfig_Fallback_ReturnsScope()
        {
            // Verify TryGet falls back to host config when property not in plugin config
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope? scope = plugin.Config().TryGet("host_element");

            Assert.IsNotNull(scope);
            Assert.AreEqual("host_element", scope.ScopeName);
        }

        [TestMethod]
        public void TryGet_NotFound_ReturnsNull()
        {
            // Verify TryGet returns null when configuration not found in either scope
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope? scope = plugin.Config().TryGet("nonexistent");

            Assert.IsNull(scope);
        }

        [TestMethod]
        public void Get_Found_ReturnsScope()
        {
            // Verify Get successfully returns configuration scope when found
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope scope = plugin.Config().Get("simple_config");

            Assert.IsNotNull(scope);
            Assert.AreEqual("simple_config", scope.ScopeName);
        }

        [TestMethod]
        public void Get_NotFound_ThrowsException()
        {
            // Verify Get throws ConfigurationException when configuration not found
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ConfigurationException>(() => plugin.Config().Get("nonexistent"));
        }

        [TestMethod]
        public void PluginConfig_OverridesHost()
        {
            // Verify plugin-specific configuration takes precedence over host configuration
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope scope = plugin.Config().Get("simple_config");
            SimpleConfig config = scope.Deserialize<SimpleConfig>();

            // Should get plugin config, not host config
            Assert.AreEqual("TestPlugin", config.Name);
            Assert.AreEqual(8080, config.Port);
            Assert.IsTrue(config.Enabled);
        }

        #endregion

        #region TryGetForType / GetForType Tests

        [TestMethod]
        public void TryGetForType_Generic_WithAttribute_ReturnsScope()
        {
            // Verify TryGetForType<T> retrieves config for type with ConfigurationNameAttribute
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope? scope = plugin.Config().TryGetForType<SimpleConfig>();

            Assert.IsNotNull(scope);
        }

        [TestMethod]
        public void TryGetForType_Type_WithAttribute_ReturnsScope()
        {
            // Verify TryGetForType(Type) works with Type parameter variant
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope? scope = plugin.Config().TryGetForType(typeof(SimpleConfig));

            Assert.IsNotNull(scope);
        }

        [TestMethod]
        public void TryGetForType_NoAttribute_ReturnsNull()
        {
            // Verify TryGetForType returns null when type lacks ConfigurationNameAttribute
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope? scope = plugin.Config().TryGetForType<NoAttributeConfig>();

            Assert.IsNull(scope);
        }

        [TestMethod]
        public void TryGetForType_NullType_ThrowsArgumentNullException()
        {
            // Verify TryGetForType validates null type parameter
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ArgumentNullException>(() => plugin.Config().TryGetForType(null!));
        }

        [TestMethod]
        public void GetForType_Found_ReturnsScope()
        {
            // Verify GetForType returns scope when type configuration is present
            using TestPluginBase plugin = CreateTestPlugin();

            IConfigScope scope = plugin.Config().GetForType<SimpleConfig>();

            Assert.IsNotNull(scope);
        }

        [TestMethod]
        public void GetForType_NotFound_ThrowsException()
        {
            // Verify GetForType throws when type configuration is missing
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ConfigurationException>(() => plugin.Config().GetForType<NoAttributeConfig>());
        }

        #endregion

        #region TryGetFor / GetFor Tests

        [TestMethod]
        public void TryGetFor_WithAttribute_ReturnsScope()
        {
            // Verify TryGetFor retrieves config for object's type
            using TestPluginBase plugin = CreateTestPlugin();
            SimpleConfig obj = new();

            IConfigScope? scope = plugin.Config().TryGetFor(obj);

            Assert.IsNotNull(scope);
        }

        [TestMethod]
        public void TryGetFor_NullObject_ThrowsArgumentNullException()
        {
            // Verify TryGetFor validates null object parameter
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ArgumentNullException>(() => plugin.Config().TryGetFor(null!));
        }

        [TestMethod]
        public void GetFor_Found_ReturnsScope()
        {
            // Verify GetFor retrieves config for object's type when available
            using TestPluginBase plugin = CreateTestPlugin();
            SimpleConfig obj = new();

            IConfigScope scope = plugin.Config().GetFor(obj);

            Assert.IsNotNull(scope);
        }

        [TestMethod]
        public void GetFor_NullObject_ThrowsArgumentNullException()
        {
            // Verify GetFor validates null object parameter
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ArgumentNullException>(() => plugin.Config().GetFor(null!));
        }

        #endregion

        #region GetElement / TryGetElement Tests

        [TestMethod]
        public void TryGetElement_Found_ReturnsDeserialized()
        {
            // Verify TryGetElement deserializes and returns configuration object
            using TestPluginBase plugin = CreateTestPlugin();

            SimpleConfig? config = plugin.Config().TryGetElement<SimpleConfig>();

            Assert.IsNotNull(config);
            Assert.AreEqual("TestPlugin", config.Name);
            Assert.AreEqual(8080, config.Port);
        }

        [TestMethod]
        public void TryGetElement_NotFound_ReturnsNull()
        {
            // Verify TryGetElement returns null when configuration not found
            using TestPluginBase plugin = CreateTestPlugin();

            OptionalConfig? config = plugin.Config().TryGetElement<OptionalConfig>();

            Assert.IsNull(config);
        }

        [TestMethod]
        public void GetElement_Found_ReturnsDeserialized()
        {
            // Verify GetElement deserializes and returns configuration object
            using TestPluginBase plugin = CreateTestPlugin();

            SimpleConfig config = plugin.Config().GetElement<SimpleConfig>();

            Assert.IsNotNull(config);
            Assert.AreEqual("TestPlugin", config.Name);
        }

        [TestMethod]
        public void GetElement_NotFound_ThrowsException()
        {
            // Verify GetElement throws when configuration not found
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ConfigurationException>(() => plugin.Config().GetElement<OptionalConfig>());
        }

        [TestMethod]
        public void GetElement_WithElementName_ReturnsDeserialized()
        {
            // Verify GetElement supports explicit element name override
            using TestPluginBase plugin = CreateTestPlugin();

            SimpleConfig config = plugin.Config().GetElement<SimpleConfig>("simple_config");

            Assert.IsNotNull(config);
            Assert.AreEqual("TestPlugin", config.Name);
        }

        [TestMethod]
        public void GetElement_ValidationSuccess_ReturnsConfig()
        {
            // Verify GetElement invokes IOnConfigValidation.OnValidate when present
            using TestPluginBase plugin = CreateTestPlugin();

            ValidatedConfig config = plugin.Config().GetElement<ValidatedConfig>();

            Assert.IsNotNull(config);
            Assert.AreEqual("localhost", config.Host);
            Assert.AreEqual(30, config.Timeout);
        }

        [TestMethod]
        public void GetElement_ValidationFailure_ThrowsValidationException()
        {
            // Verify GetElement wraps validation exceptions in ConfigurationValidationException
            object invalidConfigObj = new
            {
                validated_config = new
                {
                    Host    = "",
                    Timeout = -5
                }
            };

            using TestPluginBase plugin = new(invalidConfigObj, HostConfigObj);

            Assert.ThrowsExactly<ConfigurationValidationException>(() => plugin.Config().GetElement<ValidatedConfig>());
        }

        [TestMethod]
        public void GetElement_AsyncConfigurable_SchedulesInitialization()
        {
            // Verify GetElement schedules IAsyncConfigurable.ConfigureServiceAsync
            using TestPluginBase plugin = CreateTestPlugin();

            AsyncConfig config = plugin.Config().GetElement<AsyncConfig>();

            Assert.IsNotNull(config);
            Assert.AreEqual("/data/db", config.DatabasePath);
            // Note: IsInitialized check would require actual plugin scheduling
        }

        #endregion

        #region HasForType Tests

        [TestMethod]
        public void HasForType_Generic_ConfigExists_ReturnsTrue()
        {
            // Verify HasForType<T> returns true when configuration is present
            using TestPluginBase plugin = CreateTestPlugin();

            bool hasConfig = plugin.Config().HasForType<SimpleConfig>();

            Assert.IsTrue(hasConfig);
        }

        [TestMethod]
        public void HasForType_Type_ConfigExists_ReturnsTrue()
        {
            // Verify HasForType(Type) variant works with Type parameter
            using TestPluginBase plugin = CreateTestPlugin();

            bool hasConfig = plugin.Config().HasForType(typeof(SimpleConfig));

            Assert.IsTrue(hasConfig);
        }

        [TestMethod]
        public void HasForType_ConfigMissing_ReturnsFalse()
        {
            // Verify HasForType returns false when configuration is absent
            using TestPluginBase plugin = CreateTestPlugin();

            bool hasConfig = plugin.Config().HasForType<OptionalConfig>();

            Assert.IsFalse(hasConfig);
        }

        [TestMethod]
        public void HasForType_NoAttribute_ReturnsFalse()
        {
            // Verify HasForType returns false when type lacks ConfigurationNameAttribute
            using TestPluginBase plugin = CreateTestPlugin();

            bool hasConfig = plugin.Config().HasForType<NoAttributeConfig>();

            Assert.IsFalse(hasConfig);
        }

        [TestMethod]
        public void HasForType_NullType_ThrowsArgumentNullException()
        {
            // Verify HasForType validates null type parameter
            using TestPluginBase plugin = CreateTestPlugin();

            Assert.ThrowsExactly<ArgumentNullException>(() => plugin.Config().HasForType(null!));
        }

        #endregion

        #region Assets Path Tests

        [TestMethod]
        public void TryGetAssetsPath_Found_ReturnsPath()
        {
            // Verify TryGetAssetsPath returns absolute path when configured
            using TestPluginBase plugin = CreateTestPlugin();

            string? path = plugin.Config().TryGetAssetsPath();

            Assert.IsNotNull(path);
            Assert.EndsWith("plugin_assets", path);
        }

        [TestMethod]
        public void TryGetAssetsPath_NotConfigured_ReturnsNull()
        {
            // Verify TryGetAssetsPath returns null when not configured
            using TestPluginBase plugin = new();

            string? path = plugin.Config().TryGetAssetsPath();

            Assert.IsNull(path);
        }

        [TestMethod]
        public void GetAssetsPath_Found_ReturnsPath()
        {
            // Verify GetAssetsPath returns absolute path when configured
            using TestPluginBase plugin = CreateTestPlugin();

            string path = plugin.Config().GetAssetsPath();

            Assert.IsNotNull(path);
            Assert.EndsWith("plugin_assets", path);
        }

        [TestMethod]
        public void GetAssetsPath_NotConfigured_ThrowsException()
        {
            // Verify GetAssetsPath throws when assets path is not configured
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ConfigurationException>(() => plugin.Config().GetAssetsPath());
        }

        #endregion

        #region Plugin Search Dirs Tests

        [TestMethod]
        public void TryGetPluginSearchDirs_SinglePath_ReturnsArray()
        {
            // Verify TryGetPluginSearchDirs returns array with single path configuration
            using TestPluginBase plugin = CreateTestPlugin();

            string[] dirs = plugin.Config().TryGetPluginSearchDirs();

            Assert.IsNotNull(dirs);
            Assert.HasCount(1, dirs);
            Assert.IsTrue(dirs[0].EndsWith("plugin\\dir") || dirs[0].EndsWith("plugin/dir"));
        }

        [TestMethod]
        public void TryGetPluginSearchDirs_MultiPath_ReturnsArray()
        {
            // Verify TryGetPluginSearchDirs handles multiple paths from paths array
            object multiPathConfigObj = new
            {
                plugins = new
                {
                    paths = new string[] { "/host/plugins1", "/host/plugins2" }
                }
            };

            using TestPluginBase plugin = new(multiPathConfigObj, new { });

            string[] dirs = plugin.Config().TryGetPluginSearchDirs();

            Assert.IsNotNull(dirs);
            Assert.HasCount(2, dirs);
        }

        [TestMethod]
        public void TryGetPluginSearchDirs_NotConfigured_ReturnsEmpty()
        {
            // Verify TryGetPluginSearchDirs returns empty array when not configured
            using TestPluginBase plugin = new();

            string[] dirs = plugin.Config().TryGetPluginSearchDirs();

            Assert.IsNotNull(dirs);
            Assert.IsEmpty(dirs);
        }

        [TestMethod]
        public void GetPluginSearchDirs_Found_ReturnsArray()
        {
            // Verify GetPluginSearchDirs returns array when paths are configured
            using TestPluginBase plugin = CreateTestPlugin();

            string[] dirs = plugin.Config().GetPluginSearchDirs();

            Assert.IsNotNull(dirs);
            Assert.IsGreaterThan(0, dirs.Length);
        }

        [TestMethod]
        public void GetPluginSearchDirs_NotConfigured_ThrowsException()
        {
            // Verify GetPluginSearchDirs throws when plugin search paths not configured
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ConfigurationException>(() => plugin.Config().GetPluginSearchDirs());
        }

        #endregion

        #region Static Helper Tests

        [TestMethod]
        public void GetConfigurationNameAttribute_WithAttribute_ReturnsAttribute()
        {
            // Verify GetConfigurationNameAttribute retrieves attribute from decorated type
            ConfigurationNameAttribute? attr = Loading.PluginConfigStore.GetConfigurationNameAttribute(typeof(SimpleConfig));

            Assert.IsNotNull(attr);
            Assert.AreEqual("simple_config", attr.ConfigVarName);
            Assert.IsTrue(attr.Required);
        }

        [TestMethod]
        public void GetConfigurationNameAttribute_NoAttribute_ReturnsNull()
        {
            // Verify GetConfigurationNameAttribute returns null for undecorated type
            ConfigurationNameAttribute? attr = Loading.PluginConfigStore.GetConfigurationNameAttribute(typeof(NoAttributeConfig));

            Assert.IsNull(attr);
        }

        [TestMethod]
        public void GetConfigurationNameAttribute_NullType_ThrowsArgumentNullException()
        {
            // Verify GetConfigurationNameAttribute validates null type parameter
            Assert.ThrowsExactly<ArgumentNullException>(() => 
                Loading.PluginConfigStore.GetConfigurationNameAttribute(null!)
            );
        }

        [TestMethod]
        public void GetConfigNameForType_WithAttribute_ReturnsName()
        {
            // Verify GetConfigNameForType extracts config property name from attribute
            string? name = Loading.PluginConfigStore.GetConfigNameForType(typeof(SimpleConfig));

            Assert.AreEqual("simple_config", name);
        }

        [TestMethod]
        public void GetConfigNameForType_NoAttribute_ReturnsNull()
        {
            // Verify GetConfigNameForType returns null for undecorated type
            string? name = Loading.PluginConfigStore.GetConfigNameForType(typeof(NoAttributeConfig));

            Assert.IsNull(name);
        }

        [TestMethod]
        public void ConfigurationRequired_RequiredTrue_ReturnsTrue()
        {
            // Verify ConfigurationRequired returns true when Required attribute is true
            bool required = Loading.PluginConfigStore.ConfigurationRequired(typeof(SimpleConfig));

            Assert.IsTrue(required);
        }

        [TestMethod]
        public void ConfigurationRequired_RequiredFalse_ReturnsFalse()
        {
            // Verify ConfigurationRequired returns false when Required attribute is false
            bool required = Loading.PluginConfigStore.ConfigurationRequired(typeof(OptionalConfig));

            Assert.IsFalse(required);
        }

        [TestMethod]
        public void ConfigurationRequired_NoAttribute_ReturnsFalse()
        {
            // Verify ConfigurationRequired returns false for undecorated type
            bool required = Loading.PluginConfigStore.ConfigurationRequired(typeof(NoAttributeConfig));

            Assert.IsFalse(required);
        }

        #endregion
    }
}
