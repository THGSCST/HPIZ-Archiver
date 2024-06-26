﻿using System;
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

            entriesDictionary = new SortedDictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            Entries = new ReadOnlyDictionary<string, FileEntry>(entriesDictionary);

            GetEntries(rootNumberOfEntries, rootOffset, archiveReader, string.Empty);

            archiveReader = new BinaryReader(archiveStream);

            foreach (var entry in entriesDictionary.Keys)
            {
                if (entriesDictionary[entry].FlagCompression != CompressionMethod.StoreUncompressed)
                {
                    int chunkCount = entriesDictionary[entry].CalculateChunkQuantity();
                    archiveStream.Position = entriesDictionary[entry].CompressedDataOffset;
                    var buffer = archiveReader.ReadBytes(chunkCount * 4);

                    if (obfuscationKey != 0)
                        Clarify(buffer, (int)entriesDictionary[entry].CompressedDataOffset);

                    var size = new int[chunkCount];
                    Buffer.BlockCopy(buffer, 0, size, 0, buffer.Length);

                    entriesDictionary[entry].CompressedChunkSizes = size;
                }
            }
        }

        private void GetEntries(int NumberOfEntries, int EntryListOffset, BinaryReader reader, string parentPath)
        {
            for (int i = 0; i < NumberOfEntries; ++i)
            {
                reader.BaseStream.Position = EntryListOffset + (i * 9);

                int nameOffset = reader.ReadInt32();
                int dataOffset = reader.ReadInt32();
                bool IsDirectory = reader.ReadBoolean();
                reader.BaseStream.Position = nameOffset;
                var fullPath = Path.Combine(parentPath, ReadStringCP437NullTerminated(reader));
                reader.BaseStream.Position = dataOffset;

                if (IsDirectory)
                    GetEntries(reader.ReadInt32(), reader.ReadInt32(), reader, fullPath);
                else
                    entriesDictionary.Add(fullPath, new FileEntry(reader, this));
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
