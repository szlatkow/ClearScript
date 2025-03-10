// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.ClearScript.V8;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ClearScript.Test
{
    [TestClass]
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Test classes use TestCleanupAttribute for deterministic teardown.")]
    public class HostListTest : ClearScriptTest
    {
        #region setup / teardown

        private ScriptEngine engine;

        [TestInitialize]
        public void TestInitialize()
        {
            engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            engine.Dispose();
            BaseTestCleanup();
        }

        #endregion

        #region test methods

        // ReSharper disable InconsistentNaming

        [TestMethod, TestCategory("HostList")]
        public void HostList_ArrayList()
        {
            var list = new ArrayList { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual(DayOfWeek.Tuesday, engine.Evaluate("list[1]"));
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list[2]"));
            engine.Execute("list[1] = list[2]");
            Assert.AreEqual(DayOfWeek.Wednesday, engine.Evaluate("list[1]"));
        }

        [TestMethod, TestCategory("HostList")]
        public void HostList_List()
        {
            var list = new List<IConvertible> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual(DayOfWeek.Tuesday, engine.Evaluate("list[1]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            engine.Execute("list[1] = list[2]");
            Assert.AreEqual(DayOfWeek.Wednesday, engine.Evaluate("list[1]"));
        }

        [TestMethod, TestCategory("HostList")]
        public void HostList_Custom()
        {
            var list = new BogusCustomList { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual(DayOfWeek.Tuesday, engine.Evaluate("list[1]"));
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list[2]"));
            engine.Execute("list[1] = list[2]");
            Assert.AreEqual(DayOfWeek.Wednesday, engine.Evaluate("list[1]"));
        }

        [TestMethod, TestCategory("HostList")]
        public void HostList_CustomGeneric()
        {
            var list = new BogusCustomList<IConvertible> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual(DayOfWeek.Tuesday, engine.Evaluate("list[1]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            engine.Execute("list[1] = list[2]");
            Assert.AreEqual(DayOfWeek.Wednesday, engine.Evaluate("list[1]"));
        }

        [TestMethod, TestCategory("HostList")]
        public void HostList_TypeRestriction()
        {
            var list = new List<IConvertible> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list.Item(2)"));
            engine.DisableListIndexTypeRestriction = true;
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list.Item(2)"));
            engine.DisableListIndexTypeRestriction = false;
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list.Item(2)"));
            engine.DisableTypeRestriction = true;
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list.Item(2)"));
        }

        [TestMethod, TestCategory("ReadOnlyHostList")]
        public void ReadOnlyHostList_List()
        {
            IReadOnlyList<IConvertible> list = new List<IConvertible> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday };
            engine.AddRestrictedHostObject("list", list);
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual(DayOfWeek.Tuesday, engine.Evaluate("list[1]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            TestUtil.AssertException<UnauthorizedAccessException>(() => engine.Execute("list[1] = list[2]"));
        }

        [TestMethod, TestCategory("ReadOnlyHostList")]
        public void ReadOnlyHostList_Custom()
        {
            var list = new BogusReadOnlyCustomList<IConvertible>(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday);
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual(DayOfWeek.Tuesday, engine.Evaluate("list[1]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            TestUtil.AssertException<UnauthorizedAccessException>(() => engine.Execute("list[1] = list[2]"));
        }

        [TestMethod, TestCategory("ReadOnlyHostList")]
        public void ReadOnlyHostList_TypeRestriction()
        {
            var list = new BogusReadOnlyCustomList<IConvertible>(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday);
            engine.Script.list = list;
            Assert.AreEqual(3, engine.Evaluate("list.Count"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list.Item(2)"));
            engine.DisableListIndexTypeRestriction = true;
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list.Item(2)"));
            engine.DisableListIndexTypeRestriction = false;
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:IConvertible]", engine.ExecuteCommand("list.Item(2)"));
            engine.DisableTypeRestriction = true;
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list[2]"));
            Assert.AreEqual("[HostObject:DayOfWeek]", engine.ExecuteCommand("list.Item(2)"));
        }

        // ReSharper restore InconsistentNaming

        #endregion

        #region miscellaneous

        public interface IBogus
        {
        }

        public interface ICustomList : IList
        {
        }

        public interface ICustomList<T> : IList<T>
        {
        }

        public interface IReadOnlyCustomList<out T> : IReadOnlyList<T>
        {
        }

        public interface IBogusCustomList : IBogus, ICustomList
        {
        }

        public interface IBogusCustomList<T> : IBogus, ICustomList<T>
        {
        }

        public interface IBogusReadOnlyCustomList<out T> : IBogus, IReadOnlyCustomList<T>
        {
        }

        public class BogusCustomListBase : IBogusCustomList
        {
            private readonly IList list = new ArrayList();

            public IEnumerator GetEnumerator()
            {
                // ReSharper disable once NotDisposedResourceIsReturned
                return list.GetEnumerator();
            }

            public void CopyTo(Array array, int index)
            {
                list.CopyTo(array, index);
            }

            public int Count => list.Count;

            public object SyncRoot => list.SyncRoot;

            public bool IsSynchronized => list.IsSynchronized;

            public int Add(object value)
            {
                return list.Add(value);
            }

            public bool Contains(object value)
            {
                return list.Contains(value);
            }

            public void Clear()
            {
                list.Clear();
            }

            public int IndexOf(object value)
            {
                return list.IndexOf(value);
            }

            public void Insert(int index, object value)
            {
                list.Insert(index, value);
            }

            public void Remove(object value)
            {
                list.Remove(value);
            }

            public void RemoveAt(int index)
            {
                list.RemoveAt(index);
            }

            public object this[int index]
            {
                get => list[index];
                set => list[index] = value;
            }

            public bool IsReadOnly => list.IsReadOnly;

            public bool IsFixedSize => list.IsFixedSize;
        }

        public class BogusCustomListBase<T> : IBogusCustomList<T>
        {
            private readonly IList<T> list = new List<T>();

            public IEnumerator<T> GetEnumerator()
            {
                // ReSharper disable once NotDisposedResourceIsReturned
                return list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(T item)
            {
                list.Add(item);
            }

            public void Clear()
            {
                list.Clear();
            }

            public bool Contains(T item)
            {
                return list.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                list.CopyTo(array, arrayIndex);
            }

            public bool Remove(T item)
            {
                return list.Remove(item);
            }

            public int Count => list.Count;

            public bool IsReadOnly => list.IsReadOnly;

            public int IndexOf(T item)
            {
                return list.IndexOf(item);
            }

            public void Insert(int index, T item)
            {
                list.Insert(index, item);
            }

            public void RemoveAt(int index)
            {
                list.RemoveAt(index);
            }

            public T this[int index]
            {
                get => list[index];
                set => list[index] = value;
            }
        }

        public class BogusReadOnlyCustomListBase<T> : IBogusReadOnlyCustomList<T>
        {
            private readonly IReadOnlyList<T> list;

            public BogusReadOnlyCustomListBase(params T[] items)
            {
                list = new List<T>(items);
            }

            public IEnumerator<T> GetEnumerator()
            {
                // ReSharper disable once NotDisposedResourceIsReturned
                return list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int Count => list.Count;

            public T this[int index] => list[index];
        }

        public class BogusCustomList : BogusCustomListBase
        {
        }

        public class BogusCustomList<T> : BogusCustomListBase<T>
        {
        }

        public class BogusReadOnlyCustomList<T> : BogusReadOnlyCustomListBase<T>
        {
            public BogusReadOnlyCustomList(params T[] items)
                : base(items)
            {
            }
        }

        #endregion
    }
}
