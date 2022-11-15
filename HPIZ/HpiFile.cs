using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            foreach (var filePath in archivesFiles.Keys)
            {
                //int progressLimiter = (fileList.Count - 1) / 100 + 1; //Reduce progress calls
                string fullName = destinationPath + "\\" + filePath;
                Directory.CreateDirectory(Path.GetDirectoryName(fullName));


                System.Diagnostics.Debug.WriteLine(filePath);

                var source = archivesFiles[filePath];

                if (cache != null)
                    File.WriteAllBytes(fullName, cache[source].Entries[filePath].Uncompress());
                else
                    using (var archive = new HpiArchive(File.OpenRead(source)))
                        File.WriteAllBytes(fullName, archive.Entries[filePath].Uncompress());

                System.Diagnostics.Debug.WriteLine("DONE: " + filePath);

                //Report progress
                if (progress != null) //&& i % progressLimiter == 0)
                    progress.Report(filePath);
            }
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
                    var file = new FileInfo(fullName);
                    if (file.Length > Int32.MaxValue)
                        throw new Exception("File is too large: " + filePath + ". Maximum allowed size is 2GBytes.");
                    byte[] buffer = File.ReadAllBytes(fullName);
                    files.Add(filePath, new FileEntry(buffer, flavor, fullName, progress));
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
            var tree = new DirectoryNode();
            foreach (var fileName in entries.Keys)
                tree.AddEntry(fileName);
            int chunkStartPosition = tree.CalculateSize() + HpiArchive.HeaderSize;

            using (var fileStream = File.Create(destinationArchiveFileName))
            {
                BinaryWriter chunkWriter = new BinaryWriter(fileStream);
                chunkWriter.BaseStream.Position = chunkStartPosition;

                foreach (var file in entries)
                {
                    if (!duplicates.ContainsKey(file.Key))
                    {
                        if (chunkWriter.BaseStream.Position > uint.MaxValue)
                            throw new Exception("Maximum allowed archive size is 4GB (4 294 967 295 bytes).");

                        file.Value.CompressedDataOffset = (uint)chunkWriter.BaseStream.Position;

                        if (file.Value.FlagCompression != CompressionMethod.StoreUncompressed)
                            foreach (var size in file.Value.CompressedChunkSizes)
                                chunkWriter.Write(size);

                        foreach (var chunk in file.Value.ChunkBytes)
                            chunk.WriteTo(chunkWriter.BaseStream);
                    }
                }

                foreach (var duplicate in duplicates)
                {
                    entries[duplicate.Key].CompressedDataOffset = entries[duplicate.Value].CompressedDataOffset;
                }

                string mandatoryEndString = String.Format("Copyright {0} Cavedog Entertainment", DateTime.Now.Year);
                chunkWriter.Write(System.Text.Encoding.GetEncoding(437).GetBytes(mandatoryEndString));

                BinaryWriter headerWriter = new BinaryWriter(new MemoryStream());

                headerWriter.Write(HpiArchive.HeaderMarker);
                headerWriter.Write(HpiArchive.DefaultVersion);
                headerWriter.Write(chunkStartPosition);
                headerWriter.Write(HpiArchive.NoObfuscationKey);
                headerWriter.Write(HpiArchive.HeaderSize); //Directory Start

                SortedDictionary<string, FileEntry>.Enumerator sequence = entries.GetEnumerator();
                HpiArchive.SetEntries(tree, headerWriter, sequence);

                fileStream.Position = 0;
                headerWriter.BaseStream.Position = 0;
                headerWriter.BaseStream.CopyTo(fileStream);

                if (fileStream.Length > Int32.MaxValue)
                    MessageBox.Show("The HPI file was created, but its size exceeds 2GB (2 147 483 647 bytes). A fatal error may occur when loading the game.", "Oversize Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }
    }
}
