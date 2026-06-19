using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace HPIZ
{
    public class HpiArchive : IDisposable
    {
        internal const int HeaderMarker = 0x49504148; //HAPI Header
        internal const int DefaultVersion = 0x00010000;
        internal const int NoObfuscationKey = 0;
        internal const int HeaderSize = 20;

        internal readonly Stream archiveStream;
        internal readonly int obfuscationKey;
        private readonly SortedDictionary<string, FileEntry> entriesDictionary;
        public ReadOnlyDictionary<string, FileEntry> Entries { get; }

        public HpiArchive(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead || !stream.CanSeek)
                throw new ArgumentException("Archive stream must be readable and seekable.", nameof(stream));
            if (stream.Length < HeaderSize)
                throw new InvalidDataException("Archive is smaller than the HPI header.");

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

            if (directorySize < HeaderSize || directorySize > archiveStream.Length)
                throw new InvalidDataException("Invalid HPI directory size.");
            if (directoryStart < HeaderSize || directoryStart > directorySize - 8)
                throw new InvalidDataException("Invalid HPI directory offset.");

            if (obfuscationKey != 0)
            {
                obfuscationKey = ~(obfuscationKey << 2 | obfuscationKey >> 6);

                archiveReader.BaseStream.Position = 0;
                var buffer = archiveReader.ReadBytes(directorySize);
                if (buffer.Length != directorySize)
                    throw new EndOfStreamException("Archive ended before the complete directory was read.");
                Clarify(buffer, 0);
                archiveReader = new BinaryReader(new MemoryStream(buffer));
            }

            archiveReader.BaseStream.Position = directoryStart;

            int rootNumberOfEntries = archiveReader.ReadInt32();
            int rootOffset = archiveReader.ReadInt32();

            entriesDictionary = new SortedDictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            Entries = new ReadOnlyDictionary<string, FileEntry>(entriesDictionary);

            GetEntries(
                rootNumberOfEntries,
                rootOffset,
                archiveReader,
                string.Empty,
                new HashSet<int>(),
                0);

            archiveReader = new BinaryReader(archiveStream);

            foreach (var entry in entriesDictionary.Keys)
            {
                if (entriesDictionary[entry].FlagCompression != CompressionMethod.StoreUncompressed)
                {
                    int chunkCount = entriesDictionary[entry].CalculateChunkQuantity();
                    if (chunkCount == 0)
                        throw new InvalidDataException("Compressed entries cannot have zero chunks.");

                    archiveStream.Position = entriesDictionary[entry].CompressedDataOffset;
                    var buffer = archiveReader.ReadBytes(chunkCount * 4);
                    if (buffer.Length != chunkCount * 4)
                        throw new EndOfStreamException("Archive ended before the chunk table was read.");

                    if (obfuscationKey != 0)
                        Clarify(buffer, (int)entriesDictionary[entry].CompressedDataOffset);

                    var size = new int[chunkCount];
                    Buffer.BlockCopy(buffer, 0, size, 0, buffer.Length);
                    long compressedDataSize = 0;
                    foreach (int chunkSize in size)
                    {
                        if (chunkSize < Chunk.OverheadSize)
                            throw new InvalidDataException("Invalid compressed chunk size.");
                        compressedDataSize += chunkSize;
                    }

                    long compressedEnd = entriesDictionary[entry].CompressedDataOffset
                        + (chunkCount * 4L)
                        + compressedDataSize;
                    if (compressedEnd > archiveStream.Length)
                        throw new InvalidDataException("Compressed entry points beyond the end of the archive.");

                    entriesDictionary[entry].CompressedChunkSizes = size;
                }
                else
                {
                    long uncompressedEnd = entriesDictionary[entry].CompressedDataOffset
                        + (long)entriesDictionary[entry].UncompressedSize;
                    if (uncompressedEnd > archiveStream.Length)
                        throw new InvalidDataException("Stored entry points beyond the end of the archive.");
                }
            }
        }

        private void GetEntries(
            int numberOfEntries,
            int entryListOffset,
            BinaryReader reader,
            string parentPath,
            HashSet<int> visitedDirectories,
            int depth)
        {
            if (depth > 256)
                throw new InvalidDataException("Archive directory nesting is too deep.");
            if (numberOfEntries < 0)
                throw new InvalidDataException("Archive directory has a negative entry count.");
            if (entryListOffset < 0 || entryListOffset + (numberOfEntries * 9L) > reader.BaseStream.Length)
                throw new InvalidDataException("Archive directory entry list is outside the directory data.");
            if (!visitedDirectories.Add(entryListOffset))
                throw new InvalidDataException("Archive directory contains a cycle.");

            for (int i = 0; i < numberOfEntries; ++i)
            {
                reader.BaseStream.Position = entryListOffset + (i * 9L);

                int nameOffset = reader.ReadInt32();
                int dataOffset = reader.ReadInt32();
                bool IsDirectory = reader.ReadBoolean();
                if (nameOffset < 0 || nameOffset >= reader.BaseStream.Length)
                    throw new InvalidDataException("Archive entry name is outside the directory data.");
                if (dataOffset < 0 || dataOffset >= reader.BaseStream.Length)
                    throw new InvalidDataException("Archive entry data is outside the directory data.");

                reader.BaseStream.Position = nameOffset;
                var fullPath = Path.Combine(parentPath, ReadStringCP437NullTerminated(reader));
                reader.BaseStream.Position = dataOffset;

                if (IsDirectory)
                    GetEntries(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader,
                        fullPath,
                        visitedDirectories,
                        depth + 1);
                else
                    entriesDictionary.Add(fullPath, new FileEntry(reader, this));
            }

            visitedDirectories.Remove(entryListOffset);
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

            char[] reserved = { '<', '>', '\"', ':', '/', '\\', '|', '?', '*', };

            for (int i = 0; i < characters.Length; i++)
                if (char.IsControl(characters[i]) || reserved.Contains(characters[i]))
                    characters[i] = '_'; //Replace control or reserved char with underscore

            return new string(characters);
        }



        internal void Clarify(byte[] obfuscatedBytes, int position, int start = 0, int lenght = 0)
        {
            if (lenght == 0) lenght = obfuscatedBytes.Length;
            else lenght = lenght + start;
            for (int i = start; i < lenght; ++i)
            {
                unchecked { obfuscatedBytes[i] = (byte)~(position ^ obfuscationKey ^ obfuscatedBytes[i]); }
                position++;
            }
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
