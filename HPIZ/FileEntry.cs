﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HPIZ
{
    public class FileEntry
    {
        private HpiArchive parent;
        public uint CompressedDataOffset;
        public int UncompressedSize;
        public CompressionMethod FlagCompression;
        public int[] CompressedChunkSizes;
        public MemoryStream[] ChunkBytes;

        public FileEntry(BinaryReader reader, HpiArchive parentArchive)
        {
            CompressedDataOffset = reader.ReadUInt32();
            UncompressedSize = reader.ReadInt32();
            FlagCompression = (CompressionMethod)reader.ReadByte();
            parent = parentArchive;
        }

        public FileEntry(byte[] uncompressedBytes, CompressionMethod flavor, string reportProgressFileName, IProgress<string> progress)
        {
            UncompressedSize = uncompressedBytes.Length;
            if (flavor != CompressionMethod.StoreUncompressed && uncompressedBytes.Length > Strategy.DeflateBreakEven) //Skip compression of small files
            {
                if (uncompressedBytes.Length > Chunk.MaxSize) //Split into chunks
                {
                    CompressedChunkSizes = new int[CalculateChunkQuantity()];
                    ChunkBytes = new MemoryStream[CompressedChunkSizes.Length];

                    // Parallelize chunk compression start
                    Parallel.For(0, CompressedChunkSizes.Length, j =>
                    {
                        int chunkSize = Chunk.MaxSize;
                        if (j + 1 == CompressedChunkSizes.Length && uncompressedBytes.Length != Chunk.MaxSize) chunkSize = uncompressedBytes.Length % Chunk.MaxSize; //Last loop

                        var uncompressedChunk = new byte[chunkSize];
                        Buffer.BlockCopy(uncompressedBytes, Chunk.MaxSize * j, uncompressedChunk, 0, chunkSize);

                        ChunkBytes[j] = Chunk.Compress(uncompressedChunk, flavor);

                        FlagCompression = CompressionMethod.ZLibDeflate;
                        CompressedChunkSizes[j] = (int)ChunkBytes[j].Length;

                        if (progress != null)
                            progress.Report(reportProgressFileName + ":Chunk#" + j.ToString());
                    }); // Parallel.For end 
                }
                else //Single chunk
                {
                    ChunkBytes = new MemoryStream[1];
                    ChunkBytes[0] = Chunk.Compress(uncompressedBytes, flavor);

                    if (Strategy.TryCompressKeepIfWorthwhile && ChunkBytes[0].Length + 4 > uncompressedBytes.Length)
                    {
                        FlagCompression = CompressionMethod.StoreUncompressed;
                        ChunkBytes[0] = new MemoryStream(uncompressedBytes);
                    }
                    else
                    {
                        FlagCompression = CompressionMethod.ZLibDeflate;
                        CompressedChunkSizes = new int[] { (int)ChunkBytes[0].Length };
                    }

                    if (progress != null)
                        progress.Report(reportProgressFileName + ":Chunk#0");
                }
            }
            else
            {
                FlagCompression = CompressionMethod.StoreUncompressed;
                ChunkBytes = new MemoryStream[1];
                ChunkBytes[0] = new MemoryStream(uncompressedBytes);
                if (progress != null)
                    progress.Report(reportProgressFileName);
            }
        }

        internal byte[][] GetUncompressedChunkBytes()
        {
            BinaryReader reader = new BinaryReader(parent.archiveStream);
            reader.BaseStream.Position = CompressedDataOffset;
            if (FlagCompression != CompressionMethod.StoreUncompressed)
                reader.BaseStream.Position += CompressedChunkSizes.Length * 4;
            var outputBytes = new byte[CompressedChunkSizes.Length][];
            for (int i = 0; i < outputBytes.Length; i++)
                outputBytes[i] = Chunk.Decompress(new MemoryStream(reader.ReadBytes(CompressedChunkSizes[i])));

            return outputBytes;
        }

        public byte[] Uncompress()
        {
            BinaryReader reader = new BinaryReader(parent.archiveStream);

            if (FlagCompression == CompressionMethod.StoreUncompressed)
            {
                reader.BaseStream.Position = CompressedDataOffset;
                var uncompressedOutput = reader.ReadBytes(UncompressedSize);
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
            reader.BaseStream.Position = strReadPositions;
            var chunkBuffer = reader.ReadBytes(CompressedChunkSizes.Sum());

            var outBytes = new byte[UncompressedSize];

            // Parallelize chunk decompression
            Parallel.For(0, chunkCount, i =>
            {

                if (parent.obfuscationKey != 0)
                    parent.Clarify(chunkBuffer, (int)(readPositions[i] + strReadPositions), readPositions[i], CompressedChunkSizes[i]);

                Debug.WriteLine(CompressedDataOffset.ToString());

                Debug.Assert(CompressedDataOffset != 0);

                var decompressedChunk = Chunk.Decompress(new MemoryStream(chunkBuffer, readPositions[i], CompressedChunkSizes[i]));

                Buffer.BlockCopy(decompressedChunk, 0, outBytes, i * Chunk.MaxSize, decompressedChunk.Length);
            }); // Parallel.For

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
            return (size + Chunk.MaxSize - 1) / Chunk.MaxSize;
        }

        public int CalculateChunkQuantity()
        {
            return CalculateChunkQuantity(UncompressedSize);
        }
    }
}
