/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc.Tests
* File: IpcExportBridgeTests.cs 
*
* IpcExportBridgeTests.cs is part of VNLib.Plugins.Extensions.Ipc.Tests which is part of the larger 
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
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

using static VNLib.Plugins.Extensions.Ipc.IpcObjectExporter;
using static VNLib.Plugins.Extensions.Ipc.Tests.IpcObjectExporterTests;

namespace VNLib.Plugins.Extensions.Ipc.Tests
{
     [TestClass]
    public sealed class IpcExportBridgeTests : VnDisposeable
    {
        private const int ProducerInitializationDelay = 50;

        private readonly IMemoryHandle<byte> _sharedBuffer;
        private readonly object _sharedLock;

        public TestContext TestContext { get; set; }

        public IpcExportBridgeTests()
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

        private IpcExportBridge CreateProducer()
        {
            return IpcExportBridge.Create(_sharedLock, () => _sharedBuffer.Span);
        }

        private IpcExportBridge OpenConsumer()
        {
            return IpcExportBridge.Open(_sharedLock, () => _sharedBuffer.Span);
        }

        #region Constructor

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.Create(object, IpcBufferCallback)"/> 
        /// initializes the export table.
        /// </summary>
        [TestMethod]
        public void Create_InitializesTable()
        {
            using IpcExportBridge producer = CreateProducer();

            (bool initialized, _, _) = producer.TryGetExport(string.Empty);
            Assert.IsTrue(initialized);
        }

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.Open(object, IpcBufferCallback)"/> 
        /// does not initialize the export table (consumer side).
        /// </summary>
        [TestMethod]
        public void Open_DoesNotInitializeTable()
        {
            using IpcExportBridge consumer = OpenConsumer();

            (bool initialized, _, _) = consumer.TryGetExport(string.Empty);
            Assert.IsFalse(initialized);
        }

        /// <summary>
        /// Validates that creating a second producer on the same buffer
        /// throws <see cref="InvalidOperationException"/> because the table is already initialized.
        /// </summary>
        [TestMethod]
        public void Create_ThrowsOnDoubleInitialization()
        {
            using IpcExportBridge first = CreateProducer();

            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                _ = CreateProducer();
            });
        }

        /// <summary>
        /// Validates that Create throws when the lock argument is null.
        /// </summary>
        [TestMethod]
        public void Create_ThrowsWhenLockNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = IpcExportBridge.Create(null!, () => _sharedBuffer.Span);
            });
        }

        /// <summary>
        /// Validates that Create throws when the getSpan argument is null.
        /// </summary>
        [TestMethod]
        public void Create_ThrowsWhenGetSpanNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = IpcExportBridge.Create(_sharedLock, null!);
            });
        }

        #endregion

        #region Publish

        /// <summary>
        /// Validates that publishing an object using the producer convenience method
        /// works as expected.
        /// </summary>
        [TestMethod]
        public void Publish_SucceedsAfterConstruction()
        {
            using IpcExportBridge producer = CreateProducer();

            TestSharedObj obj = new();

            producer.Publish("TestExport", obj);

            (object? Instance, _) = producer.TryGetExport("TestExport");

            Assert.IsNotNull(Instance);
            Assert.AreEqual(obj, (TestSharedObj?)Instance);
        }

        /// <summary>
        /// Validates that multiple distinct objects can be published and retrieved
        /// through the producer bridge.
        /// </summary>
        [TestMethod]
        public void Publish_MultipleObjectsAfterConstruction()
        {
            using IpcExportBridge producer = CreateProducer();

            TestSharedObj obj1 = new();
            TestSharedObj obj2 = new();

            producer.Publish("Export1", obj1);
            producer.Publish("Export2", obj2);

            (object? instance1, _) = producer.TryGetExport("Export1");
            (object? instance2, _) = producer.TryGetExport("Export2");

            TestSharedObj? result1 = (TestSharedObj?)instance1;
            TestSharedObj? result2 = (TestSharedObj?)instance2;

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(obj1, result1);
            Assert.AreEqual(obj2, result2);
        }

        #endregion

        #region Consumer Publish

        /// <summary>
        /// Validates that an export published by a consumer bridge is visible
        /// to the producer reading the same shared table.
        /// </summary>
        [TestMethod]
        public void Publish_ConsumerExportVisibleToProducer()
        {
            using IpcExportBridge producer = CreateProducer();
            using IpcExportBridge consumer = OpenConsumer();

            TestSharedObj obj = new();

            consumer.Publish("ConsumerExport", obj);

            (bool initialized, object? instance, _) = producer.TryGetExport("ConsumerExport");
            Assert.IsTrue(initialized);
            Assert.IsNotNull(instance);
            Assert.AreSame(obj, instance);
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Validates that disposing a producer bridge destroys the export table
        /// and completes all instance exit tasks, making it no longer initialized
        /// on a fresh exporter over the same buffer.
        /// </summary>
        [TestMethod]
        public async Task Dispose_ProducerDestroysTable()
        {
            IpcExportBridge producer = CreateProducer();
            producer.Publish("export", new TestSharedObj());

            (_, Task? exitTask) = producer.TryGetExport("export");
            Assert.IsNotNull(exitTask);
            Assert.IsFalse(exitTask.IsCompleted);

            producer.Dispose();

            // Instance exit task should be completed when producer destroys the table
            await exitTask.WaitAsync(TestContext.CancellationToken);

            // Fresh exporter over same buffer should see uninitialized table
            Assert.IsFalse(
                new IpcObjectExporter(_sharedBuffer.Span, _sharedLock)
                .TryGetExport(string.Empty).Initialized
            );
        }

        /// <summary>
        /// Validates that disposing a consumer bridge does not destroy the export table,
        /// because only the producer owns the table lifecycle.
        /// </summary>
        [TestMethod]
        public void Dispose_ConsumerDoesNotDestroyTable()
        {
            using IpcExportBridge producer = CreateProducer();

            IpcExportBridge consumer = OpenConsumer();

            consumer.Dispose();

            // Table should still be initialized after consumer disposal
            (bool initialized, _, _) = producer.TryGetExport(string.Empty);
            Assert.IsTrue(initialized);
        }

        /// <summary>
        /// Validates that disposing a consumer bridge unpublishes all exports
        /// that the consumer published, making them no longer visible to other
        /// bridges and completing their exit tasks.
        /// </summary>
        [TestMethod]
        public async Task Dispose_ConsumerUnpublishesOwnExports()
        {
            using IpcExportBridge producer = CreateProducer();

            IpcExportBridge consumer = OpenConsumer();
            consumer.Publish("ConsumerExport", new TestSharedObj());

            (_, Task? exitTask) = producer.TryGetExport("ConsumerExport");
            Assert.IsNotNull(exitTask);
            Assert.IsFalse(exitTask.IsCompleted);

            consumer.Dispose();

            // Exit task should be completed by the unpublish
            await exitTask.WaitAsync(TestContext.CancellationToken);

            // Export should no longer be visible to the producer
            (bool initialized, object? instance, _) = producer.TryGetExport("ConsumerExport");
            Assert.IsTrue(initialized);
            Assert.IsNull(instance);
        }

        /// <summary>
        /// Validates that explicitly unpublishing an export before disposing the
        /// consumer bridge does not throw during disposal, and that the freed slot
        /// can be reused by another consumer publishing the same export name.
        /// </summary>
        [TestMethod]
        public void Unpublish_ThenDispose_SlotReusableByAnotherConsumer()
        {
            const string SharedExportName = "sharedExport";

            using IpcExportBridge producer = CreateProducer();

            {
                TestSharedObj obj = new();

                // First consumer publishes
                IpcExportBridge consumer = OpenConsumer();
                consumer.Publish(SharedExportName, obj);

                (bool init, object? inst, _) = producer.TryGetExport(SharedExportName);
                Assert.IsTrue(init);
                Assert.AreSame(obj, inst);

                // Explicitly unpublish before disposal
                bool removed = consumer.Unpublish(SharedExportName);
                Assert.IsTrue(removed);

                // Disposal should not throw or attempt to unpublish again
                consumer.Dispose();
            }

            // Slot should be free
            (bool init2, object? inst2, _) = producer.TryGetExport(SharedExportName);
            Assert.IsTrue(init2);
            Assert.IsNull(inst2);

            {
                TestSharedObj obj = new();

                // A new consumer re-publishes the same name into the reclaimed slot
                using IpcExportBridge consumer = OpenConsumer();
                consumer.Publish(SharedExportName, obj);

                (bool init, object? inst, _) = producer.TryGetExport(SharedExportName);
                Assert.IsTrue(init);
                Assert.AreSame(obj, inst);
            }
        }

        /// <summary>
        /// Validates that disposing a consumer bridge does not throw when the
        /// producer has already destroyed the table, ensuring the
        /// <see cref="IpcExportBridge"/> cleanup path swallows
        /// <see cref="InvalidOperationException"/> from the destroyed table.
        /// </summary>
        [TestMethod]
        public void Dispose_ConsumerAfterTableDestroyed_DoesNotThrow()
        {
            IpcExportBridge producer = CreateProducer();

            IpcExportBridge consumer = OpenConsumer();
            consumer.Publish("ConsumerExport", new TestSharedObj());

            // Producer destroys the table first
            producer.Dispose();

            // Consumer disposal should not throw despite the table being gone
            consumer.Dispose();
        }

        /// <summary>
        /// Validates that disposing one consumer bridge only unpublishes the
        /// exports that consumer published, leaving other consumers' exports intact.
        /// </summary>
        [TestMethod]
        public void Dispose_OneConsumer_DoesNotRemoveOtherConsumersExports()
        {
            using IpcExportBridge producer = CreateProducer();
            using IpcExportBridge consumerB = OpenConsumer();

            IpcExportBridge consumerA = OpenConsumer();

            TestSharedObj objA = new();
            TestSharedObj objB = new();

            consumerA.Publish("ExportA", objA);
            consumerB.Publish("ExportB", objB);

            // Dispose only consumerA
            consumerA.Dispose();

            // ExportA should be gone
            (bool initA, object? instA, _) = producer.TryGetExport("ExportA");
            Assert.IsTrue(initA);
            Assert.IsNull(instA);

            // ExportB should still be visible
            (bool initB, object? instB, _) = producer.TryGetExport("ExportB");
            Assert.IsTrue(initB);
            Assert.IsNotNull(instB);
            Assert.AreSame(objB, instB);
        }

        /// <summary>
        /// Validates that disposing a producer bridge cancels pending 
        /// <see cref="IpcExportBridge.WaitForExport(string)"/> calls, causing them
        /// to throw <see cref="ObjectDisposedException"/>.
        /// </summary>
        [TestMethod]
        public async Task Dispose_ProducerCancelsPendingWaits()
        {
            IpcExportBridge producer = CreateProducer();

            _ = Task.Run(async () =>
            {
                await Task.Delay(ProducerInitializationDelay, TestContext.CancellationToken)
                    .ConfigureAwait(false);

                producer.Dispose();
            }, TestContext.CancellationToken);

            _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => producer.WaitForExport("NeverPublished")
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that disposing a consumer bridge cancels pending 
        /// <see cref="IpcExportBridge.WaitForExport(string)"/> calls on that consumer,
        /// causing them to throw <see cref="ObjectDisposedException"/>.
        /// </summary>
        [TestMethod]
        public async Task Dispose_ConsumerCancelsPendingWaits()
        {
            using IpcExportBridge producer = CreateProducer();
            IpcExportBridge consumer = OpenConsumer();

            _ = Task.Run(async () =>
            {
                await Task.Delay(ProducerInitializationDelay, TestContext.CancellationToken)
                    .ConfigureAwait(false);

                consumer.Dispose();
            }, TestContext.CancellationToken);

            _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => consumer.WaitForExport("NeverPublished")
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that disposing the consumer bridge properly guards the 
        /// public methods with disposed exceptions.
        /// </summary>
        [TestMethod]
        public async Task Dispose_Guards_AllPublicMethods()
        {
            IpcExportBridge consumer = OpenConsumer();
            consumer.Dispose();

            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => consumer.WaitForExport("disposed")
            );

            Assert.ThrowsExactly<ObjectDisposedException>(() => consumer.TryGetExport("disposed"));
            Assert.ThrowsExactly<ObjectDisposedException>(() => consumer.Publish("disposed", new()));
            Assert.ThrowsExactly<ObjectDisposedException>(() => consumer.Unpublish("disposed"));
        }

        #endregion

        #region WaitForExport

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.WaitForExport(string)"/> returns
        /// the previously published object when the export is already available
        /// at the time of the call.
        /// </summary>
        [TestMethod]
        public async Task WaitForExport_ReturnsPublishedObject_WithoutYielding()
        {
            const string symbolName = "MyExport";

            using IpcExportBridge producer = CreateProducer();

            TestSharedObj obj = new();
            producer.Publish(symbolName, obj);

            Task<IpcObjectExport> waitTask = producer.WaitForExport(symbolName);
            Assert.IsTrue(waitTask.IsCompletedSuccessfully);

            (object? result, _) = await waitTask.ConfigureAwait(false);

            Assert.IsInstanceOfType<TestSharedObj>(result);
            Assert.AreEqual(obj, result);
        }

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.WaitForExport(string)"/> returns
        /// the previously published object when the export is not yet available
        /// at the time of the call, and yields until it becomes available.
        /// </summary>
        [TestMethod]
        public async Task WaitForExport_YieldsWhenNotReady()
        {
            const string symbolName = "MyExport";

            using IpcExportBridge producer = CreateProducer();

            TestSharedObj obj = new();

            Task<IpcObjectExport> waitTask = producer.WaitForExport(symbolName);
            Assert.IsFalse(waitTask.IsCompleted);

            producer.Publish(symbolName, obj);

            (object? result, _) = await waitTask.ConfigureAwait(false);
            Assert.IsInstanceOfType<TestSharedObj>(result);
            Assert.AreEqual(obj, result);
        }

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.WaitForExport(string, CancellationToken)"/> throws
        /// <see cref="OperationCanceledException"/> when the caller's cancellation
        /// token is cancelled before the export becomes available.
        /// </summary>
        [TestMethod]
        public async Task WaitForExport_ThrowsOperationCanceled_WhenTokenCancelled()
        {
            using IpcExportBridge producer = CreateProducer();

            using CancellationTokenSource cts = new();
            cts.Cancel();

            _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => producer.WaitForExport("NeverPublished", cts.Token)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.WaitForExport(string)"/> waits
        /// for an export to become available when it is not yet published at the
        /// time of the call, and returns the object once it is published on a
        /// background thread.
        /// </summary>
        [TestMethod]
        public async Task WaitForExport_WaitsUntilExportAvailable()
        {
            const string symbolName = "DelayedExport";

            using IpcExportBridge producer = CreateProducer();

            TestSharedObj obj = new();

            _ = Task.Run(async () =>
            {
                await Task.Delay(ProducerInitializationDelay, TestContext.CancellationToken)
                    .ConfigureAwait(false);

                producer.Publish(symbolName, obj);
            }, TestContext.CancellationToken);

            (object? result, _) = await producer.WaitForExport(symbolName)
                .ConfigureAwait(false);

            Assert.IsInstanceOfType<TestSharedObj>(result);
            Assert.AreEqual(obj, result);
        }

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.WaitForExport(string, CancellationToken)"/>
        /// throws <see cref="ObjectDisposedException"/> when the bridge
        /// is disposed before the requested export is published, because the internal
        /// cancellation token propagates the dispose signal.
        /// </summary>
        [TestMethod]
        public async Task WaitForExport_ThrowsObjectDisposed_WhenBridgeDisposed()
        {
            IpcExportBridge producer = CreateProducer();

            _ = Task.Run(async () =>
            {
                await Task.Delay(ProducerInitializationDelay)
                    .ConfigureAwait(false);

                producer.Dispose();
            });

            _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => producer.WaitForExport("NeverPublished")
            ).ConfigureAwait(false);

            _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => producer.WaitForExport("NeverPublished", CancellationToken.None)
            ).ConfigureAwait(false);

            _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => producer.WaitForExport("NeverPublished", Timeout.InfiniteTimeSpan)
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.WaitForExport(string, TimeSpan)"/>
        /// throws <see cref="TimeoutException"/> when the requested export
        /// never becomes available before the timeout expires.
        /// </summary>
        [TestMethod]
        public async Task WaitForExport_ThrowsTimeout_WhenExportNotAvailable()
        {
            using IpcExportBridge producer = CreateProducer();

            _ = await Assert.ThrowsExactlyAsync<TimeoutException>(
                () => producer.WaitForExport("NeverPublished", TimeSpan.FromMilliseconds(100))
            ).ConfigureAwait(false);
        }

        #endregion

        #region TryGetExport

        /// <summary>
        /// Validates that <see cref="IpcExportBridge.TryGetExport"/> returns
        /// the published instance via the consumer bridge.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ReturnsPublishedInstance()
        {
            using IpcExportBridge producer = CreateProducer();
            using IpcExportBridge consumer = OpenConsumer();

            TestSharedObj obj = new();
            producer.Publish(nameof(TestSharedObj), obj);

            (bool initialized, object? instance, Task? exitTask) = consumer.TryGetExport(nameof(TestSharedObj));

            Assert.IsTrue(initialized);
            Assert.IsNotNull(instance);
            Assert.IsNotNull(exitTask);
            Assert.AreEqual(obj, instance);
        }

        /// <summary>
        /// Validates the invariant that a published instance always has a
        /// valid non-null exit task tied to its lifetime, observed via
        /// the consumer bridge.
        /// </summary>
        [TestMethod]
        public void TryGetExport_OnExitTaskNonNullWhenInstanceExists()
        {
            using IpcExportBridge producer = CreateProducer();
            using IpcExportBridge consumer = OpenConsumer();

            producer.Publish("export", new TestSharedObj());
            (bool initialized, object? instance, Task? exitTask) = consumer.TryGetExport("export");
            Assert.IsTrue(initialized);
            Assert.IsNotNull(instance);
            Assert.IsNotNull(exitTask);
        }

        /// <summary>
        /// Validates the invariant that a non-existent symbol in an initialized
        /// table has no exit task, because the task is tied to the instance
        /// lifetime, not the table lifetime.
        /// </summary>
        [TestMethod]
        public void TryGetExport_OnExitTaskNullWhenNoInstance()
        {
            using IpcExportBridge producer = CreateProducer();
            using IpcExportBridge consumer = OpenConsumer();

            producer.Publish("export", new TestSharedObj());
            (bool initialized, object? instance, Task? exitTask) = consumer.TryGetExport("missing");
            Assert.IsTrue(initialized);
            Assert.IsNull(instance);
            Assert.IsNull(exitTask);
        }

        /// <summary>
        /// Validates that unpublishing an instance completes its exit task,
        /// because the exit task is tied to the instance lifetime.
        /// </summary>
        [TestMethod]
        public async Task Unpublish_CompletesInstanceExitTask()
        {
            using IpcExportBridge producer = CreateProducer();

            producer.Publish("export", new TestSharedObj());
            (_, Task? exitTask) = producer.TryGetExport("export");

            Assert.IsNotNull(exitTask);
            Assert.IsFalse(exitTask.IsCompleted);

            producer.Unpublish("export");

            await exitTask.WaitAsync(TestContext.CancellationToken);
        }

        #endregion
    }
}
