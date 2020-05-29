using System;
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

        public FileEntry (byte[] uncompressedBytes, CompressionFlavor flavor, string fileName, IProgress<string> progress)
        {
            UncompressedSize = uncompressedBytes.Length;
            if (flavor != CompressionFlavor.StoreUncompressed && uncompressedBytes.Length > Strategy.DeflateBreakEven) //Skip compression of small files
            {
                compressedChunkSizes = new int[CalculateChunkQuantity()];
                ChunkBytes = new byte[compressedChunkSizes.Length][];

                // Parallelize chunk compression
                System.Threading.Tasks.Parallel.For(0, compressedChunkSizes.Length, j =>
                {
                    int size = Chunk.MaxSize;
                    if (j + 1 == compressedChunkSizes.Length && uncompressedBytes.Length != Chunk.MaxSize) size = uncompressedBytes.Length % Chunk.MaxSize; //Last loop

                    using (var ms = new MemoryStream(uncompressedBytes, j * Chunk.MaxSize, size))
                    {
                        ChunkBytes[j] = Chunk.Compress(ms.ToArray(), flavor);

                        if(Strategy.TryCompressKeepIfWorthwhile && compressedChunkSizes.Length == 1 && ChunkBytes[j].Length + 4 > ms.Length)
                        {
                            FlagCompression = CompressionMethod.None;
                            ChunkBytes[0] = uncompressedBytes;
                        }
                        else
                        {
                            FlagCompression = CompressionMethod.ZLib;
                            compressedChunkSizes[j] = ChunkBytes[j].Length;
                        }

                    }

                    if (progress != null)
                        progress.Report(fileName + ":Chunk#" + j.ToString());

                }); // Parallel.For                    

            }
            else
            {
                FlagCompression = CompressionMethod.None;
                ChunkBytes = new byte[1][];
                ChunkBytes[0] = uncompressedBytes;
                if (progress != null)
                    progress.Report(fileName);
            }
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
                return compressedChunkSizes.Sum() + compressedChunkSizes.Length * 4;
        }

        public float Ratio()
        {
            if (compressedChunkSizes == null || UncompressedSize == 0)
                return 1;
            else
                return (float)CompressedSizeCount() / UncompressedSize;
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
