using System;
using System.IO;
using System.IO.Compression;

namespace HPIZ
{
    public static class ZLibDeflater
    {
        public static void Decompress(byte[] input, byte[] output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int header = input.Length >= 2 ? (input[0] << 8) | input[1] : 0;
            if (input.Length < 2
                || (input[0] & 0x0F) != 8
                || (input[0] >> 4) > 7
                || header % 31 != 0
                || (input[1] & 0x20) != 0)
                throw new InvalidDataException("Unexpected ZLib header");

            using (var inputStream = new DeflateStream(new MemoryStream(input, 2, input.Length - 2), CompressionMode.Decompress))
            {
                int totalBytesRead = 0;
                while (totalBytesRead < output.Length)
                {
                    int bytesRead = inputStream.Read(output, totalBytesRead, output.Length - totalBytesRead);
                    if (bytesRead == 0)
                        throw new EndOfStreamException("The compressed stream ended before the expected output size was reached.");

                    totalBytesRead += bytesRead;
                }

                if (inputStream.ReadByte() != -1)
                    throw new InvalidDataException("The compressed stream expands beyond the expected output size.");
            }
        }
    }
}
