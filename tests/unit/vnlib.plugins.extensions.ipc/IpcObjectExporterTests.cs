/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Ipc.Tests
* File: IpcObjectExporterTests.cs 
*
* IpcObjectExporterTests.cs is part of VNLib.Plugins.Extensions.Ipc.Tests which is part of the larger 
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
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

using static VNLib.Plugins.Extensions.Ipc.IpcObjectExporter;

namespace VNLib.Plugins.Extensions.Ipc.Tests
{
    [TestClass]
    public class IpcObjectExporterTests : VnDisposeable
    {
        private const int MaxExports = IpcObjectExporter.MaxExports;
        private const int MaxExportNameSize = IpcObjectExporter.MaxExportNameSize;

        private readonly IMemoryHandle<byte> _sharedBuffer;
        private readonly object _sharedLock;

        public IpcObjectExporterTests()
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

        private IpcExportBridge CreateBridge()
        {
            return IpcExportBridge.Create(_sharedLock, () => _sharedBuffer.Span);
        }

        private IpcExportBridge OpenBridge()
        {
            return IpcExportBridge.Open(_sharedLock, () => _sharedBuffer.Span);
        }

        public IpcObjectExporter Exporter => new(_sharedBuffer.Span, _sharedLock);

        #region Constructor

        /// <summary>
        /// Validates that a freshly constructed exporter (without initialization)
        /// reports the table as uninitialized with null instance and exit task.
        /// </summary>
        [TestMethod]
        public void Constructor_CreatesUninitializedExporter()
        {
            (bool initialized, object? instance, Task? exitTask) = Exporter.TryGetExport("any");
            Assert.IsFalse(initialized);
            Assert.IsNull(instance);
            Assert.IsNull(exitTask);
        }

        /// <summary>
        /// Validates that creating an exporter with an empty buffer and attempting
        /// to publish throws because the shared memory table cannot fit its header.
        /// </summary>
        [TestMethod]
        public void Constructor_Throws_WhenBufferTooSmall()
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                new IpcObjectExporter([], _sharedLock)
                    .Publish("object", new object());
            });
        }

        #endregion

        #region Initialize

        /// <summary>
        /// Validates that initializing the exporter transitions the table to a ready state.
        /// </summary>
        [TestMethod]
        public void Initialize_SetsTableReady()
        {
            Exporter.Initialize();

            try
            {
                (bool initialized, _, _) = Exporter.TryGetExport(string.Empty);
                Assert.IsTrue(initialized);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that a second call to Initialize on an already-initialized table
        /// is rejected to prevent corrupting the shared memory header.
        /// </summary>
        [TestMethod]
        public void Initialize_Throws_OnDoubleInitialization()
        {
            Exporter.Initialize();

            try
            {
                _ = Assert.ThrowsExactly<InvalidOperationException>(() => Exporter.Initialize());
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        #endregion

        #region Destroy

        /// <summary>
        /// Validates that Destroy transitions the table back to an uninitialized state,
        /// making all previously published exports no longer resolvable.
        /// </summary>
        [TestMethod]
        public void Destroy_TransitionsTableToUninitialized()
        {
            Exporter.Initialize();

            Exporter.Publish("obj", new object());
            Exporter.Destroy();

            (bool Initialized, object? instance, _) = Exporter.TryGetExport("obj");
            Assert.IsFalse(Initialized);
            Assert.IsNull(instance);
        }

        /// <summary>
        /// Validates that calling Destroy on an uninitialized table is rejected,
        /// since there is no shared memory state to tear down.
        /// </summary>
        [TestMethod]
        public void Destroy_Throws_WhenNotInitialized()
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => Exporter.Destroy());
        }

        /// <summary>
        /// Validates that a second call to Destroy on an already-destroyed table
        /// is rejected to prevent double-freeing GCHandles and shared memory state.
        /// </summary>
        [TestMethod]
        public void Destroy_Throws_OnDoubleDestroy()
        {
            Exporter.Initialize();
            Exporter.Destroy();

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => Exporter.Destroy());
        }

        /// <summary>
        /// Validates that Destroy frees all GCHandles such that after a destroy/re-init
        /// cycle, previously published names resolve to null instances while new
        /// publishes function correctly.
        /// </summary>
        [TestMethod]
        public void Destroy_FreesAllGCHandles()
        {
            Exporter.Initialize();

            try
            {
                Exporter.Publish("Obj1", new object());
                Exporter.Publish("Obj2", new object());
                Exporter.Destroy();

                Exporter.Initialize();

                (object? old1, _) = Exporter.TryGetExport("Obj1");
                Assert.IsNull(old1);

                (object? old2, _) = Exporter.TryGetExport("Obj2");
                Assert.IsNull(old2);

                Exporter.Publish("NewObj", new object());
                (object? newResult, _) = Exporter.TryGetExport("NewObj");
                Assert.IsNotNull(newResult);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        #endregion

        #region Publish

        /// <summary>
        /// Validates that a published object remains alive and callable through the
        /// export table, confirming the GCHandle keeps the instance rooted and method
        /// dispatch through the export preserves the object's state.
        /// </summary>
        [TestMethod]
        public void Publish_ExportedObjectRemainsAliveAndCallable()
        {
            TestSharedObj producer = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish(nameof(TestSharedObj), producer);

                (object? export, _) = Exporter.TryGetExport(nameof(TestSharedObj));
                TestSharedObj consumer = (TestSharedObj)export!;

                string result = consumer.TestMethod("hello world");
                Assert.AreEqual("hello world", result);

                Assert.AreEqual(1, producer.TestMethodCalledCount);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that publishing to a zero-length buffer is rejected, since the
        /// export table header cannot fit in an empty shared memory region.
        /// </summary>
        [TestMethod]
        public void Publish_Throws_WhenBufferTooSmall()
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                new IpcObjectExporter([], _sharedLock)
                    .Publish("object", new object());
            });
        }

        /// <summary>
        /// Validates that publishing before the table is initialized is rejected,
        /// since the shared memory header must be written first.
        /// </summary>
        [TestMethod]
        public void Publish_Throws_WhenNotInitialized()
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => Exporter.Publish("object", new object())
            );
        }

        /// <summary>
        /// Validates that Publish rejects invalid arguments: names exceeding
        /// MaxExportNameSize, null instances, and empty/whitespace names.
        /// </summary>
        [TestMethod]
        public void Publish_Throws_WhenInvalidArguments()
        {
            Exporter.Initialize();

            try
            {
                string longName = new('a', MaxExportNameSize + 1);
                _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Exporter.Publish(longName, new object()));
                _ = Assert.ThrowsExactly<ArgumentNullException>(() => Exporter.Publish("test", null!));
                _ = Assert.ThrowsExactly<ArgumentException>(() => Exporter.Publish("", new object()));
                _ = Assert.ThrowsExactly<ArgumentException>(() => Exporter.Publish("   ", new object()));
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that publishing a second export under an already-registered
        /// symbol name is rejected to prevent ambiguous resolution for consumers.
        /// </summary>
        [TestMethod]
        public void Publish_Throws_WhenDuplicateName()
        {
            TestSharedObj obj = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish(nameof(TestSharedObj), obj);
                _ = Assert.ThrowsExactly<ArgumentException>(() => Exporter.Publish(nameof(TestSharedObj), new object()));
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that the exporter can hold the maximum number of concurrent
        /// exports (MaxExports) and that every published entry is individually
        /// resolvable by its symbol name.
        /// </summary>
        [TestMethod]
        public void Publish_FillsMaxExportCapacity()
        {
            Exporter.Initialize();

            try
            {
                for (int i = 0; i < MaxExports; i++)
                {
                    Exporter.Publish($"Export_{i}", new object());
                }

                for (int i = 0; i < MaxExports; i++)
                {
                    (object? result, _) = Exporter.TryGetExport($"Export_{i}");
                    Assert.IsNotNull(result);
                }
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that publishing beyond the maximum export capacity is
        /// rejected, preventing the fixed-size shared memory table from
        /// overflowing.
        /// </summary>
        [TestMethod]
        public void Publish_Throws_WhenCapacityExceeded()
        {
            Exporter.Initialize();

            try
            {
                for (int i = 0; i < MaxExports; i++)
                {
                    Exporter.Publish($"Export_{i}", new object());
                }

                _ = Assert.ThrowsExactly<InvalidOperationException>(
                    () => Exporter.Publish("Overflow", new object())
                );
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that after an export is unpublished, its slot can be reused
        /// for a new export with the same symbol name.
        /// </summary>
        [TestMethod]
        public void Publish_CanReuseSlotAfterRemove()
        {
            Exporter.Initialize();

            try
            {
                object first = new();
                Exporter.Publish("ReusedSlot", first);
                Exporter.Unpublish("ReusedSlot");

                object second = new();
                Exporter.Publish("ReusedSlot", second);

                (object? instance, _)= Exporter.TryGetExport("ReusedSlot");
                Assert.IsNotNull(instance);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that the same object instance can be published under multiple
        /// names, supporting backward/forward compatibility aliases for consumers.
        /// Both names must resolve to the identical reference.
        /// </summary>
        [TestMethod]
        public void Publish_SameInstanceUnderDifferentNames()
        {
            TestSharedObj obj = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish("MyService", obj);
                Exporter.Publish("MyService_v2", obj);

                (object? v1, _) = Exporter.TryGetExport("MyService");
                (object? v2, _) = Exporter.TryGetExport("MyService_v2");

                Assert.IsNotNull(v1);
                Assert.IsNotNull(v2);
                Assert.AreSame(obj, v1);
                Assert.AreSame(obj, v2);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        #endregion

        #region Unpublish

        /// <summary>
        /// Validates that unpublishing a symbol removes the export entry and frees
        /// the GCHandle, making the symbol no longer resolvable by consumers.
        /// </summary>
        [TestMethod]
        public void Unpublish_RemovesExportEntry()
        {
            TestSharedObj obj = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish(nameof(TestSharedObj), obj);
                bool removed = Exporter.Unpublish(nameof(TestSharedObj));
                Assert.IsTrue(removed);

                (object? result, _) = Exporter.TryGetExport(nameof(TestSharedObj));
                Assert.IsNull(result);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that unpublishing a non-existent symbol returns false without
        /// affecting other previously published exports.
        /// </summary>
        [TestMethod]
        public void Unpublish_ReturnsFalse_WhenNameNotFound()
        {
            Exporter.Initialize();

            try
            {
                Exporter.Publish(nameof(TestSharedObj), new TestSharedObj());

                bool removed = Exporter.Unpublish("NonExistent");
                Assert.IsFalse(removed);

                (object? found, _) = Exporter.TryGetExport(nameof(TestSharedObj));
                Assert.IsNotNull(found);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that unpublishing with null, empty, or whitespace-only names
        /// is rejected, since symbol names must be meaningful identifiers.
        /// </summary>
        [TestMethod]
        public void Unpublish_Throws_WhenInvalidArguments()
        {
            Exporter.Initialize();

            try
            {
                _ = Assert.ThrowsExactly<ArgumentException>(() => Exporter.Unpublish(null!));
                _ = Assert.ThrowsExactly<ArgumentException>(() => Exporter.Unpublish(""));
                _ = Assert.ThrowsExactly<ArgumentException>(() => Exporter.Unpublish("   "));
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that unpublishing before the table is initialized is rejected,
        /// since no exports can exist in an uninitialized table.
        /// </summary>
        [TestMethod]
        public void Unpublish_Throws_WhenNotInitialized()
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => Exporter.Unpublish("name"));
        }

        /// <summary>
        /// Validates that removing one export does not affect the accessibility of
        /// other exports, confirming that the table correctly manages independent
        /// GCHandles per entry.
        /// </summary>
        [TestMethod]
        public void Unpublish_OtherExportsRemainAccessible()
        {
            TestSharedObj obj1 = new();
            TestSharedObj obj2 = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish("Object1", obj1);
                Exporter.Publish("Object2", obj2);
                Exporter.Unpublish("Object1");

                (object? removed, _)= Exporter.TryGetExport("Object1");
                Assert.IsNull(removed);

                (object? remaining, _) = Exporter.TryGetExport("Object2");
                Assert.IsNotNull(remaining);
                Assert.AreSame(obj2, remaining);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        #endregion

        #region TryGetExport

        /// <summary>
        /// Validates that TryGetExport returns the same object instance that was
        /// published, with Initialized true and a valid producer exit task.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ReturnsExportedObject()
        {
            TestSharedObj obj = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish(nameof(TestSharedObj), obj);

                (bool initialized, object? instance, Task? exitTask) = Exporter.TryGetExport(nameof(TestSharedObj));
               
                Assert.IsTrue(initialized);
                Assert.IsNotNull(instance);
                Assert.AreSame(obj, instance);
                Assert.IsNotNull(exitTask);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that TryGetExport returns an uninitialized result when the
        /// table has not been initialized, with null instance and null exit task.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ReturnsNotInitialized_WhenTableNotReady()
        {
            (bool initialized, object? instance, Task? exitTask) = Exporter.TryGetExport("object");
            Assert.IsFalse(initialized);
            Assert.IsNull(instance);
            Assert.IsNull(exitTask);
        }

        /// <summary>
        /// Validates that TryGetExport on an initialized table with no matching
        /// symbol returns Initialized true with a null instance but a valid exit task.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ReturnsNotFound_WhenSymbolMissing()
        {
            Exporter.Initialize();

            try
            {
                (bool initialized, object? instance, Task? exitTask) = Exporter.TryGetExport("nonexistent");
                Assert.IsTrue(initialized);
                Assert.IsNull(instance);
                Assert.IsNull(exitTask);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that after the table is destroyed, TryGetExport reports the
        /// table as uninitialized with a null instance.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ReturnsNotInitialized_AfterDestroy()
        {
            Exporter.Initialize();
            Exporter.Publish("obj", new object());
            Exporter.Destroy();

            (bool initialized, _, _)= Exporter.TryGetExport("obj");
            Assert.IsFalse(initialized);
        }

        /// <summary>
        /// Validates that an initialized but empty table returns null for any
        /// lookup, confirming the table does not produce spurious results when
        /// no exports have been published.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ReturnsNullOnEmptyTable()
        {
            Exporter.Initialize();

            try
            {
                (bool initialized, object? instance, _) = Exporter.TryGetExport("anything");
                Assert.IsTrue(initialized);
                Assert.IsNull(instance);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that export name lookups are case-insensitive, allowing
        /// consumers to resolve a published symbol regardless of casing.
        /// </summary>
        [TestMethod]
        public void TryGetExport_IsCaseInsensitive()
        {
            TestSharedObj obj = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish("MyService", obj);

                (object? lower, _) = Exporter.TryGetExport("myservice");
                (object? upper, _) = Exporter.TryGetExport("MYSERVICE");
                (object? mixed, _) = Exporter.TryGetExport("mYsErViCe");

                Assert.IsNotNull(lower);
                Assert.IsNotNull(upper);
                Assert.IsNotNull(mixed);

                Assert.AreSame(obj, lower);
                Assert.AreSame(obj, upper);
                Assert.AreSame(obj, mixed);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that a single-character export name is correctly stored
        /// and resolved in the shared memory table.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ResolvesSingleCharName()
        {
            Exporter.Initialize();

            try
            {
                Exporter.Publish("A", new object());

                (object? instance, _) = Exporter.TryGetExport("A");
                Assert.IsNotNull(instance);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that export names containing special characters such as
        /// dots and underscores are correctly stored and resolved, as these
        /// characters are common in interface-style identifiers.
        /// </summary>
        [TestMethod]
        public void TryGetExport_ResolvesNameWithSpecialCharacters()
        {
            const string specialName = "IAccount.Security_Provider";

            Exporter.Initialize();

            try
            {
                Exporter.Publish(specialName, new object());

                (object? instance, _) = Exporter.TryGetExport(specialName);
                Assert.IsNotNull(instance);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates the invariant that a published instance always has a
        /// valid non-null exit task tied to its lifetime.
        /// </summary>
        [TestMethod]
        public void TryGetExport_OnExitTaskNonNullWhenInstanceExists()
        {
            Exporter.Initialize();

            try
            {
                Exporter.Publish("export", new TestSharedObj());
                (bool initialized, object? instance, Task? exitTask) = Exporter.TryGetExport("export");
                Assert.IsTrue(initialized);
                Assert.IsNotNull(instance);
                Assert.IsNotNull(exitTask);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates the invariant that a non-existent symbol in an initialized
        /// table has no exit task, because the task is tied to the instance
        /// lifetime, not the table lifetime.
        /// </summary>
        [TestMethod]
        public void TryGetExport_OnExitTaskNullWhenNoInstance()
        {
            Exporter.Initialize();

            try
            {
                Exporter.Publish("export", new TestSharedObj());
                (bool initialized, object? instance, Task? exitTask) = Exporter.TryGetExport("missing");
                Assert.IsTrue(initialized);
                Assert.IsNull(instance);
                Assert.IsNull(exitTask);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates the invariant that when Initialized is false, no
        /// OnExitTask has been allocated.
        /// </summary>
        [TestMethod]
        public void TryGetExport_OnExitTaskNullWhenNotInit()
        {
            (bool initialized, _, Task? exitTask) = Exporter.TryGetExport("any");
            Assert.IsFalse(initialized);
            Assert.IsNull(exitTask);
        }

        #endregion

        #region OnExitTask

        /// <summary>
        /// Validates that an instance's exit task completes when the
        /// producer is disposed (which destroys the table), allowing
        /// consumers to detect instance termination.
        /// </summary>
        [TestMethod]
        public async Task OnExitTask_CompletesWhenPluginUnloads()
        {
            Task? exitTask;

            using (IpcExportBridge producer = CreateBridge())
            {
                producer.Publish("export", new TestSharedObj());
                (_, exitTask) = producer.TryGetExport("export");

                Assert.IsNotNull(exitTask);
                Assert.IsFalse(exitTask.IsCompleted);
            }

            // Dispose calls destroy which fires all instance exit tasks

            await exitTask!.WaitAsync(TestContext.CancellationToken);
        }

        /// <summary>
        /// Validates that unpublishing an instance completes its exit task,
        /// because the exit task is tied to the instance lifetime.
        /// </summary>
        [TestMethod]
        public async Task Unpublish_CompletesInstanceExitTask()
        {
            Exporter.Initialize();

            try
            {
                Exporter.Publish("export", new TestSharedObj());
                (_, Task? exitTask) = Exporter.TryGetExport("export");

                Assert.IsNotNull(exitTask);
                Assert.IsFalse(exitTask.IsCompleted);

                Exporter.Unpublish("export");

                await exitTask.WaitAsync(TestContext.CancellationToken);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        #endregion

        #region CornerCases

        /// <summary>
        /// Validates that after a destroy/re-init cycle, the table returns to a
        /// clean state: Initialized is true, previous exports are gone, and new
        /// exports function normally. Simulates a producer plugin unloading and
        /// a new producer reinitializing the same shared memory.
        /// </summary>
        [TestMethod]
        public void Initialize_SucceedsAfterDestroy()
        {
            Exporter.Initialize();

            Exporter.Publish("First", new object());
            Exporter.Destroy();

            Exporter.Initialize();
            try
            {
                (bool initialized, object? previous, _) = Exporter.TryGetExport("First");
                Assert.IsTrue(initialized);
                Assert.IsNull(previous);

                Exporter.Publish("Second", new object());
                (object? newExport, _) = Exporter.TryGetExport("Second");
                Assert.IsNotNull(newExport);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that an export name at the maximum allowed length
        /// (MaxExportNameSize - 1) is accepted, since the validation rejects
        /// lengths greater than or equal to MaxExportNameSize.
        /// </summary>
        [TestMethod]
        public void Publish_AcceptsMaxValidNameLength()
        {
            Exporter.Initialize();

            try
            {
                const int MaxValidNameLength = MaxExportNameSize - 1;
                string maxName = new('x', MaxValidNameLength);

                Exporter.Publish(maxName, new object());

                (object? instance, _) = Exporter.TryGetExport(maxName);
                Assert.IsNotNull(instance);
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that a name exactly equal to MaxExportNameSize in length
        /// is rejected, confirming the upper bound of the name length constraint.
        /// </summary>
        [TestMethod]
        public void Publish_RejectsNameAtArrayLength()
        {
            Exporter.Initialize();

            try
            {
                string arrayLenName = new('x', MaxExportNameSize);
                _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                    () => Exporter.Publish(arrayLenName, new object())
                );
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        /// <summary>
        /// Validates that duplicate name detection is also case-insensitive,
        /// preventing two exports from claiming the same logical symbol name
        /// with different casings.
        /// </summary>
        [TestMethod]
        public void Publish_DuplicateNameIsCaseInsensitive()
        {
            TestSharedObj obj = new();

            Exporter.Initialize();

            try
            {
                Exporter.Publish("MyService", obj);

                _ = Assert.ThrowsExactly<ArgumentException>(
                    () => Exporter.Publish("MYSERVICE", new object())
                );
            }
            finally
            {
                Exporter.Destroy();
            }
        }

        #endregion

        internal sealed class TestSharedObj
        {
            public int TestMethodCalledCount { get; private set; }

            public string TestMethod(string value)
            {
                TestMethodCalledCount++;
                return value;
            }
        }

        public TestContext TestContext { get; set; }
    }
}
