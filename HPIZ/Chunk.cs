using CompressSharper.Zopfli;
using System;
using System.IO;
using System.IO.Compression;

namespace HPIZ
{
    internal static class Chunk
    {
        private const int Header = 0x48535153; //SQSH (SQUASH)
        private const byte DefaultVersion = 2; //Always 2?
        public const int OverheadSize = 19; //Chunk structure minimum size in bytes
        public const int MaxSize = 65536; //Maximum chunk size in bytes
        private const byte NoObfuscation = 0;

        internal static BinaryBuffer Compress(byte[] input, int inputOffset, int inputCount, CompressionMethod flavor)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (inputOffset < 0 || inputCount < 0 || inputOffset > input.Length - inputCount)
                throw new ArgumentOutOfRangeException();

            if (flavor == CompressionMethod.StoreUncompressed)
                throw new InvalidOperationException("Chunk format cannot be used for uncompressed data");

            // DEFLATE can be slightly larger than incompressible input. Reserve enough
            // headroom to avoid MemoryStream doubling its backing array.
            var output = new MemoryStream(checked(inputCount + OverheadSize + 128));
            output.Position = OverheadSize;
            output.WriteByte(0x78);
            output.WriteByte(0xDA);

            switch (flavor)
            {
                case CompressionMethod.ZLibDeflate:
                    using (DeflateStream deflateStream = new DeflateStream(output, CompressionLevel.Optimal, true))
                        deflateStream.Write(input, inputOffset, inputCount);
                    break;

                case CompressionMethod.i5ZopfliDeflate:
                case CompressionMethod.i10ZopfliDeflate:
                case CompressionMethod.i15ZopfliDeflate:

                    if (inputCount < Strategy.ZopfliBreakEven) //Skip Zopfli if chunk is small
                        goto case CompressionMethod.ZLibDeflate;

                    byte[] zopfliInput;
                    if (inputOffset == 0 && inputCount == input.Length)
                    {
                        zopfliInput = input;
                    }
                    else
                    {
                        zopfliInput = new byte[inputCount];
                        Buffer.BlockCopy(input, inputOffset, zopfliInput, 0, inputCount);
                    }

                    ZopfliDeflater zstream = new ZopfliDeflater(output);
                    zstream.NumberOfIterations = (int)flavor;
                    zstream.MasterBlockSize = 0;
                    zstream.Deflate(zopfliInput, true);
                    break;

                case CompressionMethod.LZ77:
                    throw new NotImplementedException("LZ77 compression not implemented");

                default:
                    throw new InvalidOperationException("Unknow compression method");
            }

            WriteAdler32(input, inputOffset, inputCount, output);

            byte[] outputBytes = output.GetBuffer();
            int outputLength = checked((int)output.Length);
            int compressedSize = outputLength - OverheadSize;
            int checksum = ComputeChecksum(outputBytes, OverheadSize, compressedSize);

            WriteInt32LittleEndian(outputBytes, 0, Header);
            outputBytes[4] = DefaultVersion;
            outputBytes[5] = (byte)CompressionMethod.ZLibDeflate;
            outputBytes[6] = NoObfuscation;
            WriteInt32LittleEndian(outputBytes, 7, compressedSize);
            WriteInt32LittleEndian(outputBytes, 11, inputCount);
            WriteInt32LittleEndian(outputBytes, 15, checksum);

            output.Dispose();
            return new BinaryBuffer(outputBytes, 0, outputLength);
        }

        internal static int Decompress(
            byte[] input,
            int inputOffset,
            int inputCount,
            byte[] output,
            int outputOffset)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (inputOffset < 0 || inputCount < 0 || inputOffset > input.Length - inputCount)
                throw new ArgumentOutOfRangeException();
            if (inputCount < OverheadSize)
                throw new InvalidDataException("Chunk is smaller than its header.");

            int headerMark = ReadInt32LittleEndian(input, inputOffset);
            if (headerMark != Header) throw new InvalidDataException("Invalid Chunk Header");

            int version = input[inputOffset + 4];
            if (version != DefaultVersion) throw new NotImplementedException("Unsuported Chunk Version");

            CompressionMethod FlagCompression = (CompressionMethod)input[inputOffset + 5];
            if (FlagCompression != CompressionMethod.LZ77 && FlagCompression != CompressionMethod.ZLibDeflate)
                throw new InvalidOperationException("Unknown compression method in Chunk header");

            bool IsObfuscated = input[inputOffset + 6] != 0;
            int CompressedSize = ReadInt32LittleEndian(input, inputOffset + 7);
            int DecompressedSize = ReadInt32LittleEndian(input, inputOffset + 11);
            int checksum = ReadInt32LittleEndian(input, inputOffset + 15);

            if (CompressedSize < 0 || CompressedSize > inputCount - OverheadSize)
                throw new InvalidDataException("Invalid compressed chunk size.");
            if (DecompressedSize < 0 || DecompressedSize > MaxSize)
                throw new InvalidDataException("Invalid decompressed chunk size.");
            if (outputOffset < 0 || outputOffset > output.Length - DecompressedSize)
                throw new InvalidDataException("Decompressed chunk exceeds the output buffer.");

            int compressedOffset = inputOffset + OverheadSize;

            if (ComputeChecksum(input, compressedOffset, CompressedSize) != checksum)
                throw new InvalidDataException("Bad Chunk Checksum");

            if (IsObfuscated)
            {
                byte[] clarifiedData = new byte[CompressedSize];
                for (int j = 0; j < CompressedSize; ++j)
                    clarifiedData[j] = (byte)((input[compressedOffset + j] - j) ^ j);

                input = clarifiedData;
                compressedOffset = 0;
            }

            if (FlagCompression == CompressionMethod.LZ77)
                LZ77.Decompress(input, compressedOffset, CompressedSize, output, outputOffset, DecompressedSize);
            else
                ZLibDeflater.Decompress(input, compressedOffset, CompressedSize, output, outputOffset, DecompressedSize);

            return DecompressedSize;
        }

        private static int ComputeChecksum(byte[] data, int offset, int count)
        {
            int sum = 0;
            int end = offset + count;
            for (int i = offset; i < end; ++i)
                sum += data[i];
            return sum;
        }

        private static void WriteAdler32(byte[] data, int offset, int count, Stream output)
        {
            ulong s1 = 1;
            ulong s2 = 0;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                s1 += data[i];
                s2 += s1;
            }
            s1 %= 65521;
            s2 %= 65521;

            uint sum = (uint)((s2 << 16) | s1);

            output.WriteByte((byte)(sum >> 24));
            output.WriteByte((byte)(sum >> 16));
            output.WriteByte((byte)(sum >> 8));
            output.WriteByte((byte)sum);
        }

        private static int ReadInt32LittleEndian(byte[] buffer, int offset)
        {
            return buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24);
        }

        private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
