using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPIZ
{
    public static class HpiFile
    {
        private static readonly Encoding CodePage437 = Encoding.GetEncoding(437);
        private static readonly ParallelOptions FileCompressionParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 16))
        };

        public static HpiArchive Open(string archiveFileName)
        {
            var stream = new FileStream(
                archiveFileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            try
            {
                return new HpiArchive(stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public static HpiArchive Create(string archiveFileName)
        {
            WriteToFile(
                archiveFileName,
                new SortedDictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase),
                new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            return Open(archiveFileName);
        }

        public static ExtractionResult DoExtraction(FilePathCollection archivesFiles, string destinationPath, IProgress<string> progress, Dictionary<string, HpiArchive> cache = null)
        {
            bool disposeCacheEntries = false;
            var errors = new List<FileOperationError>();
            int extractedFileCount = 0;

            if (cache == null)
            {
                cache = new Dictionary<string, HpiArchive>(StringComparer.OrdinalIgnoreCase);
                disposeCacheEntries = true;
            }

            try
            {
                string destinationRoot = Path.GetFullPath(destinationPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                foreach (var filePath in archivesFiles.Keys)
                {
                    try
                    {
                        string fullName = GetSafeExtractionPath(destinationRoot, filePath);
                        string parentDirectory = Path.GetDirectoryName(fullName);
                        if (!string.IsNullOrEmpty(parentDirectory))
                            Directory.CreateDirectory(parentDirectory);

                        var source = archivesFiles[filePath];
                        HpiArchive archive;
                        if (!cache.TryGetValue(source, out archive))
                        {
                            archive = Open(source);
                            cache.Add(source, archive);
                        }

                        WriteExtractedFile(fullName, archive.Entries[filePath].Uncompress());
                        extractedFileCount++;
                        progress?.Report(filePath);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new FileOperationError(filePath, ex));
                    }
                }
            }
            finally
            {
                if (disposeCacheEntries)
                    DisposeArchives(cache.Values);
            }

            return new ExtractionResult(extractedFileCount, errors);
        }

        public static ArchiveCreationResult CreateFromDirectory(string sourceDirectoryFullName, string destinationArchiveFileName, CompressionMethod flavor, IProgress<string> progress)
        {
            destinationArchiveFileName = Path.GetFullPath(destinationArchiveFileName);
            var fileList = new FilePathCollection(sourceDirectoryFullName);
            return CreateFromManySources(fileList, destinationArchiveFileName, flavor, progress);
        }

        public static ArchiveCreationResult CreateFromManySources(FilePathCollection sources, string destinationArchiveFileName, CompressionMethod flavor, IProgress<string> progress, Dictionary<string, HpiArchive> cache = null, SortedDictionary<string, string> duplicates = null)
        {
            if (duplicates == null)
                duplicates = FindDuplicateContent(sources, cache);

            var files = new SortedDictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<FileOperationError>();
            var sourceItems = sources
                .Where(source => !duplicates.ContainsKey(source.Key))
                .ToArray();
            var compressedEntries = new FileEntry[sourceItems.Length];
            var parallelErrors = new ConcurrentBag<FileOperationError>();
            bool parallelizeFiles = flavor != CompressionMethod.StoreUncompressed
                && sourceItems.Length >= FileCompressionParallelOptions.MaxDegreeOfParallelism;

            Action<int> compressSource = index =>
            {
                string filePath = sourceItems[index].Key;
                string sourcePath = sourceItems[index].Value;
                try
                {
                    //Check if source path is a directory or HPI archive
                    if (Directory.Exists(sourcePath))
                    {
                        string fullName = Path.Combine(sourcePath, filePath);
                        var file = new FileInfo(fullName);
                        if (file.Length > Int32.MaxValue)
                            throw new Exception("File is too large: " + filePath + ". Maximum allowed size is 2GBytes.");
                        byte[] buffer = File.ReadAllBytes(fullName);
                        compressedEntries[index] = new FileEntry(
                            buffer,
                            flavor,
                            fullName,
                            progress,
                            !parallelizeFiles);
                    }
                    else //Is HPI archive
                    {
                        byte[] buffer;

                        if (cache != null)
                            buffer = cache[sourcePath].Entries[filePath].Uncompress();
                        else
                            using (var archive = Open(sourcePath))
                                buffer = archive.Entries[filePath].Uncompress();

                        compressedEntries[index] = new FileEntry(
                            buffer,
                            flavor,
                            filePath,
                            progress,
                            !parallelizeFiles);
                    }
                }
                catch (Exception ex)
                {
                    parallelErrors.Add(new FileOperationError(filePath, ex));
                }
            };

            if (parallelizeFiles)
                Parallel.For(0, sourceItems.Length, FileCompressionParallelOptions, compressSource);
            else
                for (int i = 0; i < sourceItems.Length; i++)
                    compressSource(i);

            for (int i = 0; i < sourceItems.Length; i++)
            {
                if (compressedEntries[i] != null)
                    files.Add(sourceItems[i].Key, compressedEntries[i]);
            }

            errors.AddRange(parallelErrors.OrderBy(error => error.FilePath, StringComparer.OrdinalIgnoreCase));

            foreach (var duplicate in duplicates)
            {
                FileEntry duplicateSourceEntry;
                if (files.TryGetValue(duplicate.Value, out duplicateSourceEntry))
                    files.Add(duplicate.Key, duplicateSourceEntry);
                else
                    errors.Add(new FileOperationError(
                        duplicate.Key,
                        new InvalidDataException(
                            $"Duplicate source '{duplicate.Value}' was not available.")));
            }

            var validDuplicates = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var duplicate in duplicates)
                if (files.ContainsKey(duplicate.Key) && files.ContainsKey(duplicate.Value))
                    validDuplicates.Add(duplicate.Key, duplicate.Value);

            try
            {
                bool exceedsRecommendedSize = WriteToFile(destinationArchiveFileName, files, validDuplicates);
                return new ArchiveCreationResult(files.Count, exceedsRecommendedSize, errors);
            }
            finally
            {
                DisposeEntryBuffers(files.Values);
            }
        }

        public static SortedDictionary<string, string> FindDuplicateContent(FilePathCollection sources, Dictionary<string, HpiArchive> cache = null)
        {
            bool disposeCacheEntries = cache == null;
            if (cache == null)
                cache = new Dictionary<string, HpiArchive>(StringComparer.OrdinalIgnoreCase);

            try
            {
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
                            cache.Add(sourcePath, Open(sourcePath));

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
                                hash = HashUtility.CalculateSha256(fullName);
                            }
                            else //Is HPI archive
                            {
                                var entry = cache[sourcePath].Entries[candidate];
                                hash = HashUtility.CalculateSha256(entry.Uncompress());
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
                            else if (!duplicateResults.ContainsKey(file))
                                duplicateResults.Add(file, first);
                    }

                return duplicateResults;
            }
            finally
            {
                if (disposeCacheEntries)
                    DisposeArchives(cache.Values);
            }
        }


        private static bool WriteToFile(string destinationArchiveFileName, SortedDictionary<string, FileEntry> entries, SortedDictionary<string, string> duplicates)
        {
            // Create directory tree and calculate chunk start position
            DirectoryNode directoryTree = new DirectoryNode(entries.Keys.ToList());
            int chunkStartPosition = directoryTree.CalculateTreeSize() + HpiArchive.HeaderSize;
            string destinationFullPath = Path.GetFullPath(destinationArchiveFileName);
            string destinationDirectory = Path.GetDirectoryName(destinationFullPath);
            if (string.IsNullOrEmpty(destinationDirectory))
                throw new InvalidOperationException("The destination archive must have a parent directory.");

            Directory.CreateDirectory(destinationDirectory);
            string temporaryFileName = Path.Combine(
                destinationDirectory,
                Path.GetFileName(destinationFullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            bool exceedsRecommendedSize = false;
            try
            {
                using (FileStream fileStream = new FileStream(temporaryFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
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
                                WriteInt32Array(chunkWriter.BaseStream, file.Value.CompressedChunkSizes);

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
                    chunkWriter.Write(CodePage437.GetBytes(mandatoryEndString));

                    // Write header to a memory stream
                    byte[] headerBytes = new byte[chunkStartPosition];
                    using (var headerStream = new MemoryStream(headerBytes, true))
                    using (var headerWriter = new BinaryWriter(headerStream))
                    {
                        headerStream.SetLength(0);
                        headerWriter.Write(HpiArchive.HeaderMarker);
                        headerWriter.Write(HpiArchive.DefaultVersion);
                        headerWriter.Write(chunkStartPosition);
                        headerWriter.Write(HpiArchive.NoObfuscationKey);
                        headerWriter.Write(HpiArchive.HeaderSize); // Directory Start

                        var sequence = entries.GetEnumerator();
                        directoryTree.WriteTree(headerWriter, sequence);

                        // Write the completed header to the reserved beginning of the archive.
                        fileStream.Position = 0;
                        fileStream.Write(headerBytes, 0, headerBytes.Length);
                    }

                    exceedsRecommendedSize = fileStream.Length > int.MaxValue;
                }

                using (var validationArchive = Open(temporaryFileName))
                {
                    if (validationArchive.Entries.Count != entries.Count)
                        throw new InvalidDataException("The generated archive failed validation.");
                }

                ReplaceDestinationFile(temporaryFileName, destinationFullPath);
                return exceedsRecommendedSize;
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"An error occurred while creating or overwriting the file '{destinationFullPath}'.",
                    ex);
            }
            finally
            {
                if (File.Exists(temporaryFileName))
                {
                    try
                    {
                        File.Delete(temporaryFileName);
                    }
                    catch
                    {
                        // Preserve the original operation result; a stale temp file is recoverable.
                    }
                }
            }
        }

        private static string GetSafeExtractionPath(string destinationRoot, string filePath)
        {
            string fullName = Path.GetFullPath(Path.Combine(destinationRoot, filePath));
            if (!fullName.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The archive entry '{filePath}' points outside the destination folder.");

            return fullName;
        }

        private static void ReplaceDestinationFile(string temporaryFileName, string destinationFileName)
        {
            if (File.Exists(destinationFileName))
                File.Replace(temporaryFileName, destinationFileName, null);
            else
                File.Move(temporaryFileName, destinationFileName);
        }

        private static void WriteExtractedFile(string destinationFileName, byte[] contents)
        {
            string destinationDirectory = Path.GetDirectoryName(destinationFileName);
            string temporaryFileName = Path.Combine(
                destinationDirectory,
                Path.GetFileName(destinationFileName) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var fileStream = new FileStream(temporaryFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fileStream.Write(contents, 0, contents.Length);
                }

                ReplaceDestinationFile(temporaryFileName, destinationFileName);
            }
            finally
            {
                if (File.Exists(temporaryFileName))
                {
                    try
                    {
                        File.Delete(temporaryFileName);
                    }
                    catch
                    {
                        // Preserve the original extraction result; a stale temp file is recoverable.
                    }
                }
            }
        }

        private static void DisposeArchives(IEnumerable<HpiArchive> archives)
        {
            foreach (var archive in archives)
                archive.Dispose();
        }

        private static void DisposeEntryBuffers(IEnumerable<FileEntry> entries)
        {
            var disposedEntries = new HashSet<FileEntry>();
            foreach (var entry in entries)
            {
                if (!disposedEntries.Add(entry) || entry.ChunkBytes == null)
                    continue;

                Array.Clear(entry.ChunkBytes, 0, entry.ChunkBytes.Length);
            }
        }

        private static void WriteInt32Array(Stream destination, int[] values)
        {
            byte[] bytes = new byte[values.Length * sizeof(int)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
                for (int offset = 0; offset < bytes.Length; offset += sizeof(int))
                    Array.Reverse(bytes, offset, sizeof(int));

            destination.Write(bytes, 0, bytes.Length);
        }

    }
}
