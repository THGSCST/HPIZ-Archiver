using CompressSharper.Zopfli;
using HPIZ.Compression;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace HPIZ
{
    public class Chunk
    {
        private const int Header = 0x48535153; //SQSH (SQUASH)
        private const byte DefaultVersion = 2; //Always 2?
        public const int SizeOfChunk = 19;

        public CompressionMethod CompMethod;
        public bool IsObfuscated;
        public int CompressedSize; // the length of the compressed data
        public int DecompressedSize; // the length of the decompressed data
        public byte[] Data;
        public Chunk(byte[] chunkData)
        {
            BinaryReader hr = new BinaryReader(new MemoryStream(chunkData));
            int headerMark = hr.ReadInt32();
            if (headerMark != Chunk.Header) throw new InvalidDataException("Invalid Chunk Header");

            int version = hr.ReadByte();
            if (version != DefaultVersion) throw new NotImplementedException("Unsuported Chunk Version");

            CompMethod = (CompressionMethod)hr.ReadByte();
            IsObfuscated = hr.ReadBoolean();
            CompressedSize = hr.ReadInt32();
            DecompressedSize = hr.ReadInt32();

            if (CompMethod == CompressionMethod.None && CompressedSize != DecompressedSize)
                throw new Exception("Chunk size inconsistent with decompressed and compressed sizes");

            int checksum = hr.ReadInt32();

            Data = new byte[CompressedSize];
            hr.Read(Data, 0, CompressedSize);


            if (ComputeChecksum() != checksum) throw new InvalidDataException("Bad Chunk Checksum");

            if (IsObfuscated)
                for (int j = 0; j < CompressedSize; ++j)
                    Data[j] = (byte)((Data[j] - j) ^ j);

        }

        public Chunk(byte[] data, bool toREMOVE)
        {
            Data = data;
            CompMethod = CompressionMethod.None;
            IsObfuscated = false;
            CompressedSize = data.Length;
            DecompressedSize = data.Length;
        }

        private int ComputeChecksum()
        {
            int sum = 0;
            for (int i = 0; i < Data.Length; ++i)
                sum += Data[i];
            return sum;
        }

        public void WriteBytes(BinaryWriter writer)
        {
            writer.Write(Chunk.Header);
            writer.Write(Chunk.DefaultVersion);
            writer.Write((byte)CompMethod);
            writer.Write(IsObfuscated);
            writer.Write(CompressedSize);
            writer.Write(DecompressedSize);
            writer.Write(ComputeChecksum());
            writer.Write(Data);
        }

        public void Compress(bool useZopfli)
        {

            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78); //ZLib header first byte
                ms.WriteByte(0xDA); //ZLib header second byte
                if (useZopfli)
                {
                    ZopfliDeflater zstream = new ZopfliDeflater(ms);

                    zstream.NumberOfIterations = 10;
                    zstream.MasterBlockSize = 0;
                    zstream.Deflate(Data, true);
                }
                else
                    using (DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Compress, true))
                        deflateStream.Write(Data, 0, Data.Length);
                    
                if (ms.Length < Data.Length) //Check if compression was effective, if not leave this chunk uncompressed.
                {
                    Data = ms.ToArray();
                    CompressedSize = Data.Length;
                    CompMethod = CompressionMethod.ZLib;
                }
                    
                }
            }

        public void Decompress()
        {

            var outputBuffer = new byte[DecompressedSize];
            if (CompMethod == CompressionMethod.LZ77)
            {
                LZ77.Decompress(Data, outputBuffer);
                Data = outputBuffer;
            }
            if (CompMethod == CompressionMethod.ZLib)
            {
                ZLibDeflater.Decompress(Data, outputBuffer);
                Data = outputBuffer;
            }
            CompMethod = CompressionMethod.None;
        }





    }
}