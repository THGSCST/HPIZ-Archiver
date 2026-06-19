using System;
using System.IO;

namespace HPIZ
{
    internal sealed class BinaryBuffer
    {
        public byte[] Bytes { get; }
        public int Offset { get; }
        public int Count { get; }

        public BinaryBuffer(byte[] bytes)
            : this(bytes, 0, bytes?.Length ?? 0)
        {
        }

        public BinaryBuffer(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (offset < 0 || count < 0 || offset > bytes.Length - count)
                throw new ArgumentOutOfRangeException();

            Bytes = bytes;
            Offset = offset;
            Count = count;
        }

        public void WriteTo(Stream destination)
        {
            destination.Write(Bytes, Offset, Count);
        }
    }
}
