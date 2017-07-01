﻿// Copyright 2013-2017 Dirk Lemstra <https://github.com/dlemstra/Magick.NET/>
//
// Licensed under the ImageMagick License (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
//
//   http://www.imagemagick.org/script/license.php
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace ImageMagick
{
    internal sealed unsafe class StreamWrapper : IDisposable
    {
        private const int BufferSize = 8192;

        private readonly byte[] _Buffer;
        private readonly byte* _BufferStart;
        private readonly GCHandle _Handle;
        private Stream _Stream;

        private StreamWrapper(Stream stream)
        {
            _Stream = stream;
            _Buffer = new byte[BufferSize];
            _Handle = GCHandle.Alloc(_Buffer, GCHandleType.Pinned);
            _BufferStart = (byte*)_Handle.AddrOfPinnedObject().ToPointer();
        }

        public static StreamWrapper CreateForReading(Stream stream)
        {
            Throw.IfFalse(nameof(stream), stream.CanRead, "The stream should be readable.");

            return new StreamWrapper(stream);
        }

        public static StreamWrapper CreateForWriting(Stream stream)
        {
            Throw.IfFalse(nameof(stream), stream.CanWrite, "The stream should be writeable.");

            return new StreamWrapper(stream);
        }

        public void Dispose()
        {
            if (_Stream == null)
                return;

            _Handle.Free();
            _Stream = null;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions will result in a memory leak.")]
        public int Read(IntPtr data, UIntPtr count, IntPtr user_data)
        {
            int total = (int)count;
            if (total == 0)
                return 0;

            byte* p = (byte*)data.ToPointer();
            int bytesRead = 0;

            while (total > 0)
            {
                int length = Math.Min(total, BufferSize);

                try
                {
                    length = _Stream.Read(_Buffer, 0, length);
                }
                catch
                {
                    return -1;
                }

                if (length == 0)
                    break;

                bytesRead += length;

                p = ReadBuffer(p, length);

                total -= length;
            }

            return bytesRead;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions will result in a memory leak.")]
        public long Seek(long offset, IntPtr whence, IntPtr user_data)
        {
            try
            {
                return _Stream.Seek(offset, (SeekOrigin)whence);
            }
            catch
            {
                return -1;
            }
        }

        public long Tell(IntPtr user_data)
        {
            return _Stream.Position;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions will result in a memory leak.")]
        public int Write(IntPtr data, UIntPtr count, IntPtr user_data)
        {
            int total = (int)count;
            if (total == 0)
                return 0;

            byte* p = (byte*)data.ToPointer();

            while (total > 0)
            {
                int length = Math.Min(total, BufferSize);

                p = FillBuffer(p, length);

                try
                {
                    _Stream.Write(_Buffer, 0, length);
                }
                catch
                {
                    return -1;
                }

                total -= length;
            }

            return (int)count;
        }

        private byte* FillBuffer(byte* p, int length)
        {
            byte* q = _BufferStart;
            while (length > 0)
            {
                *(q++) = *(p++);
                length--;
            }

            return p;
        }

        private byte* ReadBuffer(byte* p, int length)
        {
            byte* q = _BufferStart;
            while (length > 0)
            {
                *(p++) = *(q++);
                length--;
            }

            return p;
        }
    }
}
