using System.IO;
using System.Linq;

namespace HPIZ
{
    public class FileEntry
    {
        public int OffsetOfCompressedData;
        public int UncompressedSize;
        public CompressionMethod FlagCompression;
        public int[] ChunkSizes;

        public FileEntry(BinaryReader reader)
        {
            OffsetOfCompressedData = reader.ReadInt32();
            UncompressedSize = reader.ReadInt32();
            FlagCompression = (CompressionMethod) reader.ReadByte();
        }

        public FileEntry()
        {
        }

        public FileEntry(int uncompressedSize, CompressionMethod flagCompression, int[] chunkSizes)
        {
            UncompressedSize = uncompressedSize;
            FlagCompression = flagCompression;
            ChunkSizes = chunkSizes;
        }

        public int CompressedSizeCount()
        {
            if (FlagCompression == CompressionMethod.None)
                return UncompressedSize;
            else
                return ChunkSizes.Sum() + ChunkSizes.Length * 4 + Chunk.MinSize;
        }

        public float Ratio()
        {
            if (ChunkSizes == null || UncompressedSize < 1)
                return 1;
            else
                return (float) CompressedSizeCount() / UncompressedSize;
        }

        public int CalculateChunkQuantity()
        {
            return (UncompressedSize / 65536) + (UncompressedSize % 65536 == 0 ? 0 : 1);
        }
    }
}
