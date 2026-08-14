/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Tests
* File: PluginTaskExtensionsTests.cs
*
* PluginTaskExtensionsTests.cs is part of VNLib.Plugins.Extensions.Loading.Tests which is part of the larger
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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Tests
{
    using static PluginTaskExtensions;

    /// <summary>
    /// Unit tests for <see cref="PluginTaskExtensions"/> and the
    /// <see cref="PluginTaskObserver"/> ref struct it produces.
    /// </summary>
    [TestClass]
    public sealed class PluginTaskExtensionsTests
    {
        #region Tasks

        /// <summary>
        /// Verifies that <see cref="PluginTaskExtensions.Tasks(PluginBase)"/> throws
        /// <see cref="ArgumentNullException"/> when the plugin instance is null.
        /// </summary>
        [TestMethod]
        public void Tasks_NullPlugin_ThrowsArgumentNullException()
        {
            PluginBase plugin = null!;

            Assert.ThrowsExactly<ArgumentNullException>(() => _ = plugin.Tasks());
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskExtensions.Tasks(PluginBase)"/> returns a
        /// <see cref="PluginTaskObserver"/> for a valid plugin instance without throwing.
        /// </summary>
        [TestMethod]
        public void Tasks_ValidPlugin_ReturnsObserver()
        {
            using TestPluginBase plugin = new();

            _ = plugin.Tasks();
        }

        #endregion

        #region ObserveWork

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ObserveWork(Func{Task}, int)"/> throws
        /// <see cref="ObjectDisposedException"/> when called on an unloaded plugin.
        /// </summary>
        [TestMethod]
        public async Task ObserveWork_UnloadedPlugin_ThrowsObjectDisposedException()
        {
            using TestPluginBase plugin = new();
            plugin.Dispose();

            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => plugin.Tasks().ObserveWork(() => Task.CompletedTask)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ObserveWork(Func{Task}, int)"/> completes
        /// the async work successfully and observes the result.
        /// </summary>
        [TestMethod]
        public async Task ObserveWork_AsyncWork_CompletesSuccessfully()
        {
            using TestPluginBase plugin = new();
            int expected = 42;
            int actual = 0;

            Task task = plugin.Tasks().ObserveWork(() =>
            {
                actual = expected;
                return Task.CompletedTask;
            });

            await task.ConfigureAwait(false);
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ObserveWork(Func{Task}, int)"/> swallows
        /// <see cref="TaskCanceledException"/> when the plugin is unloaded during work.
        /// </summary>
        [TestMethod]
        public async Task ObserveWork_PluginUnloadedDuringDelay_SwallowsCancelException()
        {
            using TestPluginBase plugin = new();

            // If the task runs it should raise the exception when awaited. If cancel is swallowed
            // it should silently complete
            Task task = plugin.Tasks()
                              .ObserveWork(() => Task.FromException(new Exception("ShouldNotThrow")), delayMs: 5000);

            // Unload the plugin while the delay is pending
            plugin.Dispose();

            // Task should complete without throwing because cancellation is caught internally
            await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ObserveWork(IAsyncBackgroundWork, int)"/>
        /// delegates to the work's <see cref="IAsyncBackgroundWork.DoWorkAsync"/> method.
        /// </summary>
        [TestMethod]
        public async Task ObserveWork_BackgroundWork_CallsDoWorkAsync()
        {
            using TestPluginBase plugin = new();
            MockBackgroundWork work = new();

            await plugin.Tasks()
                        .ObserveWork(work, delayMs: 0)
                        .ConfigureAwait(false);

            Assert.IsTrue(work.DoWorkAsyncCalled);
        }

        #endregion

        #region RegisterForUnload

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.RegisterForUnload(Action)"/> throws
        /// <see cref="ArgumentNullException"/> when the callback is null.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_NullCallback_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Tasks().RegisterForUnload((Action)null!)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.RegisterForUnload(Action)"/> invokes the
        /// registered callback when the plugin is unloaded.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_ActionCallback_InvokedOnUnload()
        {
            using TestPluginBase plugin = new();
            bool wasCalled = false;

            plugin.Tasks().RegisterForUnload(() => wasCalled = true);
            plugin.Dispose();

            Assert.IsTrue(wasCalled);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.RegisterForUnload(Action)"/> returns the
        /// observer for fluent chaining.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_ActionCallback_ReturnsObserver()
        {
            using TestPluginBase plugin = new();
            PluginTaskObserver observer = plugin.Tasks();

            _ = observer.RegisterForUnload(() => { });
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.RegisterForUnload(IDisposable)"/> throws
        /// <see cref="ArgumentNullException"/> when the disposable is null.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_NullDisposable_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            Assert.ThrowsExactly<ArgumentNullException>(
                () => plugin.Tasks().RegisterForUnload((IDisposable)null!)
            );
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.RegisterForUnload(IDisposable)"/> disposes
        /// the registered instance when the plugin is unloaded.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_Disposable_DisposedOnUnload()
        {
            using TestPluginBase plugin = new();
            MockDisposable disposable = new();

            plugin.Tasks().RegisterForUnload(disposable);
            plugin.Dispose();

            Assert.IsTrue(disposable.IsDisposed);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.RegisterForUnload(IDisposable)"/> returns the
        /// observer for fluent chaining.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_Disposable_ReturnsObserver()
        {
            using TestPluginBase plugin = new();
            PluginTaskObserver observer = plugin.Tasks();

            _ = observer
                .RegisterForUnload(new MockDisposable())
                .RegisterForUnload(new MockDisposable());

            _ = observer
                .RegisterForUnload(() => { })
                .RegisterForUnload(() => { });
        }

        /// <summary>
        /// Verifies that multiple callbacks registered via
        /// <see cref="PluginTaskObserver.RegisterForUnload(Action)"/> are all invoked on unload.
        /// </summary>
        [TestMethod]
        public void RegisterForUnload_MultipleCallbacks_AllInvokedOnUnload()
        {
            using TestPluginBase plugin = new();
            int callCount = 0;

            plugin.Tasks().RegisterForUnload(() => callCount++);
            plugin.Tasks().RegisterForUnload(() => callCount++);
            plugin.Tasks().RegisterForUnload(() => callCount++);
            plugin.Dispose();

            Assert.AreEqual(3, callCount);
        }

        #endregion

        #region ConfigureServiceAsync

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ConfigureServiceAsync{T}(T, int)"/> throws
        /// <see cref="ArgumentNullException"/> when the service is null.
        /// </summary>
        [TestMethod]
        public async Task ConfigureServiceAsync_NullService_ThrowsArgumentNullException()
        {
            using TestPluginBase plugin = new();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => plugin.Tasks().ConfigureServiceAsync((IAsyncConfigurable)null!)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ConfigureServiceAsync{T}(T, int)"/> calls
        /// <see cref="IAsyncConfigurable.ConfigureServiceAsync(PluginBase)"/> on the service.
        /// </summary>
        [TestMethod]
        public async Task ConfigureServiceAsync_ValidService_CallsConfigure()
        {
            using TestPluginBase plugin = new();
            MockConfigurableService service = new();

            await plugin.Tasks()
                        .ConfigureServiceAsync(service, delayMs: 0)
                        .ConfigureAwait(false);

            Assert.IsTrue(service.ConfigureCalled);
        }

        /// <summary>
        /// Verifies that <see cref="PluginTaskObserver.ConfigureServiceAsync{T}(T, int)"/> throws
        /// <see cref="ObjectDisposedException"/> when called on an unloaded plugin.
        /// </summary>
        [TestMethod]
        public async Task ConfigureServiceAsync_UnloadedPlugin_ThrowsObjectDisposedException()
        {
            using TestPluginBase plugin = new();
            plugin.Dispose(); 

            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => plugin.Tasks().ConfigureServiceAsync(new MockConfigurableService())
            ).ConfigureAwait(false);
        }

        #endregion

        #region Helper Types

        private sealed class MockBackgroundWork : IAsyncBackgroundWork
        {
            public bool DoWorkAsyncCalled { get; private set; }

            public Task DoWorkAsync(ILogProvider log, CancellationToken exitToken)
            {
                DoWorkAsyncCalled = true;
                return Task.CompletedTask;
            }
        }

        private sealed class MockDisposable : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class MockConfigurableService : IAsyncConfigurable
        {
            public bool ConfigureCalled { get; private set; }

            public Task ConfigureServiceAsync(PluginBase plugin)
            {
                ConfigureCalled = true;
                return Task.CompletedTask;
            }
        }

        #endregion
    }
}
