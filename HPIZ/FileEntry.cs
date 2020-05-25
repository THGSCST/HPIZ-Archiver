using System.IO;
using System.Linq;

namespace HPIZ
{
    public class FileEntry
    {
        private HpiArchive parent;
        public int OffsetOfCompressedData;
        public int UncompressedSize;
        public CompressionMethod FlagCompression;
        public int[] compressedChunkSizes;
        public byte[][] ChunkBytes;

        public FileEntry(BinaryReader reader, HpiArchive parentArchive)
        {
            OffsetOfCompressedData = reader.ReadInt32();
            UncompressedSize = reader.ReadInt32();
            FlagCompression = (CompressionMethod) reader.ReadByte();
            parent = parentArchive;
        }

        public FileEntry(int uncompressedSize, CompressionMethod flagCompression)
        {
            UncompressedSize = uncompressedSize;
            FlagCompression = flagCompression;
            if (FlagCompression != CompressionMethod.None)
                compressedChunkSizes = new int[CalculateChunkQuantity()];
        }

        internal byte[][] GetUncompressedChunkBytes()
        {
            BinaryReader reader = new BinaryReader(parent.archiveStream);
            reader.BaseStream.Position = OffsetOfCompressedData;
            if(FlagCompression != CompressionMethod.None)
                reader.BaseStream.Position += compressedChunkSizes.Length * 4;
            var outputBytes = new byte[compressedChunkSizes.Length][];
            for (int i = 0; i < outputBytes.Length; i++)
            {
                outputBytes[i] = Chunk.Decompress( new MemoryStream(reader.ReadBytes(compressedChunkSizes[i])));
            }

            return outputBytes;
        }

        public int CompressedSizeCount()
        {
            if (FlagCompression == CompressionMethod.None)
                return UncompressedSize;
            else
                return compressedChunkSizes.Sum() + compressedChunkSizes.Length * 4 + Chunk.OverheadSize;
        }

        public float Ratio()
        {
            if (compressedChunkSizes == null || UncompressedSize < 1)
                return 1;
            else
                return (float) CompressedSizeCount() / UncompressedSize;
        }

        public static int CalculateChunkQuantity(int size)
        {
            return (size + 65535) / 65536;
        }

        public int CalculateChunkQuantity()
        {
            return CalculateChunkQuantity(UncompressedSize);
        }
    }
}
