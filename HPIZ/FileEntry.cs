using System;
using System.IO;
using System.Linq;

namespace HPIZ
{
    public class FileEntry
    {
        private HpiArchive parent;
        public uint OffsetOfCompressedData;
        public int UncompressedSize;
        public CompressionMethod FlagCompression;
        public int[] compressedChunkSizes;
        public MemoryStream[] ChunkBytes;

        public FileEntry(BinaryReader reader, HpiArchive parentArchive)
        {
            OffsetOfCompressedData = reader.ReadUInt32();
            UncompressedSize = reader.ReadInt32();
            FlagCompression = (CompressionMethod) reader.ReadByte();
            parent = parentArchive;
        }

        public FileEntry (byte[] uncompressedBytes, CompressionFlavor flavor, string fileName, IProgress<string> progress)
        {
            UncompressedSize = uncompressedBytes.Length;
            if (flavor != CompressionFlavor.StoreUncompressed && uncompressedBytes.Length > Strategy.DeflateBreakEven) //Skip compression of small files
            {
                if (uncompressedBytes.Length > Chunk.MaxSize) //Split into chunks and compress
                {
                    compressedChunkSizes = new int[CalculateChunkQuantity()];
                    ChunkBytes = new MemoryStream[compressedChunkSizes.Length];

                    // Parallelize chunk compression start
                    System.Threading.Tasks.Parallel.For(0, compressedChunkSizes.Length, j =>
                    {
                        int chunkSize = Chunk.MaxSize;
                        if (j + 1 == compressedChunkSizes.Length && uncompressedBytes.Length != Chunk.MaxSize) chunkSize = uncompressedBytes.Length % Chunk.MaxSize; //Last loop

                        var uncompressedChunk = new byte[chunkSize];
                        Buffer.BlockCopy(uncompressedBytes, Chunk.MaxSize * j, uncompressedChunk, 0, chunkSize);

                        ChunkBytes[j] = Chunk.Compress(uncompressedChunk, flavor);

                        FlagCompression = CompressionMethod.ZLib;
                        compressedChunkSizes[j] = (int) ChunkBytes[j].Length;

                        if (progress != null)
                            progress.Report(fileName + ":Chunk#" + j.ToString());
                    }); // Parallel.For end 
                }
                else //Single chunk
                {
                    ChunkBytes = new MemoryStream[1];
                    ChunkBytes[0] = Chunk.Compress(uncompressedBytes, flavor);

                    if (Strategy.TryCompressKeepIfWorthwhile && ChunkBytes[0].Length + 4 > uncompressedBytes.Length)
                    {
                        FlagCompression = CompressionMethod.None;
                        ChunkBytes[0] = new MemoryStream(uncompressedBytes);
                    }
                    else
                    {
                        FlagCompression = CompressionMethod.ZLib;
                        compressedChunkSizes = new int[] { (int) ChunkBytes[0].Length };
                    }

                    if (progress != null)
                        progress.Report(fileName + ":Chunk#0");
                }
            }
            else
            {
                FlagCompression = CompressionMethod.None;
                ChunkBytes = new MemoryStream[1];
                ChunkBytes[0] = new MemoryStream(uncompressedBytes);
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
