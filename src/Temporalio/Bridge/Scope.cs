using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    internal sealed class Scope : IDisposable
    {
        private readonly IList<object> toKeepAlive = new List<object>();

        public Interop.ByteArrayRef ByteArray(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = new ByteArrayRef(bytes);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        public Interop.ByteArrayRef ByteArray(string? str)
        {
            if (str == null || str.Length == 0)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromUTF8(str);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        public Interop.ByteArrayRef Metadata(IEnumerable<KeyValuePair<string, string>>? metadata)
        {
            if (metadata == null)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromMetadata(metadata);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        public Interop.ByteArrayRef NewlineDelimited(IEnumerable<string>? values)
        {
            if (values == null)
            {
                return ByteArrayRef.Empty.Ref;
            }
            var val = ByteArrayRef.FromNewlineDelimited(values);
            toKeepAlive.Add(val);
            return val.Ref;
        }

        public unsafe Interop.CancellationToken* CancellationToken(
            System.Threading.CancellationToken? token
        )
        {
            if (token == null)
            {
                return null;
            }
            var val = Temporalio.Bridge.CancellationToken.FromThreading(token.Value);
            toKeepAlive.Add(val);
            return val.ptr;
        }

        public unsafe T* Pointer<T>(T value) where T : unmanaged
        {
            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            toKeepAlive.Add(handle);
            return (T*)handle.AddrOfPinnedObject();
        }

        public IntPtr FunctionPointer<T>(T func) where T : Delegate
        {
            toKeepAlive.Add(func);
            return Marshal.GetFunctionPointerForDelegate(func);
        }

        public void Dispose()
        {
            foreach (var v in toKeepAlive)
            {
                if (v is GCHandle handle)
                {
                    handle.Free();
                }
            }
            // This keep alive does nothing obviously, but it's good documentation to
            // understand the purpose of this separate dispose call
            GC.KeepAlive(toKeepAlive);
            GC.SuppressFinalize(this);
        }
    }
}
