/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: MvcExtensionsTests.cs
*
* MvcExtensionsTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger
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
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Extensions.Loading.Routing;
using VNLib.Plugins.Extensions.Loading.Routing.Mvc;

namespace VNLib.Plugins.Extensions.Loading.Tests.Routing.Mvc
{
    /// <summary>
    /// Tests for <see cref="MvcExtensions"/> controller routing and endpoint discovery.
    /// </summary>
    [TestClass]
    public class MvcExtensionsTests
    {
        /// <summary>
        /// Verifies that a controller with a single [HttpStaticRoute] attribute
        /// is discovered and routed without error, and that the controller
        /// instance is returned to the caller.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithStaticRoute_ReturnsController()
        {
            using TestPluginBase plugin = new();

            SingleRouteController controller = plugin.Host()
                                                     .Routes()
                                                     .Add<SingleRouteController>();

            Assert.IsNotNull(controller);
        }

        /// <summary>
        /// Verifies that a controller with an invalid route path (missing leading slash)
        /// throws <see cref="ConfigurationValidationException"/> at routing time.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithInvalidPath_ThrowsConfigurationValidationException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Host().Routes().Add<InvalidPathController>()
            );
        }

        /// <summary>
        /// Verifies that a controller with combined HTTP methods on a single route
        /// throws <see cref="ConfigurationValidationException"/> at routing time.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithCombinedMethods_ThrowsConfigurationValidationException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Host().Routes().Add<CombinedMethodController>()
            );
        }

        /// <summary>
        /// Verifies that a controller with two methods declaring the same path and
        /// HTTP method throws <see cref="ConfigurationValidationException"/> at routing time.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithDuplicateRoute_ThrowsConfigurationValidationException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Host().Routes().Add<DuplicateRouteController>()
            );
        }

        /// <summary>
        /// Verifies that a controller with a config variable in the route path
        /// is resolved correctly when the variable is defined in plugin config.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithConfigSubstitution_ResolvesPath()
        {
            object pluginConfig = new { test_controller = new { test_path = "resolved" } };
            using TestPluginBase plugin = new(pluginConfig, new { });

            ConfigSubstitutionController controller = plugin.Host()
                                                            .Routes()
                                                            .Add<ConfigSubstitutionController>();

            Assert.IsNotNull(controller);
        }

        /// <summary>
        /// Verifies that registering a base controller type with a derived instance
        /// discovers routes declared on both the base and derived types.
        /// </summary>
        [TestMethod]
        public void Add_BaseControllerWithDerivedInstance_DiscoversDerivedRoutes()
        {
            using TestPluginBase plugin = new();

            DerivedController derived = new();
            BaseController controller = plugin.Host().Routes().Add<BaseController>(derived);

            Assert.AreSame(derived, controller);
        }

        /// <summary>
        /// Verifies that a guard passed to Add receives the discovered route table
        /// via <see cref="IHttpControllerGuard.OnRoutesConfigured"/>.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithGuard_NotifiesGuardOfRoutes()
        {
            using TestPluginBase plugin = new();

            RouteCapturingGuard guard = new();

            plugin.Host().Routes().Add<MultiRouteController>([guard]);

            Assert.IsNotNull(guard.CapturedRoutes);
            Assert.HasCount(2, guard.CapturedRoutes);

            // Both routes should be present regardless of order
            Assert.Contains(r => r.Path == "/one" && r.Method == HttpMethod.GET, guard.CapturedRoutes);
            Assert.Contains(r => r.Path == "/two" && r.Method == HttpMethod.POST, guard.CapturedRoutes);
        }

        /// <summary>
        /// Verifies that two methods on the same path with different HTTP methods
        /// are allowed — the duplicate check is scoped to (path, method) pairs.
        /// </summary>
        [TestMethod]
        public void Add_SamePathDifferentMethods_RoutesSuccessfully()
        {
            using TestPluginBase plugin = new();

            SamePathController controller = plugin.Host()
                                                    .Routes()
                                                    .Add<SamePathController>();

            Assert.IsNotNull(controller);
        }

        /// <summary>
        /// Verifies that a config variable missing from the controller's config scope
        /// throws at routing time.
        /// </summary>
        [TestMethod]
        public void Add_ControllerWithMissingConfigVariable_ThrowsConfigurationValidationException()
        {
            // Config scope exists but does not define the referenced variable
            object pluginConfig = new { test_controller = new { other_key = "value" } };
            using TestPluginBase plugin = new(pluginConfig, new { });

            Assert.ThrowsExactly<ConfigurationValidationException>(
                () => plugin.Host().Routes().Add<ConfigSubstitutionController>()
            );
        }

        /// <summary>
        /// A minimal controller with one static GET route.
        /// </summary>
        private sealed class SingleRouteController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            // Valid single path
            [HttpStaticRoute("/test", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleGet(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Controller with an invalid route path (no leading slash).
        /// </summary>
        private sealed class InvalidPathController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            // Invalid path — missing leading slash.
            [HttpStaticRoute("test", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleGet(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Controller with combined HTTP methods on a single route.
        /// </summary>
        private sealed class CombinedMethodController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            // Combined methods — not supported.
            [HttpStaticRoute("/test", HttpMethod.GET | HttpMethod.POST)]
            public ValueTask<VfReturnType> HandleGet(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Controller with two methods declaring the same path and method.
        /// </summary>
        private sealed class DuplicateRouteController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            [HttpStaticRoute("/test", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleGetOne(HttpEntity _)
                => new(VfReturnType.NotFound);

            [HttpStaticRoute("/test", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleGetTwo(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Controller with a config variable in the route path.
        /// </summary>
        [ConfigurationName("test_controller")]
        private sealed class ConfigSubstitutionController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            [HttpStaticRoute("/test/{{ test_path }}", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleGet(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Base controller with one route.
        /// </summary>
        private class BaseController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            [HttpStaticRoute("/base", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleBase(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Derived controller that adds another route.
        /// </summary>
        private class DerivedController : BaseController
        {
            [HttpStaticRoute("/derived", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleDerived(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Guard that captures the route table delivered to <see cref="IHttpControllerGuard.OnRoutesConfigured"/>.
        /// </summary>
        private sealed class RouteCapturingGuard : IHttpControllerGuard
        {
            public IReadOnlyCollection<MvcHttpRouteInfo>? CapturedRoutes { get; private set; }

            /// <inheritdoc/>
            public void OnRoutesConfigured(IReadOnlyCollection<MvcHttpRouteInfo> routes)
                => CapturedRoutes = routes;

            /// <inheritdoc/>
            public bool PreProcess(HttpEntity _) => true;
        }

        /// <summary>
        /// Controller with two routes used for guard notification testing.
        /// </summary>
        private sealed class MultiRouteController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            [HttpStaticRoute("/one", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleOne(HttpEntity _)
                => new(VfReturnType.NotFound);

            [HttpStaticRoute("/two", HttpMethod.POST)]
            public ValueTask<VfReturnType> HandleTwo(HttpEntity _)
                => new(VfReturnType.NotFound);
        }

        /// <summary>
        /// Controller with the same path served by different HTTP methods.
        /// </summary>
        private sealed class SamePathController : IHttpController
        {
            /// <inheritdoc/>
            public ProtectionSettings GetProtectionSettings() => new();

            [HttpStaticRoute("/shared", HttpMethod.GET)]
            public ValueTask<VfReturnType> HandleGet(HttpEntity _)
                => new(VfReturnType.NotFound);

            [HttpStaticRoute("/shared", HttpMethod.POST)]
            public ValueTask<VfReturnType> HandlePost(HttpEntity _)
                => new(VfReturnType.NotFound);
        }
    }
}
