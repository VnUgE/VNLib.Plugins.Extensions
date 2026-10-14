/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: VaultClientConfigTests.cs
*
* VaultClientConfigTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger
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

namespace VNLib.Plugins.Extensions.Loading.Tests.Secrets
{   
    using Loading.Secrets;

    /// <summary>
    /// Tests for <c>vault_client</c> configuration validation. These tests exercise
    /// config-shape validation only and never attempt a vault connection — the client
    /// itself is lazily initialized on first <c>vault://</c> fetch.
    /// </summary>
    [TestClass]
    public class VaultClientConfigTests
    {
        private static readonly object EmptyHostConfig = new { secrets = new { } };

        /// <summary>
        /// Verifies that an unknown vault client type is rejected at load time.
        /// </summary>
        [TestMethod]
        public void VaultConfig_UnknownType_ThrowsConfigurationValidationException()
        {
            object pluginConfig = new { vault_client = new { type = "bogus" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Secrets()
            );
        }

        /// <summary>
        /// Verifies that HCP mode without an <c>hcp_vault</c> section is rejected.
        /// </summary>
        [TestMethod]
        public void VaultConfig_HcpWithoutSection_ThrowsConfigurationValidationException()
        {
            object pluginConfig = new { vault_client = new { type = "hcp" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Secrets()
            );
        }

        /// <summary>
        /// Verifies that an HCP vault url that is not an absolute http(s) url is rejected.
        /// </summary>
        [TestMethod]
        public void VaultConfig_HcpWithInvalidUrl_ThrowsConfigurationValidationException()
        {
            object pluginConfig = new
            {
                vault_client = new
                {
                    type = "hcp",
                    hcp_vault = new { url = "not a url", token = "test-token" }
                }
            };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Secrets()
            );
        }

        /// <summary>
        /// Verifies that an out-of-range KV version is rejected.
        /// </summary>
        [TestMethod]
        public void VaultConfig_HcpWithInvalidKvVersion_ThrowsConfigurationValidationException()
        {
            object pluginConfig = new
            {
                vault_client = new
                {
                    type = "hcp",
                    hcp_vault = new { url = "https://vault.example.com", token = "test-token", kv_version = 5 }
                }
            };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Secrets()
            );
        }

        /// <summary>
        /// Verifies that external mode without an <c>assembly_name</c> is rejected.
        /// </summary>
        [TestMethod]
        public void VaultConfig_ExternalWithoutAssembly_ThrowsConfigurationValidationException()
        {
            object pluginConfig = new { vault_client = new { type = "external" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Secrets()
            );
        }

        /// <summary>
        /// Verifies that an external assembly path without a .dll extension is rejected.
        /// </summary>
        [TestMethod]
        public void VaultConfig_ExternalWithNonDllPath_ThrowsConfigurationValidationException()
        {
            object pluginConfig = new
            {
                vault_client = new { type = "external", assembly_name = "myvault.exe" }
            };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Secrets()
            );
        }

        /// <summary>
        /// Verifies that a valid HCP vault configuration loads without attempting
        /// a connection — the client is lazy and no <c>vault://</c> secret is fetched here.
        /// </summary>
        [TestMethod]
        public void VaultConfig_ValidHcpConfig_LoadsWithoutConnecting()
        {
            object pluginConfig = new
            {
                vault_client = new
                {
                    type = "hcp",
                    hcp_vault = new { url = "https://localhost", token = "test-token" }
                }
            };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            // Should not throw and must not attempt a network connection
            _ = plugin.Secrets();
        }
    }
}
