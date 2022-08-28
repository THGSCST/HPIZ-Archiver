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

        internal static MemoryStream Compress(byte[] bytesToCompress, CompressionMethod flavor)
        {
            if (bytesToCompress == null)
                throw new InvalidDataException("Cannot compress null array");

            if (flavor == CompressionMethod.StoreUncompressed)
                throw new InvalidOperationException("Chunk format cannot be used for uncompressed data");

            MemoryStream output = new MemoryStream(bytesToCompress.Length);
            BinaryWriter writer = new BinaryWriter(output);

            writer.BaseStream.Position = OverheadSize;
            writer.Write((byte) 0x78); //ZLib header first byte
            writer.Write((byte) 0xDA); //ZLib header second byte

            switch (flavor)
            {
                case CompressionMethod.ZLibDeflate:
                    using (DeflateStream deflateStream = new DeflateStream(output, CompressionLevel.Optimal, true))
                        deflateStream.Write(bytesToCompress, 0, bytesToCompress.Length);
                    break;

                case CompressionMethod.i5ZopfliDeflate:
                case CompressionMethod.i10ZopfliDeflate:
                case CompressionMethod.i15ZopfliDeflate:

                    if(bytesToCompress.Length < Strategy.ZopfliBreakEven) //Skip Zopfli if chunk is small
                        goto case CompressionMethod.ZLibDeflate;

                    ZopfliDeflater zstream = new ZopfliDeflater(output);
                    zstream.NumberOfIterations = (int)flavor;
                    zstream.MasterBlockSize = 0;
                    zstream.Deflate(bytesToCompress, true);
                    break;

                case CompressionMethod.LZ77:
                    throw new NotImplementedException("LZ77 compression not implemented");

                default:
                    throw new InvalidOperationException("Unknow compression method");
            }

            WriteAdler32(bytesToCompress, output);
            
            output.Position = 0;
            writer.Write(Chunk.Header);
            writer.Write(Chunk.DefaultVersion);
            writer.Write((byte)CompressionMethod.ZLibDeflate);
            writer.Write(NoObfuscation);
            writer.Write((int) output.Length - OverheadSize);
            writer.Write(bytesToCompress.Length);

            output.Position = OverheadSize;
            int checksum = ComputeChecksum(output);
            output.Position = 15;
            writer.Write(checksum);
            
            return output;
        }

        internal static byte[] Decompress(MemoryStream bytesToDecompress)
        {
            BinaryReader reader = new BinaryReader(bytesToDecompress);
            int headerMark = reader.ReadInt32();
            if (headerMark != Header) throw new InvalidDataException("Invalid Chunk Header");

            int version = reader.ReadByte();
            if (version != DefaultVersion) throw new NotImplementedException("Unsuported Chunk Version");

            CompressionMethod FlagCompression = (CompressionMethod)reader.ReadByte();
            if (FlagCompression != CompressionMethod.LZ77 && FlagCompression != CompressionMethod.ZLibDeflate)
                throw new InvalidOperationException("Unknown compression method in Chunk header");

            bool IsObfuscated = reader.ReadBoolean();
            int CompressedSize = reader.ReadInt32();
            int DecompressedSize = reader.ReadInt32();
            int checksum = reader.ReadInt32();

            byte[] compressedData = reader.ReadBytes(CompressedSize);

            if (ComputeChecksum(compressedData) != checksum) throw new InvalidDataException("Bad Chunk Checksum");

            if (IsObfuscated)
                for (int j = 0; j < CompressedSize; ++j)
                    compressedData[j] = (byte)((compressedData[j] - j) ^ j);

            byte[] outputBuffer = new byte[DecompressedSize];
            if (FlagCompression == CompressionMethod.LZ77)
                LZ77.Decompress(compressedData, outputBuffer);

            if (FlagCompression == CompressionMethod.ZLibDeflate)
                ZLibDeflater.Decompress(compressedData, outputBuffer);

            return outputBuffer;
        }

        private static int ComputeChecksum(byte[] data)
        {
            int sum = 0;
            for (int i = 0; i < data.Length; ++i)
                sum += data[i];
            return sum;
        }

        private static int ComputeChecksum(Stream data)
        {
            int sum = 0;
            int b = 0;
            while (b != -1)
            {
                sum += b;
                b = data.ReadByte();
            }
            return sum;
        }

        private static void WriteAdler32(byte[] data, Stream output)
        {
            ulong s1 = 1;
            ulong s2 = 0;
            for (int i = 0; i < data.Length; i++) //Maximum SUM operations without MOD is 380368695, big enough for the chunk size
            {
                s1 += data[i];
                s2 += s1;
            }
            s1 %= 65521;
            s2 %= 65521;

            uint sum = (uint)((s2 << 16) | s1);

            var outputBytes = BitConverter.GetBytes(sum);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(outputBytes);

            output.Write(outputBytes, 0, outputBytes.Length);
        }
    }
}