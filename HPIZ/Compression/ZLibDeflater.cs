using System;
using System.IO;
using System.IO.Compression;

namespace HPIZ
{
    public static class ZLibDeflater
    {
        public static void Decompress(byte[] input, byte[] output)
        {
            if (input == null || output == null)
                throw new NullReferenceException("Input or output cannot be null");

            if (input[0] != 0x78 && input[1] != 0x5E && input[1] != 0x9C && input[1] != 0xDA)
                throw new InvalidDataException("Unexpected ZLib header");

            using (var inputStream = new DeflateStream(new MemoryStream(input, 2, input.Length - 2), CompressionMode.Decompress))
            {
                inputStream.Read(output, 0, output.Length);
            }
        }
    }
}
