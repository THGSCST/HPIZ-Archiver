using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPIZ
{
    public class HpiArchive : IDisposable
    {
        internal const int HeaderMarker = 0x49504148; //HAPI Header
        internal const int DefaultVersion = 0x00010000;
        internal const int NoObfuscationKey = 0;
        internal const int HeaderSize = 20;

        internal readonly Stream archiveStream;
        private readonly int obfuscationKey;
        private readonly SortedDictionary<string, FileEntry> entriesDictionary;
        public ReadOnlyDictionary<string, FileEntry> Entries { get; }

        public HpiArchive(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            archiveStream = stream;
            BinaryReader archiveReader = new BinaryReader(archiveStream);

            if (archiveReader.ReadInt32() != HeaderMarker)
                throw new InvalidDataException("Header marker HAPI not found");

            int version = archiveReader.ReadInt32();
            if (version == 0x4B4E4142)
                throw new NotImplementedException("Saved game files are not supported");
            if (version == 0x20000)
                throw new NotImplementedException("TAK files are not supported");
            if (version != DefaultVersion)
                throw new NotImplementedException("Unknown version number");

            int directorySize = archiveReader.ReadInt32();

            obfuscationKey = archiveReader.ReadInt32();

            int directoryStart = archiveReader.ReadInt32();

            if (obfuscationKey != 0)
            {
                obfuscationKey = ~(obfuscationKey << 2 | obfuscationKey >> 6);
                
                archiveReader.BaseStream.Position = 0;
                var buffer = archiveReader.ReadBytes(directorySize);
                Clarify(buffer, 0);
                archiveReader = new BinaryReader(new MemoryStream(buffer));
            }

            archiveReader.BaseStream.Position = directoryStart;

            int rootNumberOfEntries = archiveReader.ReadInt32();
            int rootOffset = archiveReader.ReadInt32();

            entriesDictionary = new SortedDictionary<string, FileEntry>();
            Entries = new ReadOnlyDictionary<string, FileEntry>(entriesDictionary);

            GetEntries(rootNumberOfEntries, rootOffset, archiveReader, string.Empty);

            archiveReader = new BinaryReader(archiveStream);


            foreach (var entry in entriesDictionary.Keys)
            {
                if (entriesDictionary[entry].FlagCompression != CompressionMethod.None)
                {
                    int chunkCount = entriesDictionary[entry].CalculateChunkQuantity();
                    archiveStream.Position = entriesDictionary[entry].OffsetOfCompressedData;
                    var buffer = archiveReader.ReadBytes(chunkCount * 4);

                    if (obfuscationKey != 0)
                        Clarify(buffer, (int) entriesDictionary[entry].OffsetOfCompressedData);

                    var size = new int[chunkCount];
                    Buffer.BlockCopy(buffer, 0, size, 0, buffer.Length);

                    entriesDictionary[entry].compressedChunkSizes = size;
                }
                else
                    entriesDictionary[entry].compressedChunkSizes = new int[] { entriesDictionary[entry].UncompressedSize}; //To optimize
            }
        }

        private void GetEntries(int NumberOfEntries, int EntryListOffset, BinaryReader reader, string parentPath)
        {
            for (int i = 0; i < NumberOfEntries; ++i)
            {
                reader.BaseStream.Position = EntryListOffset + (i * 9);

                int nameOffset = reader.ReadInt32();
                int dataOffset = reader.ReadInt32(); //Unused?
                bool IsDirectory = reader.ReadBoolean();
                reader.BaseStream.Position = nameOffset;
                var fullPath = Path.Combine(parentPath, ReadStringCP437NullTerminated(reader));

                if (IsDirectory)
                    GetEntries(reader.ReadInt32(), reader.ReadInt32(), reader, fullPath);
                else
                {
                    FileEntry fd = new FileEntry(reader , this);
                    entriesDictionary.Add(fullPath, fd);
                }
            }
        }

        internal static void SetEntries(DirectoryTree tree, BinaryWriter bw, IEnumerator<FileEntry> sequence)
        {
            bw.Write(tree.Count); //Root Entries number in directory
           
            bw.Write((int)bw.BaseStream.Position + 4); //Entries Offset point to next

            for (int i = 0; i < tree.Count; ++i)
            {
                int posString;
                if (i == 0)
                    posString = (int)bw.BaseStream.Position + (tree.Count - i) * 9;
                else
                    posString = (int)bw.BaseStream.Length; 
                bw.Write(posString); //NameOffset;      /* points to the file name */
                int posNext = posString + tree[i].Key.Length + 1;
                bw.Write(posNext); //DirDataOffset;   /* points to directory data */
                bool isDir = tree[i].Children.Count != 0;
                bw.Write(isDir); 

                int previousPos = (int)bw.BaseStream.Position;
                bw.BaseStream.Position = posString;
                WriteStringCP437NullTerminated(bw, tree[i].Key);
                if (isDir)
                    SetEntries(tree[i].Children, bw, sequence);
                else
                {
                    sequence.MoveNext();
                    bw.Write(sequence.Current.OffsetOfCompressedData); //OffsetOfData
                    bw.Write(sequence.Current.UncompressedSize); //UncompressedSize 
                    bw.Write((byte)sequence.Current.FlagCompression); //FlagCompression 
                    
                }
                bw.BaseStream.Position = previousPos;
            }
        }


        private static string ReadStringCP437NullTerminated(BinaryReader reader)
        {
            var bytes = new Queue<byte>();
            byte b = reader.ReadByte();
            while (b != 0)
            {
                bytes.Enqueue(b);
                b = reader.ReadByte();
            }

            var characters = Encoding.GetEncoding(437).GetChars(bytes.ToArray());

            char[] reserved = { '<', '>', '\"', ':', '/', '\\', '|', '?' , '*',};
 
            for (int i = 0; i < characters.Length; i++)
                if (char.IsControl(characters[i]) || reserved.Contains(characters[i]))
                        characters[i] = '_'; //Replace control or reserved char with underscore

            return new string(characters);
        }

        private static void WriteStringCP437NullTerminated(BinaryWriter reader, string text)
        {
            Encoding codePage437 = Encoding.GetEncoding(437);
            reader.Write(codePage437.GetBytes(text));
            reader.Write(byte.MinValue); //Zero byte to end string
        }

        void Clarify(byte[] obfuscatedBytes, int position, int start = 0, int end = 0)
        {
            if (end == 0) end = obfuscatedBytes.Length;
            else end = end + start;
            for (int i = start; i < end; ++i)
            {
                obfuscatedBytes[i] = (byte) ~(position ^ obfuscationKey ^ obfuscatedBytes[i]);
                position++;
            }
        }

        public byte[] Extract(FileEntry file)
        {
            BinaryReader reader = new BinaryReader(archiveStream);

            if (file.FlagCompression == CompressionMethod.None)
            {
                reader.BaseStream.Position = file.OffsetOfCompressedData;
                var uncompressedOutput = reader.ReadBytes(file.UncompressedSize);

                if (obfuscationKey != 0)
                    Clarify(uncompressedOutput, (int) file.OffsetOfCompressedData);

                return uncompressedOutput;
            }
            
            if(file.FlagCompression != CompressionMethod.LZ77 && file.FlagCompression != CompressionMethod.ZLib)
                throw new Exception("Unknown compression method in file entry");

            var chunkCount = file.compressedChunkSizes.Length;
            var readPositions = new int[chunkCount];
            for (int i = 1; i < chunkCount; i++)
                    readPositions[i] = readPositions[i - 1] + file.compressedChunkSizes[i - 1];
            
            long strReadPositions = file.OffsetOfCompressedData + (chunkCount * 4);
            reader.BaseStream.Position = strReadPositions;
            var chunkBuffer = reader.ReadBytes(file.compressedChunkSizes.Sum());

            var outBytes = new byte[file.UncompressedSize];

            // Parallelize chunk decompression
            Parallel.For(0, chunkCount, i =>
            {
                if (obfuscationKey != 0)
                    Clarify(chunkBuffer, (int) (readPositions[i] + strReadPositions), readPositions[i], file.compressedChunkSizes[i]);

                var decompressedChunk = Chunk.Decompress(new MemoryStream(chunkBuffer, readPositions[i], file.compressedChunkSizes[i]));

                Buffer.BlockCopy(decompressedChunk, 0, outBytes, i * Chunk.MaxSize, decompressedChunk.Length);
            }); // Parallel.For

            return outBytes;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    archiveStream.Dispose();

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }


}
