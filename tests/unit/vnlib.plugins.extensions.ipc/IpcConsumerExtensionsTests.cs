/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc.Tests
* File: IpcConsumerExtensionsTests.cs 
*
* IpcConsumerExtensionsTests.cs is part of VNLib.Plugins.Extensions.Ipc.Tests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Ipc.Tests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Ipc.Tests is distributed in the hope that it will be useful,
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

using VNLib.Utils;
using VNLib.Utils.Async;
using VNLib.Utils.Extensions;
using VNLib.Utils.Memory;
using VNLib.Plugins.Extensions.Loading.Tests;

using static VNLib.Plugins.Extensions.Ipc.IpcObjectExporter;

namespace VNLib.Plugins.Extensions.Ipc.Tests
{
    [TestClass]
    public class IpcConsumerExtensionsTests : VnDisposeable
    {

        private readonly IMemoryHandle<byte> _sharedBuffer;
        private readonly object _sharedLock;

        public TestContext TestContext { get; set; }

        public IpcConsumerExtensionsTests()
        {
            _sharedBuffer = MemoryUtil.SafeAlloc(RequiredBufferSize);
            _sharedLock = new();
        }

        protected override void Free()
        {
            _sharedBuffer.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            MemoryUtil.InitializeBlock(
                ref _sharedBuffer.GetReference(),
                _sharedBuffer.GetIntLength()
            );
        }

        private IpcExportBridge ConsumerBridge()
        {
            return IpcExportBridge.Open(_sharedLock, () => _sharedBuffer.Span);
        }

        private IpcExportBridge ProducerBridge()
        {
            return IpcExportBridge.Create(_sharedLock, () => _sharedBuffer.Span);
        }

        [TestMethod]
        public async Task HappyPath()
        {
            /*
             * Lifecycle: In prod the lifecycle will usually look like this.
             * 
             * 1) Construction
             * 2) Load() - Bridge is not ready until the producer loads
             * 3) (Producer publish) - instance becomes available for use across the bridge
             * 4) (Producer or consumer exit) - Instance is removed
             * 5) Unload() - Dispose the bridge 
             * 
             * Simulating unload on either side by dispose the bridges on the same plugin. 
             */

            // early producer load
            using IpcExportBridge producer = ProducerBridge();

            // Publish test implementation of the auth manager for bridge usage
            producer.Publish(
                nameof(TestExportType),
                instance: new TestExportType()
            );

            {
                TestExportConsumer proxy = new();

                using (TestPluginBase testPlugin = new())
                {
                    // Bridge is usually established during load and usually disposed before the plugin is unloaded
                    using IpcExportBridge bridge = ConsumerBridge();

                    testPlugin.Ipc()
                              .Exports(bridge)
                              .Consume(nameof(TestExportType), proxy);

                    // Delay because consume will use the plugin scheduler for async tasks 50ms should be
                    // more than enough when publish is already called
                    await proxy.WaitForChangeAsync(TestContext.CancellationToken);

                    // Should be ready to use now
                    Assert.AreEqual(1, proxy.OnInstanceChangedCounter);
                    Assert.IsNotNull(proxy.Instance);

                    // The instance can be exact during this test because it's running inside the same ALC, 
                    // but in production this would fail because types are not unified
                    Assert.IsExactInstanceOfType<TestExportType>(proxy.Instance);


                } // Simulate consumer-side unload, waits for all threads to complete

                Assert.AreEqual(2, proxy.OnInstanceChangedCounter);
                Assert.IsNull(proxy.Instance);
            }

            {
                TestExportConsumer proxy = new();

                // re-establish consumer bridge
                using TestPluginBase testPlugin = new();
                using IpcExportBridge bridge = ConsumerBridge();

                // Export should be read
                testPlugin.Ipc()
                          .Exports(bridge)
                          .Consume(nameof(TestExportType), proxy);

                // Wait for consumer threads
                await proxy.WaitForChangeAsync(TestContext.CancellationToken);

                Assert.AreEqual(1, proxy.OnInstanceChangedCounter);
                Assert.IsNotNull(proxy.Instance);
                Assert.IsExactInstanceOfType<TestExportType>(proxy.Instance);

                // Simulate producer side unload
                producer.Dispose();

                // need to wait for the consumer threads to cleanup
                await proxy.WaitForChangeAsync(TestContext.CancellationToken);

                // Ensure consumer proxy unloaded the type
                Assert.AreEqual(2, proxy.OnInstanceChangedCounter);
                Assert.IsNull(proxy.Instance);
            }
        }

        #region Consumer Monitoring Loop

        /// <summary>
        /// Validates that a consumer worker, started before the producer has published
        /// its export, polls until the export becomes available and then notifies the
        /// consumer with the published instance.
        /// </summary>
        [TestMethod]
        public async Task Consume_WaitsForDelayedPublish_ThenNotifiesConsumer()
        {
            using IpcExportBridge producer = ProducerBridge();

            TestExportConsumer proxy = new();

            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridge)
                      .Consume(nameof(TestExportType), proxy);            

            producer.Publish(
                nameof(TestExportType),
                instance: new TestExportType()
            );

            await proxy.WaitForChangeAsync(TestContext.CancellationToken);

            Assert.AreEqual(1, proxy.OnInstanceChangedCounter);
            Assert.IsNotNull(proxy.Instance);
            Assert.IsExactInstanceOfType<TestExportType>(proxy.Instance);
        }

        /// <summary>
        /// Validates that a consumer worker, started before the producer bridge is
        /// available, awaits the bridge task and then proceeds to monitor exports
        /// once the bridge becomes ready.
        /// </summary>
        [TestMethod]
        public async Task Consume_WaitsForDelayedBridge_ThenNotifiesConsumer()
        {
            using IpcExportBridge producer = ProducerBridge();

            producer.Publish(
                nameof(TestExportType), 
                instance: new TestExportType()
            );

            TaskCompletionSource<IpcExportBridge> bridgeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            TestExportConsumer proxy = new();

            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridgeTcs.Task)
                      .Consume(nameof(TestExportType), proxy);

            // Worker is waiting for the bridge; no notification yet
            Assert.AreEqual(0, proxy.OnInstanceChangedCounter);

            // Resolve the bridge so the worker can proceed to poll the already-published export
            bridgeTcs.SetResult(bridge);

            await proxy.WaitForChangeAsync(TestContext.CancellationToken);

            Assert.AreEqual(1, proxy.OnInstanceChangedCounter);
            Assert.IsNotNull(proxy.Instance);
            Assert.IsExactInstanceOfType<TestExportType>(proxy.Instance);
        }

        /// <summary>
        /// Validates that after a producer unpublishes an export the consumer worker
        /// re-enters its monitoring loop, and when the producer republishes the same
        /// symbol the consumer receives the new instance via a second
        /// <see cref="IIpcExportConsumer.OnInstanceChanged"/> callback.
        /// </summary>
        [TestMethod]
        public async Task Consume_RepublishAfterExpiry_NotifiesConsumerAgain()
        {
            using IpcExportBridge producer = ProducerBridge();
            producer.Publish(nameof(TestExportType), new TestExportType());

            TestExportConsumer proxy = new();

            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridge)
                      .Consume(nameof(TestExportType), proxy);

            // Wait for the initial notification
            await proxy.WaitForChangeAsync(TestContext.CancellationToken);
            Assert.AreEqual(1, proxy.OnInstanceChangedCounter);
            Assert.IsNotNull(proxy.Instance);

            // Unpublish the export, worker should clear the instance and re-enter the loop
            producer.Unpublish(nameof(TestExportType));

            await proxy.WaitForChangeAsync(TestContext.CancellationToken);
            Assert.AreEqual(2, proxy.OnInstanceChangedCounter);
            Assert.IsNull(proxy.Instance);

            // Republish the same symbol, worker should pick it up again
            producer.Publish(nameof(TestExportType), new TestExportType());

            await proxy.WaitForChangeAsync(TestContext.CancellationToken);
            Assert.AreEqual(3, proxy.OnInstanceChangedCounter);
            Assert.IsNotNull(proxy.Instance);
            Assert.IsExactInstanceOfType<TestExportType>(proxy.Instance);
        }

        /// <summary>
        /// Validates that when the consumer plugin is unloaded while the worker is
        /// still waiting for an export that has not yet been published, the worker
        /// exits its monitoring loop cleanly without hanging or throwing.
        /// </summary>
        [TestMethod]
        public async Task Consume_PluginUnloadCancelsWaitingConsumer()
        {
            using IpcExportBridge producer = ProducerBridge();

            TestExportConsumer proxy = new();

            TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridge)
                      .Consume(nameof(TestExportType), proxy);

            // Allow the worker to enter its polling loop before unloading
            await Task.Delay(50, TestContext.CancellationToken);

            // Dispose blocks until all scheduled tasks (including the worker) complete.
            // If the worker does not honor the exit token, this call hangs and the test times out.
            testPlugin.Dispose();

            // Worker should have exited without ever notifying the consumer
            Assert.AreEqual(0, proxy.OnInstanceChangedCounter);
            Assert.IsNull(proxy.Instance);
        }

        /// <summary>
        /// Validates that two consumers registered for the same export symbol on the
        /// same bridge each receive independent
        /// <see cref="IIpcExportConsumer.OnInstanceChanged"/> notifications when the
        /// export becomes available.
        /// </summary>
        [TestMethod]
        public async Task Consume_MultipleConsumersSameExport_BothNotified()
        {
            using IpcExportBridge producer = ProducerBridge();

            producer.Publish(
                nameof(TestExportType),
                instance: new TestExportType()
            );

            TestExportConsumer proxyA = new();
            TestExportConsumer proxyB = new();

            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridge)
                      .Consume(nameof(TestExportType), proxyA)
                      .Consume(nameof(TestExportType), proxyB);

            await Task.WhenAll(
               proxyA.WaitForChangeAsync(TestContext.CancellationToken),
               proxyB.WaitForChangeAsync(TestContext.CancellationToken)
            );

            Assert.AreEqual(1, proxyA.OnInstanceChangedCounter);
            Assert.IsNotNull(proxyA.Instance);
            Assert.IsExactInstanceOfType<TestExportType>(proxyA.Instance);

            Assert.AreEqual(1, proxyB.OnInstanceChangedCounter);
            Assert.IsNotNull(proxyB.Instance);
            Assert.IsExactInstanceOfType<TestExportType>(proxyB.Instance);
        }

        /// <summary>
        /// Validates that two consumers registered for different export symbols on the
        /// same bridge each receive the correct instance for their respective symbol.
        /// </summary>
        [TestMethod]
        public async Task Consume_MultipleConsumersDistinctExports_EachGetsOwnInstance()
        {
            using IpcExportBridge producer = ProducerBridge();

            TestExportType exportA = new();
            TestExportType exportB = new();

            producer.Publish("ExportA", instance: exportA);
            producer.Publish("ExportB", instance: exportB);

            TestExportConsumer proxyA = new();
            TestExportConsumer proxyB = new();

            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridge)
                      .Consume("ExportA", proxyA)
                      .Consume("ExportB", proxyB);

            await Task.WhenAll(
                proxyA.WaitForChangeAsync(TestContext.CancellationToken),
                proxyB.WaitForChangeAsync(TestContext.CancellationToken)
            );

            Assert.AreEqual(1, proxyA.OnInstanceChangedCounter);
            Assert.AreSame(exportA, proxyA.Instance);

            Assert.AreEqual(1, proxyB.OnInstanceChangedCounter);
            Assert.AreSame(exportB, proxyB.Instance);
        }

        /// <summary>
        /// Validates that the <see cref="PluginConsumerExtensions.PluginIpcManager.Exports(IAsyncLazy{IpcExportBridge})"/>
        /// overload correctly awaits the lazy bridge and notifies the consumer once the
        /// bridge resolves and the export becomes available.
        /// </summary>
        [TestMethod]
        public async Task Exports_LazyBridge_ResolvesAndNotifiesConsumer()
        {
            using IpcExportBridge producer = ProducerBridge();

            producer.Publish(
                nameof(TestExportType),
                instance: new TestExportType()
            );

            TaskCompletionSource<IpcExportBridge> bridgeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            TestExportConsumer proxy = new();

            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            testPlugin.Ipc()
                      .Exports(bridgeTcs.Task.AsLazy())
                      .Consume(nameof(TestExportType), proxy);

            // Worker is waiting for the lazy bridge; no notification yet
            Assert.AreEqual(0, proxy.OnInstanceChangedCounter);

            // Resolve the lazy bridge so the worker can proceed
            bridgeTcs.SetResult(bridge);

            await proxy.WaitForChangeAsync(TestContext.CancellationToken);

            Assert.AreEqual(1, proxy.OnInstanceChangedCounter);
            Assert.IsNotNull(proxy.Instance);
            Assert.IsExactInstanceOfType<TestExportType>(proxy.Instance);
        }

        #endregion

        #region Argument Validation

        /// <summary>
        /// Validates that <see cref="PluginConsumerExtensions.Ipc(PluginBase)"/> throws
        /// <see cref="ArgumentNullException"/> when the plugin argument is null.
        /// </summary>
        [TestMethod]
        public void Ipc_NullArguments_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => PluginConsumerExtensions.Ipc(null!));
        }

        /// <summary>
        /// Validates that all <see cref="PluginConsumerExtensions.PluginIpcManager.Exports(Task{IpcExportBridge})"/> overloads
        /// throw <see cref="ArgumentNullException"/> when their bridge arguments are null.
        /// </summary>
        [TestMethod]
        public void Exports_NullArguments_ThrowsArgumentNullException()
        {
            using TestPluginBase testPlugin = new();
            PluginConsumerExtensions.PluginIpcManager manager = testPlugin.Ipc();

            Assert.ThrowsExactly<ArgumentNullException>(() => manager.Exports((IpcExportBridge)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.Exports((Task<IpcExportBridge>)null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.Exports((IAsyncLazy<IpcExportBridge>)null!));
        }

        /// <summary>
        /// Validates that <see cref="PluginConsumerExtensions.IpcExportConsumerMonitor.Consume"/> throws
        /// <see cref="ArgumentNullException"/> when the export name or consumer argument is null.
        /// </summary>
        [TestMethod]
        public void Consume_NullArguments_ThrowsArgumentNullException()
        {
            using TestPluginBase testPlugin = new();
            using IpcExportBridge bridge = ConsumerBridge();

            PluginConsumerExtensions.IpcExportConsumerMonitor monitor = testPlugin.Ipc().Exports(bridge);

            Assert.ThrowsExactly<ArgumentNullException>(() => monitor.Consume(null!, new TestExportConsumer()));
            Assert.ThrowsExactly<ArgumentNullException>(() => monitor.Consume(nameof(TestExportType), null!));
        }

        #endregion

        #region Test Fixtures

        private sealed class TestExportType { }

        private sealed class TestExportConsumer : IIpcExportConsumer
        {
            const TaskCreationOptions Options = TaskCreationOptions.RunContinuationsAsynchronously;
            private TaskCompletionSource _tcs = new(Options);

            public object? Instance { get; private set; }

            public int OnInstanceChangedCounter { get; private set; }

            /// <inheritdoc/>
            public void OnInstanceChanged(object? instance)
            {
                TaskCompletionSource tcs = Interlocked.Exchange(ref _tcs, new(Options));               

                Instance = instance;
                OnInstanceChangedCounter++;

                tcs.TrySetResult();
            }

            public Task WaitForChangeAsync(CancellationToken cancellation) 
                => _tcs.Task.WaitAsync(cancellation);
        }

        #endregion
    }
}
