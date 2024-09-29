using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HPIZArchiver;

namespace HPIZ
{
    public static class HpiFile
    {

        public static HpiArchive Open(string archiveFileName)
        {
            return new HpiArchive(new FileStream(archiveFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
        }

        public static HpiArchive Create(string archiveFileName)
        {
            throw new System.NotImplementedException();
        }

        public static void DoExtraction(FilePathCollection archivesFiles, string destinationPath, IProgress<string> progress, Dictionary<string, HpiArchive> cache = null)
        {
            bool disposeCacheEntries = false;
            if (cache == null)
            {
                cache = new Dictionary<string, HpiArchive>();
                disposeCacheEntries = true;
            }

            foreach (var filePath in archivesFiles.Keys)
            {
                string fullName = Path.Combine(destinationPath, filePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullName));

                var source = archivesFiles[filePath];

                HpiArchive archive;
                if (!cache.TryGetValue(source, out archive))
                {
                    archive = new HpiArchive(File.OpenRead(source));
                    cache.Add(source, archive);
                }

                try
                {
                    File.WriteAllBytes(fullName, archive.Entries[filePath].Uncompress());
                    progress?.Report(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while writing the file '{filePath}'.\n\nDetails: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // Dispose the cache entries if we created the cache
            if (disposeCacheEntries)
                foreach (var cachedArchive in cache.Values)
                    cachedArchive.Dispose();
        }

        public static void CreateFromDirectory(string sourceDirectoryFullName, string destinationArchiveFileName, CompressionMethod flavor, IProgress<string> progress)
        {
            destinationArchiveFileName = Path.GetFullPath(destinationArchiveFileName);
            var fileList = new FilePathCollection(sourceDirectoryFullName);
            CreateFromManySources(fileList, destinationArchiveFileName, flavor, progress);
        }

        public static void CreateFromManySources(FilePathCollection sources, string destinationArchiveFileName, CompressionMethod flavor, IProgress<string> progress, Dictionary<string, HpiArchive> cache = null, SortedDictionary<string, string> duplicates = null)
        {
            if (duplicates == null)
                duplicates = FindDuplicateContent(sources, cache);

            var files = new SortedDictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in sources.Keys)
            {
                var sourcePath = sources[filePath];
                //Check if source path is a directory or HPI archive
                if (Directory.Exists(sourcePath))
                {
                    string fullName = Path.Combine(sourcePath, filePath);
                    try
                    {
                        var file = new FileInfo(fullName);
                        if (file.Length > Int32.MaxValue)
                            throw new Exception("File is too large: " + filePath + ". Maximum allowed size is 2GBytes.");
                        byte[] buffer = File.ReadAllBytes(fullName);
                        files.Add(filePath, new FileEntry(buffer, flavor, fullName, progress));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while reading the file '{fullName}'.\n\nDetails: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else //Is HPI archive
                {
                    byte[] buffer;

                    if (cache != null)
                        buffer = cache[sourcePath].Entries[filePath].Uncompress();
                    else
                        using (var archive = new HpiArchive(File.OpenRead(sourcePath)))
                            buffer = archive.Entries[filePath].Uncompress();

                    files.Add(filePath, new FileEntry(buffer, flavor, filePath, progress));
                }
            }

            if (cache != null)
                foreach (var archive in cache)
                    archive.Value.Dispose();

            WriteToFile(destinationArchiveFileName, files, duplicates);
        }

        public static SortedDictionary<string, string> FindDuplicateContent(FilePathCollection sources, Dictionary<string, HpiArchive> cache = null)
        {
            if (cache == null)
                cache = new Dictionary<string, HpiArchive>();

            var toHashCandidates = new Dictionary<long, FilePathCollection>();

            foreach (var filePath in sources.Keys)
            {
                var sourcePath = sources[filePath];
                long fileSize;
                //Check if source is a directory or HPI archive
                if (Directory.Exists(sourcePath))
                {
                    string fullName = Path.Combine(sourcePath, filePath);
                    fileSize = new FileInfo(fullName).Length;
                }
                else //Is HPI archive
                {
                    if (!cache.ContainsKey(sourcePath))
                        cache.Add(sourcePath, HpiFile.Open(sourcePath));

                    Debug.WriteLine(sourcePath + filePath);

                    fileSize = cache[sourcePath].Entries[filePath].UncompressedSize;
                }
                if (toHashCandidates.ContainsKey(fileSize))
                    toHashCandidates[fileSize].Add(filePath, sourcePath);
                else
                    toHashCandidates.Add(fileSize, new FilePathCollection(filePath, sourcePath));
            }


            var hashedFiles = new Dictionary<string, FilePathCollection>(StringComparer.OrdinalIgnoreCase);

            foreach (var fileList in toHashCandidates.Values)
                if (fileList.Count > 1)
                    foreach (var candidate in fileList.Keys)
                    {
                        var sourcePath = fileList[candidate];
                        string hash;

                        //Check if source is a directory or HPI archive
                        if (Directory.Exists(sourcePath))
                        {
                            string fullName = Path.Combine(sourcePath, candidate);
                            hash = Utils.CalculateSha256(fullName);
                        }
                        else //Is HPI archive
                        {
                            var entry = cache[sourcePath].Entries[candidate];
                            hash = Utils.CalculateSha256(entry.Uncompress());
                        }

                        if (hashedFiles.ContainsKey(hash))
                            hashedFiles[hash].Add(candidate, sourcePath);
                        else
                            hashedFiles.Add(hash, new FilePathCollection(candidate, sourcePath));
                    }


            var duplicateResults = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hash in hashedFiles.Keys)
                if (hashedFiles[hash].Count > 1)
                {
                    string first = string.Empty;
                    foreach (var file in hashedFiles[hash].Keys)
                        if (first == string.Empty)
                            first = file;
                        else
                            if (!duplicateResults.ContainsKey(file))
                            duplicateResults.Add(file, first);
                }

            return duplicateResults;
        }


        private static void WriteToFile(string destinationArchiveFileName, SortedDictionary<string, FileEntry> entries, SortedDictionary<string, string> duplicates)
        {
            // Create directory tree and calculate chunk start position
            DirectoryNode directoryTree = new DirectoryNode(entries.Keys.ToList());
            int chunkStartPosition = directoryTree.CalculateTreeSize() + HpiArchive.HeaderSize;

            try
            {
                using (FileStream fileStream = File.Create(destinationArchiveFileName))
                using (BinaryWriter chunkWriter = new BinaryWriter(fileStream))
                {
                    // Set the position for writing chunks
                    chunkWriter.BaseStream.Position = chunkStartPosition;

                    // Write file entries
                    foreach (var file in entries)
                    {
                        if (!duplicates.ContainsKey(file.Key))
                        {
                            if (chunkWriter.BaseStream.Position > uint.MaxValue)
                                throw new Exception("Maximum allowed archive size is 4GB (4,294,967,295 bytes).");

                            file.Value.CompressedDataOffset = (uint)chunkWriter.BaseStream.Position;

                            if (file.Value.FlagCompression != CompressionMethod.StoreUncompressed)
                            {
                                foreach (var size in file.Value.CompressedChunkSizes)
                                    chunkWriter.Write(size);
                            }

                            foreach (var chunk in file.Value.ChunkBytes)
                                chunk.WriteTo(chunkWriter.BaseStream);
                        }
                    }

                    // Update duplicate file offsets
                    foreach (var duplicate in duplicates)
                    {
                        entries[duplicate.Key].CompressedDataOffset = entries[duplicate.Value].CompressedDataOffset;
                    }

                    // Write mandatory end string
                    string mandatoryEndString = $"Copyright {DateTime.Now.Year} Cavedog Entertainment";
                    chunkWriter.Write(Encoding.GetEncoding(437).GetBytes(mandatoryEndString));

                    // Write header to a memory stream
                    using (var headerStream = new MemoryStream())
                    using (var headerWriter = new BinaryWriter(headerStream))
                    {
                        headerWriter.Write(HpiArchive.HeaderMarker);
                        headerWriter.Write(HpiArchive.DefaultVersion);
                        headerWriter.Write(chunkStartPosition);
                        headerWriter.Write(HpiArchive.NoObfuscationKey);
                        headerWriter.Write(HpiArchive.HeaderSize); // Directory Start

                        var sequence = entries.GetEnumerator();
                        directoryTree.WriteTree(headerWriter, sequence);

                        // Copy header to the beginning of the file
                        fileStream.Position = 0;
                        headerStream.Position = 0;
                        headerStream.CopyTo(fileStream);
                    }

                    // Check if file size exceeds 2GB and display a warning
                    if (fileStream.Length > int.MaxValue)
                    {
                        MessageBox.Show("The HPI file was created, but its size exceeds 2GB (2,147,483,647 bytes). A fatal error may occur when loading the game.",
                                        "Oversize Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while creating or writing to the file '{destinationArchiveFileName}'.\n\nDetails: {ex.Message}",
                                "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

    }
}
