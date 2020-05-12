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
        private const int HeaderMarker = 0x49504148; //HAPI Header
        private const int DefaultVersion = 0x00010000;
        private const int NoObfuscationKey = 0;

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
                obfuscationKey = ~((obfuscationKey * 4) | (obfuscationKey >> 6));

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
                int chunkCount = entriesDictionary[entry].CalculateChunkQuantity();
                archiveStream.Position = entriesDictionary[entry].OffsetOfCompressedData;
                var buffer = archiveReader.ReadBytes(chunkCount * 4);
                
                if (obfuscationKey != 0)
                    Clarify(buffer, entriesDictionary[entry].OffsetOfCompressedData);
                
                var size = new int[chunkCount];
                Buffer.BlockCopy(buffer, 0, size, 0, buffer.Length);

                entriesDictionary[entry].ChunkSizes = size;
            }
        }

        private void GetEntries(int NumberOfEntries, int EntryListOffset, BinaryReader reader, string parentPath)
        {
            for (int i = 0; i < NumberOfEntries; ++i)
            {
                reader.BaseStream.Position = EntryListOffset + (i * 9);

                int nameOffset = reader.ReadInt32();
                int dataOffset = reader.ReadInt32(); //Unused
                bool IsDirectory = reader.ReadBoolean();
                reader.BaseStream.Position = nameOffset;
                var fullPath = Path.Combine(parentPath, ReadStringCP437NullTerminated(reader));

                if (IsDirectory)
                    GetEntries(reader.ReadInt32(), reader.ReadInt32(), reader, fullPath);
                else
                {
                    FileEntry fd = new FileEntry(reader);
                    entriesDictionary.Add(fullPath, fd);
                }
            }
        }

        private static int GetDirectorySize(DirectoryTree tree)
        {
            int totalSize = 8;

            foreach (var node in tree)
            {
                totalSize += 9;
                totalSize += node.Key.Length + 1;
                if (node.Children.Count != 0)
                    totalSize += GetDirectorySize(node.Children);
                else
                    totalSize += 9;
            }
            return totalSize;
        }

        private static void SetEntries(DirectoryTree tree, BinaryWriter bw, Queue<FileEntry> sequence, int directorySize)
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
                    SetEntries(tree[i].Children, bw, sequence, directorySize);
                else
                {
                    FileEntry fd = sequence.Dequeue();
                    bw.Write(fd.OffsetOfCompressedData + directorySize); //OffsetOfData
                    bw.Write(fd.UncompressedSize); //UncompressedSize 
                    bw.Write((byte)fd.FlagCompression); //FlagCompression 
                }
                bw.BaseStream.Position = previousPos;
            }
        }

        public static Stream Encode(SortedDictionary<string, List<Chunk>> allFiles)
        {
            var obsCollection = new DirectoryTree();
            foreach (var item in allFiles.Keys)
                obsCollection.AddEntry(item);

            Queue<FileEntry> sequence;

            Stream serial = HpiFile.SerializeChunks(allFiles, out sequence);

            BinaryWriter bw = new BinaryWriter(new MemoryStream());

            bw.Write(HeaderMarker);
            bw.Write(DefaultVersion);

            int directorySize = GetDirectorySize(obsCollection) + 20;
            bw.Write(directorySize);

            bw.Write(NoObfuscationKey);

            int directoryStart = 20;
            bw.Write(directoryStart); //Directory Start Pos20 point to next

            SetEntries(obsCollection, bw, sequence, directorySize);

            serial.Position = 0;
            bw.BaseStream.Position = bw.BaseStream.Length;
            serial.CopyTo(bw.BaseStream);

            bw.Write("Copyright " + DateTime.Now.Year.ToString() + " Cavedog Entertainment"); //Endfile mandatory string

            return bw.BaseStream;
        }


        private static string ReadStringCP437NullTerminated(BinaryReader reader)
        {
            Encoding codePage437 = Encoding.GetEncoding(437);
            var bytes = new Queue<byte>();
            byte b = reader.ReadByte();
            while (b != 0)
            {
                bytes.Enqueue(b);
                b = reader.ReadByte();
            }
            var asciiCharList = codePage437.GetChars(bytes.ToArray());

            char[] invalids = { '\"', '<', '>', '|', '\0', ':', '*', '?', '\\', '/' ,
                (Char)1, (Char)2, (Char)3, (Char)4, (Char)5, (Char)6, (Char)7, (Char)8,
                (Char)9, (Char)10, (Char)11, (Char)12, (Char)13,(Char)14,(Char)15, (Char)16,
                (Char)17, (Char)18, (Char)19, (Char)20, (Char)21,(Char)22, (Char)23, (Char)24,
                (Char)25, (Char)26, (Char)27, (Char)28, (Char)29, (Char)30, (Char)31 };
 
            for (int i = 0; i < asciiCharList.Length; i++)
            {
                if (invalids.Contains((asciiCharList[i])))
                        asciiCharList[i] = '_'; //Replace invalid char with underscore
            }

            return new string(asciiCharList);
        }


        private static void WriteStringCP437NullTerminated(BinaryWriter reader, string text)
        {
            Encoding codePage437 = Encoding.GetEncoding(437);
            reader.Write(codePage437.GetBytes(text));
            reader.Write(byte.MinValue); //Zero byte to end string
        }

        void Clarify(byte[] obfuscatedBytes, int position)
        {
            for (int i = 0; i < obfuscatedBytes.Length; ++i)
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
                var buffer = reader.ReadBytes(file.UncompressedSize);

                if (obfuscationKey != 0)
                    Clarify(buffer, file.OffsetOfCompressedData);

                return buffer;
            }
            
            if(file.FlagCompression != CompressionMethod.LZ77 && file.FlagCompression != CompressionMethod.ZLib)
                throw new Exception("Unknown compression method in file entry");

            var chunkCount = file.ChunkSizes.Length;
            reader.BaseStream.Position = file.OffsetOfCompressedData + (chunkCount * 4);

            //Set chunks array sizes and split stream to chunks and save stream positions
            var chunkBuffer = new List<byte[]>(chunkCount);
            var positions = new Queue<int>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                positions.Enqueue( (int) reader.BaseStream.Position);
                chunkBuffer.Add(new byte[file.ChunkSizes[i]]);
                reader.Read(chunkBuffer[i], 0, chunkBuffer[i].Length);
            }

            var outputChunks = new Chunk[chunkCount];

            Parallel.For(0, chunkCount, i =>
            {
                if (obfuscationKey != 0)
                    Clarify(chunkBuffer[i], positions.Dequeue());

                outputChunks[i] = new Chunk(chunkBuffer[i]);
                outputChunks[i].Decompress();
            });

            var outBytes = new byte[file.UncompressedSize];
            int copyPosition = 0;
            for (int i = 0; i < chunkCount; i++)
            {
                Array.Copy(outputChunks[i].Data, 0, outBytes, copyPosition, outputChunks[i].Data.Length);
                copyPosition += outputChunks[i].Data.Length;
            }

            if(file.UncompressedSize != copyPosition)
                throw new Exception("Bad output size");

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
