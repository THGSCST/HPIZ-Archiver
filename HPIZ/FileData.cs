using System.IO;
using System.Linq;

namespace HPIZ
{
    public class FileData
    {
        private HpiArchive parentArchive;
        public int OffsetOfCompressedData;
        public int UncompressedSize;
        public CompressionMethod FlagCompression;
        public int[] ChunkSizes;

        public FileData(BinaryReader reader)
        {
            OffsetOfCompressedData = reader.ReadInt32();
            UncompressedSize = reader.ReadInt32();
            FlagCompression = (CompressionMethod)reader.ReadByte();
        }

        public FileData()
        {
        }

        public int CompressedSizeCount()
        {
            return ChunkSizes.Sum();
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
