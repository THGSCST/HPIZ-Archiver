using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HPIZ
{
    public class FileEntry
    {
        private const int MinimumChunksForParallelCompression = 4;
        private const int MinimumChunksForParallelDecompression = 4;
        // Chunk work is independent, but using every logical processor creates excessive
        // scheduling and memory pressure on high-core-count and hybrid CPUs.
        private static readonly ParallelOptions ChunkParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 16))
        };

        private HpiArchive parent;
        public uint CompressedDataOffset;
        public int UncompressedSize;
        public CompressionMethod FlagCompression;
        public int[] CompressedChunkSizes;
        internal BinaryBuffer[] ChunkBytes;

        public FileEntry(BinaryReader reader, HpiArchive parentArchive)
        {
            CompressedDataOffset = reader.ReadUInt32();
            UncompressedSize = reader.ReadInt32();
            FlagCompression = (CompressionMethod)reader.ReadByte();
            if (UncompressedSize < 0)
                throw new InvalidDataException("Archive entry has a negative uncompressed size.");
            if (FlagCompression != CompressionMethod.StoreUncompressed
                && FlagCompression != CompressionMethod.LZ77
                && FlagCompression != CompressionMethod.ZLibDeflate)
                throw new InvalidDataException("Archive entry has an unknown compression method.");

            parent = parentArchive;
        }

        public FileEntry(byte[] uncompressedBytes, CompressionMethod flavor, string reportProgressFileName, IProgress<string> progress)
            : this(uncompressedBytes, flavor, reportProgressFileName, progress, true)
        {
        }

        internal FileEntry(
            byte[] uncompressedBytes,
            CompressionMethod flavor,
            string reportProgressFileName,
            IProgress<string> progress,
            bool parallelizeChunks)
        {
            UncompressedSize = uncompressedBytes.Length;
            if (flavor != CompressionMethod.StoreUncompressed && uncompressedBytes.Length > Strategy.DeflateBreakEven) //Skip compression of small files
            {
                if (uncompressedBytes.Length > Chunk.MaxSize) //Split into chunks
                {
                    CompressedChunkSizes = new int[CalculateChunkQuantity()];
                    ChunkBytes = new BinaryBuffer[CompressedChunkSizes.Length];
                    FlagCompression = CompressionMethod.ZLibDeflate;

                    Action<int> compressChunk = j =>
                    {
                        int remainingBytes = uncompressedBytes.Length - (Chunk.MaxSize * j);
                        int chunkSize = Math.Min(Chunk.MaxSize, remainingBytes);

                        ChunkBytes[j] = Chunk.Compress(
                            uncompressedBytes,
                            Chunk.MaxSize * j,
                            chunkSize,
                            flavor);

                        CompressedChunkSizes[j] = ChunkBytes[j].Count;

                        if (progress != null)
                            progress.Report(reportProgressFileName + ":Chunk#" + j.ToString());
                    };

                    // Zopfli itself is intentionally sequential within a chunk. Parallelism
                    // belongs here, between independent chunks, so it is never nested.
                    if (parallelizeChunks
                        && CompressedChunkSizes.Length >= MinimumChunksForParallelCompression)
                        Parallel.For(0, CompressedChunkSizes.Length, ChunkParallelOptions, compressChunk);
                    else
                        for (int j = 0; j < CompressedChunkSizes.Length; j++)
                            compressChunk(j);
                }
                else //Single chunk
                {
                    ChunkBytes = new BinaryBuffer[1];
                    ChunkBytes[0] = Chunk.Compress(uncompressedBytes, 0, uncompressedBytes.Length, flavor);

                    if (Strategy.TryCompressKeepIfWorthwhile && ChunkBytes[0].Count + 4 > uncompressedBytes.Length)
                    {
                        FlagCompression = CompressionMethod.StoreUncompressed;
                        ChunkBytes[0] = new BinaryBuffer(uncompressedBytes);
                    }
                    else
                    {
                        FlagCompression = CompressionMethod.ZLibDeflate;
                        CompressedChunkSizes = new int[] { ChunkBytes[0].Count };
                    }

                    if (progress != null)
                        progress.Report(reportProgressFileName + ":Chunk#0");
                }
            }
            else
            {
                FlagCompression = CompressionMethod.StoreUncompressed;
                ChunkBytes = new BinaryBuffer[1];
                ChunkBytes[0] = new BinaryBuffer(uncompressedBytes);
                if (progress != null)
                    progress.Report(reportProgressFileName);
            }
        }

        public byte[] Uncompress()
        {
            if (FlagCompression == CompressionMethod.StoreUncompressed)
            {
                var uncompressedOutput = new byte[UncompressedSize];
                parent.ReadExactlyAt(CompressedDataOffset, uncompressedOutput, 0, UncompressedSize);

                if (parent.obfuscationKey != 0)
                    parent.Clarify(uncompressedOutput, (int)CompressedDataOffset);

                return uncompressedOutput;
            }

            if (FlagCompression != CompressionMethod.LZ77 && FlagCompression != CompressionMethod.ZLibDeflate)
                throw new Exception("Unknown compression method in file entry");

            var chunkCount = CompressedChunkSizes.Length;
            var readPositions = new int[chunkCount];
            for (int i = 1; i < chunkCount; i++)
                readPositions[i] = readPositions[i - 1] + CompressedChunkSizes[i - 1];

            long strReadPositions = CompressedDataOffset + (chunkCount * 4);
            int compressedBytes = CompressedChunkSizes.Sum();
            var chunkBuffer = new byte[compressedBytes];
            parent.ReadExactlyAt(strReadPositions, chunkBuffer, 0, compressedBytes);

            var outBytes = new byte[UncompressedSize];

            Action<int> decompressChunk = i =>
            {
                if (parent.obfuscationKey != 0)
                    parent.Clarify(chunkBuffer, (int)(readPositions[i] + strReadPositions), readPositions[i], CompressedChunkSizes[i]);

                Debug.WriteLine(CompressedDataOffset.ToString());

                Debug.Assert(CompressedDataOffset != 0);

                Chunk.Decompress(
                    chunkBuffer,
                    readPositions[i],
                    CompressedChunkSizes[i],
                    outBytes,
                    i * Chunk.MaxSize);
            };

            if (chunkCount >= MinimumChunksForParallelDecompression)
                Parallel.For(0, chunkCount, ChunkParallelOptions, decompressChunk);
            else
                for (int i = 0; i < chunkCount; i++)
                    decompressChunk(i);

            return outBytes;
        }

        public int CompressedSizeCount()
        {
            if (FlagCompression == CompressionMethod.StoreUncompressed)
                return UncompressedSize;
            else
                return CompressedChunkSizes.Sum() + CompressedChunkSizes.Length * 4;
        }

        public float Ratio()
        {
            if (CompressedChunkSizes == null || UncompressedSize == 0)
                return 1;
            else
                return (float)CompressedSizeCount() / UncompressedSize;
        }

        public static int CalculateChunkQuantity(int size)
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            return (int)((size + (long)Chunk.MaxSize - 1) / Chunk.MaxSize);
        }

        public int CalculateChunkQuantity()
        {
            return CalculateChunkQuantity(UncompressedSize);
        }
    }
}
