// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal class StringBinaryConverter : IBinaryConverter<string>
    {
        public bool IsFixedSize => false;
        public int Size => 0;

        public unsafe int SizeOf(in string value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            fixed (char* charPtr = value)
            {
                var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                temporaryStream = null;
                return totalLength;
            }
        }

        public unsafe int Write(in string value, IntPtr pinnedDestination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (temporaryStream == null)
            {
                fixed (char* charPtr = value)
                {
                    var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);

                    
                    // version
                    var header = new DataTypeHeader
                    {
                        VersionAndFlags = {
                                Version = 0,
                                IsBinary = true,
                                IsDelta = false,
                                IsCompressed = false },
                        TypeEnum = TypeEnum.String
                    };
                    WriteUnaligned((void*)pinnedDestination, header);
                    // payload length
                    WriteUnaligned((void*)(pinnedDestination + 4), totalLength - 8);
                    // payload
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)pinnedDestination + 8, totalLength);
                    Debug.Assert(totalLength == len + 8);

                    return totalLength;
                }
            }

            temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
            return checked((int)temporaryStream.Length);
        }

        public unsafe int Read(IntPtr ptr, out string value)
        {
            var header = ReadUnaligned<DataTypeHeader>((void*)ptr);
            var payloadLength = ReadUnaligned<int>((void*)(ptr + 4));
            
            if (header.VersionAndFlags.Version != 0) throw new NotSupportedException();
            OwnedPooledArray<byte> ownedPooledBuffer = BufferPool.UseTempBuffer(payloadLength);
            var buffer = ownedPooledBuffer.Memory;
            var handle = buffer.Pin();

            try
            {
                Marshal.Copy(ptr + 8, ownedPooledBuffer.Array, 0, payloadLength);

                value = Encoding.UTF8.GetString(ownedPooledBuffer.Array, 0, payloadLength);

                return payloadLength + 8;
            }
            finally
            {
                handle.Dispose();
                if (ownedPooledBuffer != BufferPool.StaticBuffer)
                {
                    Debug.Assert(ownedPooledBuffer.IsDisposed);
                }
                else
                {
                    Debug.Assert(!ownedPooledBuffer.IsDisposed);
                }
            }
        }

        public byte Version => 0;
    }
}