/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: PluginSecretStoreTests.cs 
*
* PluginSecretStoreTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger 
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
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

namespace VNLib.Plugins.Extensions.Loading.Tests.Secrets
{
    using Loading.Secrets;

    [TestClass]
    public class PluginSecretStoreTests
    {
        // Reusable empty config objects for tests that only need one side populated
        private static readonly object EmptyHostConfig   = new { secrets = new { } };
        private static readonly object EmptyPluginConfig = new { secrets = new { } };

        #region IsSet — presence detection

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.IsSet"/> returns false
        /// when the secret key is absent from both host and plugin configurations.
        /// </summary>
        [TestMethod]
        public void IsSet_ReturnsFalse_WhenKeyAbsentFromBothConfigs()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            Assert.IsFalse(plugin.Secrets().IsSet("nonexistent"));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.IsSet"/> returns true
        /// when the secret key is present only in the host config.
        /// </summary>
        [TestMethod]
        public void IsSet_ReturnsTrue_WhenKeyInHostConfig()
        {
            var hostConfig = new { secrets = new { mySecret = "value" } };

            using TestPluginBase plugin = new(EmptyPluginConfig, hostConfig);

            Assert.IsTrue(plugin.Secrets().IsSet("mySecret"));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.IsSet"/> returns true
        /// when the secret key is present only in the plugin config.
        /// </summary>
        [TestMethod]
        public void IsSet_ReturnsTrue_WhenKeyInPluginConfig()
        {
            var pluginConfig = new { secrets = new { mySecret = "value" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.IsTrue(plugin.Secrets().IsSet("mySecret"));
        }

        /// <summary>
        /// Verifies that secret key lookup via <see cref="PluginSecretStore.IsSet"/> is
        /// case-insensitive, consistent with the ordinal-ignore-case comparison applied
        /// during the host/plugin config merge in <see cref="PluginSecretStore.TryGet"/>.
        /// </summary>
        [TestMethod]
        public void IsSet_IsCaseInsensitive()
        {
            var pluginConfig = new { secrets = new { MySecret = "value" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.IsTrue(plugin.Secrets().IsSet("mysecret"));
            Assert.IsTrue(plugin.Secrets().IsSet("MYSECRET"));
            Assert.IsTrue(plugin.Secrets().IsSet("MySecret"));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.IsSet"/> returns false when neither
        /// config contains a "secrets" key at all — as distinct from an empty secrets object.
        /// </summary>
        [TestMethod]
        public void IsSet_ReturnsFalse_WhenSecretsKeyAbsentFromBothConfigs()
        {
            // These configs have no "secrets" property at all, unlike the static EmptyHostConfig/EmptyPluginConfig
            using TestPluginBase plugin = new(new { }, new { });

            Assert.IsFalse(plugin.Secrets().IsSet("anyKey"));
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.IsSet"/> throws
        /// when the secret name argument is null, empty, or whitespace — 
        /// a null/blank key is always a programming error, not a not-found condition.
        /// </summary>
        [TestMethod]
        public void IsSet_ThrowsArgumentException_WhenNameIsNullOrWhitespace()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Secrets().IsSet(null!)
            );

            Assert.ThrowsExactly<ArgumentException>(
                () => plugin.Secrets().IsSet(string.Empty)
            );

            Assert.ThrowsExactly<ArgumentException>(
                () => plugin.Secrets().IsSet("   ")
            );
        }

        #endregion

        #region TryGet — inline values (no URI scheme)

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGet"/> returns null
        /// when the requested key is absent from both configs.
        /// </summary>
        [TestMethod]
        public void TryGet_ReturnsNull_WhenKeyNotDefined()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            using ISecretResult? result = plugin.Secrets().TryGet("nonexistent");

            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies that an inline secret value stored in the host config
        /// is correctly fetched via <see cref="PluginSecretStore.TryGet"/>.
        /// </summary>
        [TestMethod]
        public void TryGet_ReturnsValue_FromHostConfig()
        {
            var hostConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(EmptyPluginConfig, hostConfig);

            using ISecretResult? result = plugin.Secrets().TryGet("foo");

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that an inline secret value stored in the plugin config
        /// is correctly fetched via <see cref="PluginSecretStore.TryGet"/>.
        /// </summary>
        [TestMethod]
        public void TryGet_ReturnsValue_FromPluginConfig()
        {
            var pluginConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            using ISecretResult? result = plugin.Secrets().TryGet("foo");

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGet"/> key lookup is case-insensitive,
        /// consistent with the ordinal-ignore-case merge applied during config lookup.
        /// </summary>
        [TestMethod]
        public void TryGet_IsCaseInsensitive()
        {
            var pluginConfig = new { secrets = new { MySecret = "value" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            using ISecretResult? lower = plugin.Secrets().TryGet("mysecret");
            using ISecretResult? upper = plugin.Secrets().TryGet("MYSECRET");

            Assert.IsNotNull(lower);
            Assert.IsNotNull(upper);
            Assert.AreEqual("value", lower.Result.ToString());
            Assert.AreEqual("value", upper.Result.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGet"/> throws
        /// when the secret name argument is null, empty, or whitespace.
        /// </summary>
        [TestMethod]
        public void TryGet_ThrowsArgumentException_WhenNameIsNullOrWhitespace()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Secrets().TryGet(null!)
            );

            Assert.ThrowsExactly<ArgumentException>(
                () => plugin.Secrets().TryGet(string.Empty)
            );

            Assert.ThrowsExactly<ArgumentException>(
                () => plugin.Secrets().TryGet("   ")
            );
        }

        #endregion

        #region Config merge behaviour

        /// <summary>
        /// Verifies that secrets from both host and plugin configs are merged, 
        /// making distinct keys from each source independently accessible.
        /// </summary>
        [TestMethod]
        public void SecretMerge_HostAndPluginKeysAreBothAvailable()
        {
            var hostConfig   = new { secrets = new { fromHost   = "hostValue"   } };
            var pluginConfig = new { secrets = new { fromPlugin = "pluginValue" } };

            using TestPluginBase plugin = new(pluginConfig, hostConfig);

            Assert.IsTrue(plugin.Secrets().IsSet("fromHost"));
            Assert.IsTrue(plugin.Secrets().IsSet("fromPlugin"));

            using ISecretResult? hostResult   = plugin.Secrets().TryGet("fromHost");
            using ISecretResult? pluginResult = plugin.Secrets().TryGet("fromPlugin");

            Assert.IsNotNull(hostResult);
            Assert.IsNotNull(pluginResult);
            Assert.AreEqual("hostValue",   hostResult.Result.ToString());
            Assert.AreEqual("pluginValue", pluginResult.Result.ToString());
        }

        /// <summary>
        /// Verifies that when the same key exists in both configs, the plugin config 
        /// value shadows (takes precedence over) the host config value.
        /// </summary>
        [TestMethod]
        public void SecretMerge_PluginValueShadowsHostValue()
        {
            var hostConfig   = new { secrets = new { foo = "hostValue"   } };
            var pluginConfig = new { secrets = new { foo = "pluginValue" } };

            using TestPluginBase plugin = new(pluginConfig, hostConfig);

            using ISecretResult? result = plugin.Secrets().TryGet("foo");

            Assert.IsNotNull(result);
            Assert.AreEqual("pluginValue", result.Result.ToString());
            Assert.AreNotEqual("hostValue", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that when the same logical key appears in both configs but with different
        /// casing (e.g., host has "mykey", plugin has "MyKey"), the plugin value still wins.
        /// The merged dictionary uses ordinal-ignore-case comparison, so both map to the same slot.
        /// </summary>
        [TestMethod]
        public void SecretMerge_CaseInsensitiveKeyCollision_PluginValueWins()
        {
            var hostConfig   = new { secrets = new { mykey = "hostValue"   } };
            var pluginConfig = new { secrets = new { MyKey = "pluginValue" } };

            using TestPluginBase plugin = new(pluginConfig, hostConfig);

            // The lookup should resolve to the plugin value regardless of casing used here
            using ISecretResult? result = plugin.Secrets().TryGet("mykey");

            Assert.IsNotNull(result);
            Assert.AreEqual("pluginValue", result.Result.ToString());
        }

        #endregion

        #region TryGetAsync — inline values

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGetAsync"/> returns null
        /// when the requested key is absent from both configs.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_ReturnsNull_WhenKeyNotDefined()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            using ISecretResult? result = await plugin.Secrets().TryGetAsync("nonexistent");

            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies that an inline secret value is returned correctly via the async path.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_ReturnsValue_InlineSecret()
        {
            var pluginConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            using ISecretResult? result = await plugin.Secrets().TryGetAsync("foo");

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGetAsync"/> throws
        /// when the secret name argument is null, empty, or whitespace.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_ThrowsArgumentException_WhenNameIsNullOrWhitespace()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);


            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => plugin.Secrets().TryGetAsync(null!)
            );

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => plugin.Secrets().TryGetAsync(string.Empty)
            );

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => plugin.Secrets().TryGetAsync("   ")
            );
        }

        #endregion

        #region GetAsync — required secrets

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.GetAsync"/> throws
        /// <see cref="KeyNotFoundException"/> when the requested key is not defined
        /// in either config — enforcing that required secrets must be present.
        /// </summary>
        [TestMethod]
        public async Task GetAsync_ThrowsKeyNotFoundException_WhenKeyNotDefined()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            await Assert.ThrowsExactlyAsync<KeyNotFoundException>(
                () => plugin.Secrets().GetAsync("missingSecret")
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.GetAsync"/> returns the secret value
        /// without throwing when the key exists.
        /// </summary>
        [TestMethod]
        public async Task GetAsync_ReturnsValue_WhenSecretExists()
        {
            var pluginConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            using ISecretResult result = await plugin.Secrets().GetAsync("foo");

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.GetAsync"/> throws
        /// when the secret name argument is null, empty, or whitespace.
        /// </summary>
        [TestMethod]
        public async Task GetAsync_ThrowsArgumentException_WhenNameIsNullOrWhitespace()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => plugin.Secrets().GetAsync(null!)
            );

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => plugin.Secrets().GetAsync(string.Empty)
            );

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => plugin.Secrets().GetAsync("   ")
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.GetAsync"/> correctly accepts and
        /// threads a <see cref="CancellationToken"/> through to the underlying reader.
        /// </summary>
        [TestMethod]
        public async Task GetAsync_AcceptsCancellationToken()
        {
            var pluginConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            using ISecretResult result = await plugin.Secrets().GetAsync("foo", CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        #endregion

        #region File secret reader (file:// scheme)

        /// <summary>
        /// Verifies that a secret backed by a <c>file://</c> URI reads the file contents
        /// from disk and returns them correctly via the synchronous path.
        /// </summary>
        [TestMethod]
        public void TryGet_FromFile_ReturnsFileContents()
        {
            const string SecretValue = "secretFromFile";
            string tempFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(tempFile, SecretValue);

                var pluginConfig = new { secrets = new { foo = $"file://{tempFile}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                Assert.IsTrue(plugin.Secrets().IsSet("foo"));

                using ISecretResult? result = plugin.Secrets().TryGet("foo");

                Assert.IsNotNull(result);
                Assert.AreEqual(SecretValue, result.Result.ToString());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Verifies that a secret backed by a <c>file://</c> URI reads the file contents
        /// from disk and returns them correctly via the asynchronous path.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_FromFile_ReturnsFileContents()
        {
            const string SecretValue = "secretFromFile";
            string tempFile = Path.GetTempFileName();

            try
            {
                await File.WriteAllTextAsync(tempFile, SecretValue);

                var pluginConfig = new { secrets = new { foo = $"file://{tempFile}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                using ISecretResult? result = await plugin.Secrets().TryGetAsync("foo");

                Assert.IsNotNull(result);
                Assert.AreEqual(SecretValue, result.Result.ToString());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Verifies that the <c>file://</c> reader surfaces a <see cref="FileNotFoundException"/>
        /// when the referenced path does not exist, rather than returning null or wrapping
        /// the error in a different exception type.
        /// </summary>
        [TestMethod]
        public void TryGet_FromFile_ThrowsFileNotFoundException_WhenFileNotFound()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"vnlib_missing_{Guid.NewGuid()}.secret");

            var pluginConfig = new { secrets = new { foo = $"file://{missingPath}" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<FileNotFoundException>(
                () => plugin.Secrets().TryGet("foo")
            );
        }

        /// <summary>
        /// Verifies that the async <c>file://</c> reader also surfaces a <see cref="FileNotFoundException"/>
        /// when the referenced path does not exist.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_FromFile_ThrowsFileNotFoundException_WhenFileNotFound()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), $"vnlib_missing_{Guid.NewGuid()}.secret");

            var pluginConfig = new { secrets = new { foo = $"file://{missingPath}" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            await Assert.ThrowsExactlyAsync<FileNotFoundException>(
                () => plugin.Secrets().TryGetAsync("foo")
            );
        }

        #endregion

        #region Environment variable reader (env:// scheme)

        /// <summary>
        /// Verifies that a secret backed by an <c>env://</c> URI reads the referenced
        /// environment variable value correctly via the synchronous path.
        /// </summary>
        [TestMethod]
        public void TryGet_FromEnvironmentVariable_ReturnsValue()
        {
            const string EnvVarName    = "VNLIB_PSS_TEST_SYNC";
            const string ExpectedValue = "syncEnvSecret";

            Environment.SetEnvironmentVariable(EnvVarName, ExpectedValue);

            try
            {
                var pluginConfig = new { secrets = new { foo = $"env://{EnvVarName}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                using ISecretResult? result = plugin.Secrets().TryGet("foo");

                Assert.IsNotNull(result);
                Assert.AreEqual(ExpectedValue, result.Result.ToString());
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, null);
            }
        }

        /// <summary>
        /// Verifies that a secret backed by an <c>env://</c> URI reads the referenced
        /// environment variable value correctly via the asynchronous path.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_FromEnvironmentVariable_ReturnsValue()
        {
            const string EnvVarName    = "VNLIB_PSS_TEST_ASYNC";
            const string ExpectedValue = "asyncEnvSecret";

            Environment.SetEnvironmentVariable(EnvVarName, ExpectedValue);

            try
            {
                var pluginConfig = new { secrets = new { foo = $"env://{EnvVarName}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                using ISecretResult? result = await plugin.Secrets().TryGetAsync("foo");

                Assert.IsNotNull(result);
                Assert.AreEqual(ExpectedValue, result.Result.ToString());
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, null);
            }
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGet"/> returns null when a secret
        /// uses the <c>env://</c> scheme but the referenced environment variable is not set.
        /// </summary>
        [TestMethod]
        public void TryGet_FromEnvironmentVariable_ReturnsNull_WhenVariableNotSet()
        {
            const string EnvVarName = "VNLIB_PSS_TEST_UNSET";

            // Ensure the variable is absent before the test
            Environment.SetEnvironmentVariable(EnvVarName, null);

            var pluginConfig = new { secrets = new { foo = $"env://{EnvVarName}" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            using ISecretResult? result = plugin.Secrets().TryGet("foo");

            Assert.IsNull(result);
        }

        #endregion

        #region Unknown URI scheme

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGet"/> throws
        /// <see cref="NotSupportedException"/> when the secret value references
        /// an unrecognised URI scheme — no reader is registered for it.
        /// </summary>
        [TestMethod]
        public void TryGet_ThrowsNotSupportedException_WhenSchemeIsUnknown()
        {
            var pluginConfig = new { secrets = new { foo = "unknownscheme://some-path" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<NotSupportedException>(
                () => plugin.Secrets().TryGet("foo")
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.TryGetAsync"/> returns a faulted task
        /// carrying a <see cref="NotSupportedException"/> when the scheme is unrecognised.
        /// </summary>
        [TestMethod]
        public async Task TryGetAsync_ThrowsNotSupportedException_WhenSchemeIsUnknown()
        {
            var pluginConfig = new { secrets = new { foo = "unknownscheme://some-path" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            await Assert.ThrowsExactlyAsync<NotSupportedException>(
                () => plugin.Secrets().TryGetAsync("foo")
            );
        }

        #endregion

        #region OnDemandSecret

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.GetOnDemandSecret"/> propagates
        /// the requested name onto the returned <see cref="IOnDemandSecret"/> instance.
        /// </summary>
        [TestMethod]
        public void GetOnDemandSecret_HasCorrectSecretName()
        {
            var pluginConfig = new { secrets = new { myKey = "value" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("myKey");

            Assert.AreEqual("myKey", onDemand.SecretName);
        }

        /// <summary>
        /// Verifies that <see cref="PluginSecretStore.GetOnDemandSecret"/> throws
        /// when the secret name is null, empty, or whitespace.
        /// </summary>
        [TestMethod]
        public void GetOnDemandSecret_ThrowsArgumentException_WhenNameIsNullOrWhitespace()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Secrets().GetOnDemandSecret(null!)
            );

            Assert.ThrowsExactly<ArgumentException>(
                () => plugin.Secrets().GetOnDemandSecret(string.Empty)
            );

            Assert.ThrowsExactly<ArgumentException>(
                () => plugin.Secrets().GetOnDemandSecret("   ")
            );
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecret"/> returns null when
        /// the key has no backing value in either config — the secret was never defined.
        /// </summary>
        [TestMethod]
        public void GetOnDemandSecret_FetchSecret_ReturnsNull_WhenKeyNotDefined()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("missingKey");

            using ISecretResult? result = onDemand.FetchSecret();

            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecret"/> correctly returns
        /// an inline secret value each time it is called.
        /// </summary>
        [TestMethod]
        public void GetOnDemandSecret_FetchSecret_ReturnsInlineValue()
        {
            var pluginConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("foo");

            using ISecretResult? result = onDemand.FetchSecret();

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecretAsync"/> returns null
        /// when the key has no backing value in either config.
        /// </summary>
        [TestMethod]
        public async Task GetOnDemandSecret_FetchSecretAsync_ReturnsNull_WhenKeyNotDefined()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("missingKey");

            using ISecretResult? result = await onDemand.FetchSecretAsync();

            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecretAsync"/> correctly returns
        /// an inline secret value via the async path.
        /// </summary>
        [TestMethod]
        public async Task GetOnDemandSecret_FetchSecretAsync_ReturnsInlineValue()
        {
            var pluginConfig = new { secrets = new { foo = "bar" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("foo");

            using ISecretResult? result = await onDemand.FetchSecretAsync();

            Assert.IsNotNull(result);
            Assert.AreEqual("bar", result.Result.ToString());
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecret"/> re-reads the environment
        /// variable on every call, reflecting runtime changes to the variable's value.
        /// </summary>
        [TestMethod]
        public void GetOnDemandSecret_FetchSecret_ReadsCurrentEnvVarValue()
        {
            const string EnvVarName = "VNLIB_PSS_ONDEMAND_SYNC";

            Environment.SetEnvironmentVariable(EnvVarName, "firstValue");

            try
            {
                object pluginConfig = new { secrets = new { foo = $"env://{EnvVarName}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("foo");

                using (ISecretResult? first = onDemand.FetchSecret())
                {
                    Assert.IsNotNull(first);
                    Assert.AreEqual("firstValue", first.Result.ToString());
                }

                // Mutate the variable and confirm the next fetch picks up the new value
                Environment.SetEnvironmentVariable(EnvVarName, "secondValue");

                using (ISecretResult? second = onDemand.FetchSecret())
                {
                    Assert.IsNotNull(second);
                    Assert.AreEqual("secondValue", second.Result.ToString());
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, null);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecretAsync"/> re-reads the
        /// environment variable on every call, reflecting runtime changes via the async path.
        /// </summary>
        [TestMethod]
        public async Task GetOnDemandSecret_FetchSecretAsync_ReadsCurrentEnvVarValue()
        {
            const string EnvVarName = "VNLIB_PSS_ONDEMAND_ASYNC";

            Environment.SetEnvironmentVariable(EnvVarName, "firstValue");

            try
            {
                var pluginConfig = new { secrets = new { foo = $"env://{EnvVarName}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("foo");

                using (ISecretResult? first = await onDemand.FetchSecretAsync())
                {
                    Assert.IsNotNull(first);
                    Assert.AreEqual("firstValue", first.Result.ToString());
                }

                Environment.SetEnvironmentVariable(EnvVarName, "secondValue");

                using (ISecretResult? second = await onDemand.FetchSecretAsync())
                {
                    Assert.IsNotNull(second);
                    Assert.AreEqual("secondValue", second.Result.ToString());
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVarName, null);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecret"/> correctly reads
        /// a secret from disk via the <c>file://</c> URI each time it is called.
        /// </summary>
        [TestMethod]
        public void GetOnDemandSecret_FetchSecret_FromFile()
        {
            const string SecretValue = "fileOnDemandSecret";
            string tempFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(tempFile, SecretValue);

                var pluginConfig = new { secrets = new { foo = $"file://{tempFile}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("foo");

                using ISecretResult? result = onDemand.FetchSecret();

                Assert.IsNotNull(result);
                Assert.AreEqual(SecretValue, result.Result.ToString());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Verifies that <see cref="IOnDemandSecret.FetchSecretAsync"/> correctly reads
        /// a secret from disk via the <c>file://</c> URI via the async path.
        /// </summary>
        [TestMethod]
        public async Task GetOnDemandSecret_FetchSecretAsync_FromFile()
        {
            const string SecretValue = "fileOnDemandSecret";
            string tempFile = Path.GetTempFileName();

            try
            {
                await File.WriteAllTextAsync(tempFile, SecretValue);

                var pluginConfig = new { secrets = new { foo = $"file://{tempFile}" } };

                using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

                IOnDemandSecret onDemand = plugin.Secrets().GetOnDemandSecret("foo");

                using ISecretResult? result = await onDemand.FetchSecretAsync();

                Assert.IsNotNull(result);
                Assert.AreEqual(SecretValue, result.Result.ToString());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        #endregion

        #region Equality

        /// <summary>
        /// Verifies that two <see cref="PluginSecretStore"/> instances wrapping the same
        /// plugin instance compare as equal using both <c>==</c> and <see cref="object.Equals(object?)"/>.
        /// </summary>
        [TestMethod]
        public void PluginSecretStore_Equals_SamePluginInstance()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            PluginSecretStore store1 = plugin.Secrets();
            PluginSecretStore store2 = plugin.Secrets();

            Assert.AreEqual(store1, store2);
            Assert.IsTrue(store1 == store2);
            Assert.IsFalse(store1 != store2);
        }

        /// <summary>
        /// Verifies that two <see cref="PluginSecretStore"/> instances wrapping different
        /// plugin instances compare as not equal.
        /// </summary>
        [TestMethod]
        public void PluginSecretStore_NotEquals_DifferentPluginInstances()
        {
            using TestPluginBase plugin1 = new(EmptyPluginConfig, EmptyHostConfig);
            using TestPluginBase plugin2 = new(EmptyPluginConfig, EmptyHostConfig);

            PluginSecretStore store1 = plugin1.Secrets();
            PluginSecretStore store2 = plugin2.Secrets();

            Assert.AreNotEqual(store1, store2);
            Assert.IsFalse(store1 == store2);
            Assert.IsTrue(store1 != store2);
        }

        /// <summary>
        /// Verifies that the hash code for a <see cref="PluginSecretStore"/> remains 
        /// stable across repeated calls for the same underlying plugin instance.
        /// </summary>
        [TestMethod]
        public void PluginSecretStore_GetHashCode_IsConsistentAcrossCalls()
        {
            using TestPluginBase plugin = new(EmptyPluginConfig, EmptyHostConfig);

            PluginSecretStore store = plugin.Secrets();

            Assert.AreEqual(store.GetHashCode(), store.GetHashCode());
        }

        #endregion

        #region SecretResult memory safety

        /// <summary>
        /// Verifies that <see cref="SecretResult"/> zeroes its backing char array when disposed,
        /// ensuring secret material is not left in memory after the handle is released.
        /// </summary>
        [TestMethod]
        public void SecretResult_ZeroesBackingMemory_OnDispose()
        {
            var pluginConfig = new { secrets = new { foo = "sensitiveValue" } };

            using TestPluginBase plugin = new(pluginConfig, EmptyHostConfig);

            // Acquire without a using block so we can inspect after manual disposal
            ISecretResult result = plugin.Secrets().TryGet("foo")!;

            Assert.IsNotNull(result);
            Assert.AreEqual("sensitiveValue", result.Result.ToString());

            result.Dispose();

            // After disposal the backing array must be completely zeroed — no secret
            // material should remain readable in memory.
            foreach (char c in result.Result)
            {
                Assert.AreEqual('\0', c, "Expected all bytes to be zeroed after disposal");
            }
        }

        #endregion
    }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
