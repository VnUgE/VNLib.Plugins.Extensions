/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: PluginDependencyExtensionsTests.cs
*
* PluginDependencyExtensionsTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger
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


namespace VNLib.Plugins.Extensions.Loading.Tests
{
    using static PluginDependencyExtensions;

    /// <summary>
    /// Unit tests for the <see cref="PluginDependencyExtensions"/> class and the
    /// <see cref="PluginDependencies"/> ref struct it produces.
    /// </summary>
    [TestClass]
    public sealed class PluginDependencyExtensionsTests
    {
        #region Deps

        /// <summary>
        /// Verifies that <see cref="PluginDependencyExtensions.Deps(PluginBase)"/> throws
        /// <see cref="ArgumentNullException"/> when the plugin instance is null.
        /// </summary>
        [TestMethod]
        public void Deps_NullPlugin_ThrowsArgumentNullException()
        {
            PluginBase plugin = null!;

            Assert.ThrowsExactly<ArgumentNullException>(() => _ = plugin.Deps());
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencyExtensions.Deps(PluginBase)"/> returns a
        /// usable <see cref="PluginDependencies"/> scope for a valid plugin instance.
        /// </summary>
        [TestMethod]
        public void Deps_ValidPlugin_ReturnsDependencies()
        {
            using TestPluginBase plugin = new();

            _ = plugin.Deps();
        }

        #endregion

        #region UseSingleton

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.UseSingleton(Type, object)"/> throws
        /// <see cref="ArgumentNullException"/> when the service type is null.
        /// </summary>
        [TestMethod]
        public void UseSingleton_NullType_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Deps().UseSingleton(null!, new())
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.UseSingleton(Type, object)"/> throws
        /// <see cref="ArgumentNullException"/> when the instance is null.
        /// </summary>
        [TestMethod]
        public void UseSingleton_NullInstance_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Deps().UseSingleton(typeof(object), null!)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.UseSingleton{T}(T)"/> stores the instance
        /// so that <see cref="PluginDependencies.TryGetSingleton{T}()"/> recovers it.
        /// </summary>
        [TestMethod]
        public void UseSingleton_FirstCall_CachesInstance()
        {
            using TestPluginBase plugin = new();
            SingletonTestService instance = new();

            plugin.Deps()
                .UseSingleton(instance);

            SingletonTestService? recovered = plugin.Deps()
                .TryGetSingleton<SingletonTestService>();

            Assert.AreSame(instance, recovered);
        }

        /// <summary>
        /// Verifies that a second call to <see cref="PluginDependencies.UseSingleton{T}(T)"/>
        /// silently ignores the new instance when one is already cached.
        /// </summary>
        [TestMethod]
        public void UseSingleton_SecondCall_IgnoresNewInstance()
        {
            using TestPluginBase plugin = new();
            SingletonTestService first = new();
            SingletonTestService second = new();

            plugin.Deps()
                .UseSingleton(first)
                .UseSingleton(second);  // publish second instance, should be ignored

            SingletonTestService? recovered = plugin.Deps()
                .TryGetSingleton<SingletonTestService>();

            Assert.AreSame(first, recovered);
        }

        /// <summary>
        /// Verifies that the generic <see cref="PluginDependencies.UseSingleton{T}(T)"/> overload
        /// caches and recovers a typed instance.
        /// </summary>
        [TestMethod]
        public void UseSingleton_GenericOverload_CachesInstance()
        {
            using TestPluginBase plugin = new();

            SingletonTestService instance = new();

            plugin.Deps()
                .UseSingleton(instance);

            SingletonTestService? recovered = plugin.Deps()
                .TryGetSingleton<SingletonTestService>();

            Assert.AreSame(instance, recovered);
        }

        #endregion

        #region TryGetSingleton

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.TryGetSingleton{T}()"/> returns null
        /// when no instance has been cached for the requested type.
        /// </summary>
        [TestMethod]
        public void TryGetSingleton_NotInCache_ReturnsNull()
        {
            using TestPluginBase plugin = new();

            SingletonTestService? result = plugin.Deps()
                .TryGetSingleton<SingletonTestService>();

            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.TryGetSingleton(Type)"/> throws
        /// <see cref="ArgumentNullException"/> when the service type is null.
        /// </summary>
        [TestMethod]
        public void TryGetSingleton_NullType_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Deps().TryGetSingleton(null!)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.TryGetSingleton{T}()"/> returns the
        /// exact instance previously published via <see cref="PluginDependencies.UseSingleton{T}(T)"/>.
        /// </summary>
        [TestMethod]
        public void TryGetSingleton_AfterUseSingleton_ReturnsSameInstance()
        {
            using TestPluginBase plugin = new();
            SingletonTestService instance = new();

            plugin.Deps()
                .UseSingleton(instance);

            SingletonTestService? recovered = plugin.Deps()
                .TryGetSingleton<SingletonTestService>();

            Assert.AreSame(instance, recovered);
        }

        #endregion

        #region GetOrCreateSingleton

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.GetOrCreateSingleton{T}(Func{PluginBase, T})"/>
        /// invokes the factory on the first call and returns the produced instance.
        /// </summary>
        [TestMethod]
        public void GetOrCreateSingleton_FirstCall_InvokesFactory()
        {
            using TestPluginBase plugin = new();
            SingletonTestService instance = new();

            SingletonTestService result = plugin.Deps()
                .GetOrCreateSingleton(_ => instance);

            Assert.AreSame(instance, result);
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.GetOrCreateSingleton{T}(Func{PluginBase, T})"/>
        /// returns the cached instance on the second call without invoking the factory again.
        /// </summary>
        [TestMethod]
        public void GetOrCreateSingleton_SecondCall_ReturnsCached()
        {
            using TestPluginBase plugin = new();
            SingletonTestService first = new();
            SingletonTestService second = new();
            bool factoryInvoked = false;

            plugin.Deps()
                .GetOrCreateSingleton(_ => first);

            SingletonTestService result = plugin.Deps()
                .GetOrCreateSingleton(_ =>
                {
                    factoryInvoked = true;
                    return second;
                });

            Assert.AreSame(first, result);
            Assert.IsFalse(factoryInvoked);
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.GetOrCreateSingleton{T}(Func{PluginBase, T})"/>
        /// suppresses the factory when an instance was already published via
        /// <see cref="PluginDependencies.UseSingleton{T}(T)"/>.
        /// </summary>
        [TestMethod]
        public void GetOrCreateSingleton_AfterUseSingleton_SuppressesFactory()
        {
            using TestPluginBase plugin = new();
            SingletonTestService useInstance = new();

            bool factoryInvoked = false;

            plugin.Deps()
                .UseSingleton(useInstance);

            SingletonTestService result = plugin.Deps()
                .GetOrCreateSingleton(_ =>
                {
                    factoryInvoked = true;
                    return new SingletonTestService();
                });

            Assert.AreSame(useInstance, result);
            Assert.IsFalse(factoryInvoked);
        }

        #endregion

        #region Create

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> throws
        /// <see cref="ArgumentNullException"/> when the service type is null.
        /// </summary>
        [TestMethod]
        public void Create_NullServiceType_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Deps().Create(null!, null)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> creates an
        /// instance using the parameterless constructor when no config is supplied.
        /// </summary>
        [TestMethod]
        public void Create_ParameterlessConstructor_ReturnsInstance()
        {
            using TestPluginBase plugin = new();

            object result = plugin.Deps().Create(typeof(ParameterlessTestService), null);

            Assert.IsInstanceOfType<ParameterlessTestService>(result);
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> creates an
        /// instance using the <see cref="PluginBase"/>-only constructor when no config is supplied.
        /// </summary>
        [TestMethod]
        public void Create_PluginConstructor_ReturnsInstance()
        {
            using TestPluginBase plugin = new();

            object result = plugin.Deps().Create(typeof(PluginCtorTestService), null);

            PluginCtorTestService typed = Assert.IsInstanceOfType<PluginCtorTestService>(result);
            Assert.IsNotNull(typed.Plugin);
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> creates an
        /// instance using the (<see cref="PluginBase"/>, <see cref="IConfigScope"/>) constructor
        /// when a config scope is supplied.
        /// </summary>
        [TestMethod]
        public void Create_PluginAndConfigConstructor_ReturnsInstance()
        {
            using TestPluginBase plugin = new(
                pluginConfig: new { simple_config = new { } },
                hostConfig: new { }
            );

            IConfigScope config = plugin.Config().Get("simple_config");

            object result = plugin.Deps()
                .Create(typeof(PluginConfigCtorTestService), config);

            PluginConfigCtorTestService typed = Assert.IsInstanceOfType<PluginConfigCtorTestService>(result);
            Assert.IsNotNull(typed.Plugin);
            Assert.IsNotNull(typed.Config);
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> throws
        /// <see cref="MissingMemberException"/> when the service type has no resolvable constructor.
        /// </summary>
        [TestMethod]
        public void Create_NoResolvableConstructor_ThrowsMissingMemberException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<MissingMemberException>(
                () => plugin.Deps().Create(typeof(NoCtorTestService), null)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> throws
        /// <see cref="ConfigurationException"/> when the type declares a required
        /// <see cref="ConfigurationNameAttribute"/> but no config scope is supplied.
        /// </summary>
        [TestMethod]
        public void Create_ConfigRequiredButNull_ThrowsConfigurationException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ConfigurationException>(
                () => plugin.Deps().Create(typeof(RequiredConfigTestService), null)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginDependencies.Create(Type, IConfigScope?)"/> throws
        /// <see cref="NotSupportedException"/> when an abstract type has no concrete
        /// implementation as we no longer support dynamic type resolution.
        /// </summary>
        [TestMethod]
        public void Create_AbstractType_ThrowsNotSupportedException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<NotSupportedException>(
                () => plugin.Deps().Create(typeof(IUnimplementedTestService), null)
            );
        }

        #endregion

        #region GetOrCreateSingleton (overloads)

        /// <summary>
        /// Verifies that the parameterless <see cref="PluginDependencies.GetOrCreateSingleton{T}()"/>
        /// overload creates and caches an instance by delegating through the full
        /// <see cref="PluginDependencies.Create{T}()"/> pipeline.
        /// </summary>
        [TestMethod]
        public void GetOrCreateSingleton_Parameterless_CreatesAndCachesInstance()
        {
            using TestPluginBase plugin = new();

            ParameterlessTestService first = plugin.Deps()
                .GetOrCreateSingleton<ParameterlessTestService>();

            ParameterlessTestService second = plugin.Deps()
                .GetOrCreateSingleton<ParameterlessTestService>();

            Assert.AreSame(first, second);
        }

        #endregion
    }

    /// <summary>
    /// A simple test service used for singleton cache verification.
    /// </summary>
    internal sealed class SingletonTestService
    { }

    /// <summary>
    /// A test service with only a parameterless constructor.
    /// </summary>
    internal sealed class ParameterlessTestService
    { }

    /// <summary>
    /// A test service with only a <see cref="PluginBase"/> constructor.
    /// </summary>
    internal sealed class PluginCtorTestService(PluginBase plugin)
    {
        public PluginBase Plugin { get; } = plugin;
    }

    /// <summary>
    /// A test service with a (<see cref="PluginBase"/>, <see cref="IConfigScope"/>) constructor.
    /// </summary>
    internal sealed class PluginConfigCtorTestService(PluginBase plugin, IConfigScope config)
    {
        public PluginBase Plugin { get; } = plugin;

        public IConfigScope Config { get; } = config;
    }

    /// <summary>
    /// A test service with no resolvable constructor (only a string parameter).
    /// </summary>
    internal sealed class NoCtorTestService(string unused)
    { }

    /// <summary>
    /// A test service that declares a required <see cref="ConfigurationNameAttribute"/>
    /// to verify config-required validation.
    /// </summary>
    [ConfigurationName("simple_config", Required = true)]
    internal sealed class RequiredConfigTestService
    { }

    /// <summary>
    /// An abstract test interface with no concrete implementation in the test assembly,
    /// used to verify <see cref="NotSupportedException"/>.
    /// </summary>
    internal interface IUnimplementedTestService
    { }
}
