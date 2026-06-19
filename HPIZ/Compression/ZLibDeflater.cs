using System;
using System.IO;
using System.IO.Compression;

namespace HPIZ
{
    public static class ZLibDeflater
    {
        public static void Decompress(byte[] input, byte[] output)
        {
            Decompress(input, 0, input?.Length ?? 0, output, 0, output?.Length ?? 0);
        }

        internal static void Decompress(
            byte[] input,
            int inputOffset,
            int inputCount,
            byte[] output,
            int outputOffset,
            int outputCount)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (inputOffset < 0 || inputCount < 0 || inputOffset > input.Length - inputCount)
                throw new ArgumentOutOfRangeException();
            if (outputOffset < 0 || outputCount < 0 || outputOffset > output.Length - outputCount)
                throw new ArgumentOutOfRangeException();

            int header = inputCount >= 2 ? (input[inputOffset] << 8) | input[inputOffset + 1] : 0;
            if (inputCount < 2
                || (input[inputOffset] & 0x0F) != 8
                || (input[inputOffset] >> 4) > 7
                || header % 31 != 0
                || (input[inputOffset + 1] & 0x20) != 0)
                throw new InvalidDataException("Unexpected ZLib header");

            using (var compressedStream = new MemoryStream(
                input,
                inputOffset + 2,
                inputCount - 2,
                false,
                true))
            using (var inputStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                int totalBytesRead = 0;
                while (totalBytesRead < outputCount)
                {
                    int bytesRead = inputStream.Read(
                        output,
                        outputOffset + totalBytesRead,
                        outputCount - totalBytesRead);
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
