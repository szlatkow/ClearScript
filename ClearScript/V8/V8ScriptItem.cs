// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.Util;
using Microsoft.ClearScript.Util.COM;

namespace Microsoft.ClearScript.V8
{
    internal abstract class V8ScriptItem : ScriptItem, IJavaScriptObject
    {
        private readonly V8ScriptEngine engine;
        private readonly IV8Object target;
        private V8ScriptItem holder;
        private readonly InterlockedOneWayFlag disposedFlag = new InterlockedOneWayFlag();

        private V8ScriptItem(V8ScriptEngine engine, IV8Object target)
        {
            this.engine = engine;
            this.target = target;
        }

        public static object Wrap(V8ScriptEngine engine, object obj)
        {
            Debug.Assert(!(obj is IScriptMarshalWrapper));

            if (obj == null)
            {
                return null;
            }

            if (obj is IV8Object target)
            {
                if (target.IsArray)
                {
                    return new V8Array(engine, target);
                }

                if (!target.IsArrayBufferOrView)
                {
                    return new V8ScriptObject(engine, target);
                }

                switch (target.ArrayBufferOrViewKind)
                {
                    case V8ArrayBufferOrViewKind.ArrayBuffer:
                        return new V8ArrayBuffer(engine, target);

                    case V8ArrayBufferOrViewKind.DataView:
                        return new V8DataView(engine, target);

                    case V8ArrayBufferOrViewKind.Uint8Array:
                    case V8ArrayBufferOrViewKind.Uint8ClampedArray:
                        return new V8TypedArray<byte>(engine, target);

                    case V8ArrayBufferOrViewKind.Int8Array:
                        return new V8TypedArray<sbyte>(engine, target);

                    case V8ArrayBufferOrViewKind.Uint16Array:
                        return new V8UInt16Array(engine, target);

                    case V8ArrayBufferOrViewKind.Int16Array:
                        return new V8TypedArray<short>(engine, target);

                    case V8ArrayBufferOrViewKind.Uint32Array:
                        return new V8TypedArray<uint>(engine, target);

                    case V8ArrayBufferOrViewKind.Int32Array:
                        return new V8TypedArray<int>(engine, target);

                    case V8ArrayBufferOrViewKind.BigUint64Array:
                        return new V8TypedArray<ulong>(engine, target);

                    case V8ArrayBufferOrViewKind.BigInt64Array:
                        return new V8TypedArray<long>(engine, target);

                    case V8ArrayBufferOrViewKind.Float32Array:
                        return new V8TypedArray<float>(engine, target);

                    case V8ArrayBufferOrViewKind.Float64Array:
                        return new V8TypedArray<double>(engine, target);

                    default:
                        return new V8ScriptObject(engine, target);
                }
            }

            return obj;
        }

        public bool IsPromise => target.IsPromise;

        public bool IsShared => target.IsShared;

        public object InvokeMethod(bool marshalResult, string name, params object[] args)
        {
            VerifyNotDisposed();

            var result = engine.ScriptInvoke(() => target.InvokeMethod(name, engine.MarshalToScript(args)));
            if (marshalResult)
            {
                return engine.MarshalToHost(result, false);
            }

            return result;
        }

        private void VerifyNotDisposed()
        {
            if (disposedFlag.IsSet)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        #region ScriptItem overrides

        protected override bool TryBindAndInvoke(DynamicMetaObjectBinder binder, object[] args, out object result)
        {
            VerifyNotDisposed();

            try
            {
                if (binder is GetMemberBinder getMemberBinder)
                {
                    result = target.GetProperty(getMemberBinder.Name);
                    return true;
                }

                if ((binder is SetMemberBinder setMemberBinder) && (args != null) && (args.Length > 0))
                {
                    target.SetProperty(setMemberBinder.Name, args[0]);
                    result = args[0];
                    return true;
                }

                if (binder is GetIndexBinder)
                {
                    if ((args != null) && (args.Length == 1))
                    {
                        result = MiscHelpers.TryGetNumericIndex(args[0], out int index) ? target.GetProperty(index) : target.GetProperty(args[0].ToString());
                        return true;
                    }

                    throw new InvalidOperationException("Invalid argument or index count");
                }

                if (binder is SetIndexBinder)
                {
                    if ((args != null) && (args.Length == 2))
                    {
                        if (MiscHelpers.TryGetNumericIndex(args[0], out int index))
                        {
                            target.SetProperty(index, args[1]);
                        }
                        else
                        {
                            target.SetProperty(args[0].ToString(), args[1]);
                        }

                        result = args[1];
                        return true;
                    }

                    throw new InvalidOperationException("Invalid argument or index count");
                }

                if (binder is InvokeBinder)
                {
                    result = target.Invoke(false, args);
                    return true;
                }

                if (binder is InvokeMemberBinder invokeMemberBinder)
                {
                    result = target.InvokeMethod(invokeMemberBinder.Name, args);
                    return true;
                }
            }
            catch (Exception exception)
            {
                if (engine.CurrentScriptFrame != null)
                {
                    if (exception is IScriptEngineException scriptError)
                    {
                        if (scriptError.ExecutionStarted)
                        {
                            throw;
                        }

                        engine.CurrentScriptFrame.ScriptError = scriptError;
                    }
                    else
                    {
                        engine.CurrentScriptFrame.ScriptError = new ScriptEngineException(engine.Name, exception.Message, null, HResult.CLEARSCRIPT_E_SCRIPTITEMEXCEPTION, false, false, null, exception);
                    }
                }
            }

            result = null;
            return false;
        }

        public override string[] GetPropertyNames()
        {
            VerifyNotDisposed();
            return engine.ScriptInvoke(() => target.GetPropertyNames(false /*includeIndices*/));
        }

        public override int[] GetPropertyIndices()
        {
            VerifyNotDisposed();
            return engine.ScriptInvoke(() => target.GetPropertyIndices());
        }

        #endregion

        #region ScriptObject overrides

        public override object GetProperty(string name, params object[] args)
        {
            VerifyNotDisposed();
            if ((args != null) && (args.Length != 0))
            {
                throw new InvalidOperationException("Invalid argument or index count");
            }

            var result = engine.MarshalToHost(engine.ScriptInvoke(() => target.GetProperty(name)), false);

            if ((result is V8ScriptItem resultScriptItem) && (resultScriptItem.engine == engine))
            {
                resultScriptItem.holder = this;
            }

            return result;
        }

        public override void SetProperty(string name, params object[] args)
        {
            VerifyNotDisposed();
            if ((args == null) || (args.Length != 1))
            {
                throw new InvalidOperationException("Invalid argument or index count");
            }

            engine.ScriptInvoke(() => target.SetProperty(name, engine.MarshalToScript(args[0])));
        }

        public override bool DeleteProperty(string name)
        {
            VerifyNotDisposed();
            return engine.ScriptInvoke(() => target.DeleteProperty(name));
        }

        public override object GetProperty(int index)
        {
            VerifyNotDisposed();
            return engine.MarshalToHost(engine.ScriptInvoke(() => target.GetProperty(index)), false);
        }

        public override void SetProperty(int index, object value)
        {
            VerifyNotDisposed();
            engine.ScriptInvoke(() => target.SetProperty(index, engine.MarshalToScript(value)));
        }

        public override bool DeleteProperty(int index)
        {
            VerifyNotDisposed();
            return engine.ScriptInvoke(() => target.DeleteProperty(index));
        }

        public override object Invoke(bool asConstructor, params object[] args)
        {
            VerifyNotDisposed();

            if (asConstructor || (holder == null))
            {
                return engine.MarshalToHost(engine.ScriptInvoke(() => target.Invoke(asConstructor, engine.MarshalToScript(args))), false);
            }

            var engineInternal = (ScriptObject)engine.Global.GetProperty("EngineInternal");
            return engineInternal.InvokeMethod("invokeMethod", holder, this, args);
        }

        public override object InvokeMethod(string name, params object[] args)
        {
            return InvokeMethod(true, name, args);
        }

        #endregion

        #region IScriptMarshalWrapper implementation

        public override ScriptEngine Engine => engine;

        public override object Unwrap()
        {
            return target;
        }

        #endregion

        #region Object overrides

        public override bool Equals(object obj) => (obj is V8ScriptItem that) && engine.Equals(this, that);

        public override int GetHashCode() => target.IdentityHash;

        #endregion

        #region IJavaScriptObject implementation

        public JavaScriptObjectKind Kind => target.ObjectKind;

        public JavaScriptObjectFlags Flags => target.ObjectFlags;

        #endregion

        #region IDisposable implementation

        public override void Dispose()
        {
            if (disposedFlag.Set())
            {
                target.Dispose();
            }
        }

        #endregion

        #region Nested type: V8ScriptObject

        private sealed class V8ScriptObject : V8ScriptItem, IDictionary<string, object>
        {
            public V8ScriptObject(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            private bool TryGetProperty(string name, out object value)
            {
                VerifyNotDisposed();

                object tempResult = null;
                if (engine.ScriptInvoke(() => target.TryGetProperty(name, out tempResult)))
                {
                    var result = engine.MarshalToHost(tempResult, false);
                    if ((result is V8ScriptItem resultScriptItem) && (resultScriptItem.engine == engine))
                    {
                        resultScriptItem.holder = this;
                    }

                    value = result;
                    return true;
                }

                value = null;
                return false;
            }

            #region IDictionary<string, object> implementation

            private IDictionary<string, object> ThisDictionary => this;

            private IEnumerable<string> PropertyKeys => GetPropertyKeys();

            private IEnumerable<KeyValuePair<string, object>> KeyValuePairs => PropertyKeys.Select(name => new KeyValuePair<string, object>(name, GetProperty(name)));

            private string[] GetPropertyKeys()
            {
                VerifyNotDisposed();
                return engine.ScriptInvoke(() => target.GetPropertyNames(true /*includeIndices*/));
            }

            IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            {
                return KeyValuePairs.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ThisDictionary.GetEnumerator();
            }

            void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
            {
                SetProperty(item.Key, item.Value);
            }

            void ICollection<KeyValuePair<string, object>>.Clear()
            {
                PropertyKeys.ForEach(name => DeleteProperty(name));
            }

            bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
            {
                return TryGetProperty(item.Key, out var value) && Equals(value, item.Value);
            }

            void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
            {
                var source = KeyValuePairs.ToArray();
                Array.Copy(source, 0, array, arrayIndex, source.Length);
            }

            bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
            {
                return ThisDictionary.Contains(item) && DeleteProperty(item.Key);
            }

            int ICollection<KeyValuePair<string, object>>.Count => PropertyKeys.Count();

            bool ICollection<KeyValuePair<string, object>>.IsReadOnly => false;

            void IDictionary<string, object>.Add(string key, object value)
            {
                SetProperty(key, value);
            }

            bool IDictionary<string, object>.ContainsKey(string key)
            {
                return PropertyKeys.Contains(key);
            }

            bool IDictionary<string, object>.Remove(string key)
            {
                return DeleteProperty(key);
            }

            bool IDictionary<string, object>.TryGetValue(string key, out object value)
            {
                return TryGetProperty(key, out value);
            }

            object IDictionary<string, object>.this[string key]
            {
                get => TryGetProperty(key, out var value) ? value : throw new KeyNotFoundException();
                set => SetProperty(key, value);
            }

            ICollection<string> IDictionary<string, object>.Keys => PropertyKeys.ToList();

            ICollection<object> IDictionary<string, object>.Values => PropertyKeys.Select(name => GetProperty(name)).ToList();

            #endregion
        }

        #endregion

        #region Nested type: V8Array

        private sealed class V8Array : V8ScriptItem, IList<object>, IList
        {
            public V8Array(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            #region IList<T> implementation

            private IList<object> ThisGenericList => this;

            IEnumerator<object> IEnumerable<object>.GetEnumerator()
            {
                return new Enumerator(this);
            }

            bool ICollection<object>.Remove(object item)
            {
                var index = ThisList.IndexOf(item);
                if (index >= 0)
                {
                    ThisList.RemoveAt(index);
                    return true;
                }

                return false;
            }

            int ICollection<object>.Count => ThisList.Count;

            bool ICollection<object>.IsReadOnly => ThisList.IsReadOnly;

            void ICollection<object>.Clear()
            {
                ThisList.Clear();
            }

            bool ICollection<object>.Contains(object item)
            {
                return ThisList.Contains(item);
            }

            void ICollection<object>.CopyTo(object[] array, int arrayIndex)
            {
                ThisList.CopyTo(array, arrayIndex);
            }

            void ICollection<object>.Add(object item)
            {
                ThisList.Add(item);
            }

            void IList<object>.Insert(int index, object item)
            {
                ThisList.Insert(index, item);
            }

            void IList<object>.RemoveAt(int index)
            {
                ThisList.RemoveAt(index);
            }

            int IList<object>.IndexOf(object item)
            {
                return ThisList.IndexOf(item);
            }

            #endregion

            #region IList implementation

            private IList ThisList => this;

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ThisGenericList.GetEnumerator();
            }

            void ICollection.CopyTo(Array array, int index)
            {
                MiscHelpers.VerifyNonNullArgument(array, nameof(array));

                if (array.Rank > 1)
                {
                    throw new ArgumentException("Invalid target array", nameof(array));
                }

                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var length = ThisList.Count;
                if ((index + length) > array.Length)
                {
                    throw new ArgumentException("Insufficient space in target array", nameof(array));
                }

                for (var sourceIndex = 0; sourceIndex < length; sourceIndex++)
                {
                    array.SetValue(this[sourceIndex], index + sourceIndex);
                }
            }

            int ICollection.Count => Convert.ToInt32(GetProperty("length"));

            object ICollection.SyncRoot => this;

            bool ICollection.IsSynchronized => false;

            int IList.Add(object value)
            {
                return Convert.ToInt32(InvokeMethod("push", value)) - 1;
            }

            bool IList.Contains(object value)
            {
                return ThisList.IndexOf(value) >= 0;
            }

            void IList.Clear()
            {
                InvokeMethod("splice", 0, ThisList.Count);
            }

            int IList.IndexOf(object value)
            {
                return Convert.ToInt32(InvokeMethod("indexOf", value));
            }

            void IList.Insert(int index, object value)
            {
                InvokeMethod("splice", index, 0, value);
            }

            void IList.Remove(object value)
            {
                ThisGenericList.Remove(value);
            }

            void IList.RemoveAt(int index)
            {
                InvokeMethod("splice", index, 1);
            }

            bool IList.IsReadOnly => false;

            bool IList.IsFixedSize => false;

            #region Nested type: Enumerator

            private class Enumerator : IEnumerator<object>
            {
                private readonly V8Array array;
                private readonly int count;
                private int index = -1;

                public Enumerator(V8Array array)
                {
                    this.array = array;
                    count = array.ThisList.Count;
                }

                public bool MoveNext()
                {
                    if (index >= (count - 1))
                    {
                        return false;
                    }

                    ++index;
                    return true;
                }

                public void Reset()
                {
                    index = -1;
                }

                public object Current => array[index];

                public void Dispose()
                {
                }
            }

            #endregion

            #endregion
        }

        #endregion

        #region Nested type: V8ArrayBufferOrView

        private abstract class V8ArrayBufferOrView : V8ScriptItem
        {
            private V8ArrayBufferOrViewInfo info;
            private IArrayBuffer arrayBuffer;

            protected V8ArrayBufferOrView(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            protected IArrayBuffer ArrayBuffer => GetArrayBuffer();

            protected ulong Offset => GetInfo().Offset;

            protected ulong Size => GetInfo().Size;

            protected ulong Length => GetInfo().Length;

            protected byte[] GetBytes()
            {
                var size = Size;
                var result = new byte[size];
                InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(pData, size, result, 0));
                return result;
            }

            protected ulong ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
            {
                var size = Size;
                if (offset >= size)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                return InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(GetPtrWithOffset(pData, offset), Math.Min(count, size - offset), destination, destinationIndex));
            }

            protected ulong WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
            {
                var size = Size;
                if (offset >= size)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                return InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(source, sourceIndex, Math.Min(count, size - offset), GetPtrWithOffset(pData, offset)));
            }

            protected void InvokeWithDirectAccess(Action<IntPtr> action)
            {
                engine.ScriptInvoke(() => target.InvokeWithArrayBufferOrViewData(action));
            }

            protected T InvokeWithDirectAccess<T>(Func<IntPtr, T> func)
            {
                return engine.ScriptInvoke(() =>
                {
                    var result = default(T);
                    target.InvokeWithArrayBufferOrViewData(pData => result = func(pData));
                    return result;
                });
            }

            private V8ArrayBufferOrViewInfo GetInfo()
            {
                VerifyNotDisposed();

                if (info == null)
                {
                    engine.ScriptInvoke(() =>
                    {
                        if (info == null)
                        {
                            info = target.GetArrayBufferOrViewInfo();
                        }
                    });
                }

                return info;
            }

            private IArrayBuffer GetArrayBuffer()
            {
                return arrayBuffer ?? (arrayBuffer = (IArrayBuffer)engine.MarshalToHost(GetInfo().ArrayBuffer, false));
            }

            private static IntPtr GetPtrWithOffset(IntPtr pData, ulong offset)
            {
                var baseAddr = unchecked((ulong)pData.ToInt64());
                return new IntPtr(unchecked((long)checked(baseAddr + offset)));
            }
        }

        #endregion

        #region Nested type: V8ArrayBuffer

        private sealed class V8ArrayBuffer : V8ArrayBufferOrView, IArrayBuffer
        {
            public V8ArrayBuffer(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            #region IArrayBuffer implementation

            ulong IArrayBuffer.Size => Size;

            byte[] IArrayBuffer.GetBytes()
            {
                return GetBytes();
            }

            ulong IArrayBuffer.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
            {
                return ReadBytes(offset, count, destination, destinationIndex);
            }

            ulong IArrayBuffer.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
            {
                return WriteBytes(source, sourceIndex, count, offset);
            }

            void IArrayBuffer.InvokeWithDirectAccess(Action<IntPtr> action)
            {
                MiscHelpers.VerifyNonNullArgument(action, nameof(action));
                InvokeWithDirectAccess(action);
            }

            T IArrayBuffer.InvokeWithDirectAccess<T>(Func<IntPtr, T> func)
            {
                MiscHelpers.VerifyNonNullArgument(func, nameof(func));
                return InvokeWithDirectAccess(func);
            }

            #endregion
        }

        #endregion

        #region Nested type: V8ArrayBufferView

        private abstract class V8ArrayBufferView : V8ArrayBufferOrView, IArrayBufferView
        {
            protected V8ArrayBufferView(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            #region IArrayBufferView implementation

            IArrayBuffer IArrayBufferView.ArrayBuffer => ArrayBuffer;

            ulong IArrayBufferView.Offset => Offset;

            ulong IArrayBufferView.Size => Size;

            byte[] IArrayBufferView.GetBytes()
            {
                return GetBytes();
            }

            ulong IArrayBufferView.ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
            {
                return ReadBytes(offset, count, destination, destinationIndex);
            }

            ulong IArrayBufferView.WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
            {
                return WriteBytes(source, sourceIndex, count, offset);
            }

            void IArrayBufferView.InvokeWithDirectAccess(Action<IntPtr> action)
            {
                MiscHelpers.VerifyNonNullArgument(action, nameof(action));
                InvokeWithDirectAccess(action);
            }

            T IArrayBufferView.InvokeWithDirectAccess<T>(Func<IntPtr, T> func)
            {
                MiscHelpers.VerifyNonNullArgument(func, nameof(func));
                return InvokeWithDirectAccess(func);
            }

            #endregion
        }

        #endregion

        #region Nested type: V8DataView

        private sealed class V8DataView : V8ArrayBufferView, IDataView
        {
            public V8DataView(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }
        }

        #endregion

        #region Nested type: V8TypedArray

        private class V8TypedArray : V8ArrayBufferView, ITypedArray
        {
            protected V8TypedArray(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            protected IntPtr GetPtrWithIndex(IntPtr pData, ulong index)
            {
                var baseAddr = unchecked((ulong)pData.ToInt64());
                return new IntPtr(unchecked((long)checked(baseAddr + (index * (Size / Length)))));
            }

            #region ITypedArray implementation

            ulong ITypedArray.Length => Length;

            #endregion
        }

        #endregion

        #region Nested type: V8TypedArray<T>

        private class V8TypedArray<T> : V8TypedArray, ITypedArray<T>
        {
            public V8TypedArray(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            #region ITypedArray<T> implementation

            T[] ITypedArray<T>.ToArray()
            {
                var length = Length;
                var result = new T[length];
                InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(pData, length, result, 0));
                return result;
            }

            ulong ITypedArray<T>.Read(ulong index, ulong length, T[] destination, ulong destinationIndex)
            {
                var totalLength = Length;
                if (index >= totalLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(GetPtrWithIndex(pData, index), Math.Min(length, totalLength - index), destination, destinationIndex));
            }

            ulong ITypedArray<T>.Write(T[] source, ulong sourceIndex, ulong length, ulong index)
            {
                var totalLength = Length;
                if (index >= totalLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(source, sourceIndex, Math.Min(length, totalLength - index), GetPtrWithIndex(pData, index)));
            }

            #endregion
        }

        #endregion

        #region Nested type: V8UInt16Array

        // special case to support both ITypedArray<ushort> and ITypedArray<char>

        private sealed class V8UInt16Array : V8TypedArray<ushort>, ITypedArray<char>
        {
            public V8UInt16Array(V8ScriptEngine engine, IV8Object target)
                : base(engine, target)
            {
            }

            #region ITypedArray<char> implementation

            char[] ITypedArray<char>.ToArray()
            {
                var length = Length;
                var result = new char[length];
                InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(pData, length, result, 0));
                return result;
            }

            ulong ITypedArray<char>.Read(ulong index, ulong length, char[] destination, ulong destinationIndex)
            {
                var totalLength = Length;
                if (index >= totalLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(GetPtrWithIndex(pData, index), Math.Min(length, totalLength - index), destination, destinationIndex));
            }

            ulong ITypedArray<char>.Write(char[] source, ulong sourceIndex, ulong length, ulong index)
            {
                var totalLength = Length;
                if (index >= totalLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return InvokeWithDirectAccess(pData => UnmanagedMemoryHelpers.Copy(source, sourceIndex, Math.Min(length, totalLength - index), GetPtrWithIndex(pData, index)));
            }

            #endregion
        }

        #endregion
    }
}
