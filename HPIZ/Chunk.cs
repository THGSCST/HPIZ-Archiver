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

        internal static byte[] Compress(byte[] bytesToCompress, CompressionFlavor flavor)
        {
            if (bytesToCompress == null)
                throw new InvalidDataException("Cannot compress null array");

            if (flavor == CompressionFlavor.StoreUncompressed)
                throw new InvalidOperationException("Chunk format cannot be used for uncompressed data");

            MemoryStream output = new MemoryStream(bytesToCompress.Length);
            BinaryWriter writer = new BinaryWriter(output);

            writer.Write(Chunk.Header);
            writer.Write(Chunk.DefaultVersion);
            writer.Write((byte) CompressionMethod.ZLib);
            writer.Write(NoObfuscation);

            using (MemoryStream compressedStream = new MemoryStream(bytesToCompress.Length))
            {
                compressedStream.WriteByte(0x78); //ZLib header first byte
                compressedStream.WriteByte(0xDA); //ZLib header second byte

                switch (flavor)
                {
                    case CompressionFlavor.ZLibDeflate:
                        using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
                            deflateStream.Write(bytesToCompress, 0, bytesToCompress.Length);
                        break;

                    case CompressionFlavor.i5ZopfliDeflate:
                    case CompressionFlavor.i10ZopfliDeflate:
                    case CompressionFlavor.i15ZopfliDeflate:

                        if(bytesToCompress.Length < Strategy.ZopfliBreakEven) //Skip Zopfli if file is small
                            goto case CompressionFlavor.ZLibDeflate;

                        ZopfliDeflater zstream = new ZopfliDeflater(compressedStream);
                        zstream.NumberOfIterations = (int)flavor;
                        zstream.MasterBlockSize = 0;
                        zstream.Deflate(bytesToCompress, true);
                        break;

                    default:
                        throw new InvalidOperationException("Unknow compression flavor");
                }
                var compressedDataArray = compressedStream.ToArray(); //Change to stream
                int checksum = ComputeChecksum(compressedDataArray); //Change to stream

                writer.Write(compressedDataArray.Length);
                writer.Write(bytesToCompress.Length);
                writer.Write(checksum);
                writer.Write(compressedDataArray);
            }
            return output.ToArray();
        }

        internal static byte[] Decompress(MemoryStream bytesToDecompress)
        {
            BinaryReader reader = new BinaryReader(bytesToDecompress);
            int headerMark = reader.ReadInt32();
            if (headerMark != Chunk.Header) throw new InvalidDataException("Invalid Chunk Header");

            int version = reader.ReadByte();
            if (version != DefaultVersion) throw new NotImplementedException("Unsuported Chunk Version");

            CompressionMethod FlagCompression = (CompressionMethod)reader.ReadByte();
            if (FlagCompression != CompressionMethod.LZ77 && FlagCompression != CompressionMethod.ZLib)
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

            if (FlagCompression == CompressionMethod.ZLib)
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

    }
}