using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace HPIZ
{
    public static class HpiFile
    {

        public static HpiArchive Open(string archiveFileName)
        {
            return new HpiArchive(new FileStream(archiveFileName, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public static HpiArchive Create(string archiveFileName)
        {
            throw new System.NotImplementedException();
        }

        public static void DoExtraction(PathCollection archivesFiles, string destinationPath, IProgress<string> progress)
        {
            foreach (var archiveFullPath in archivesFiles.Keys)
            {
                using (var archive = new HpiArchive(File.OpenRead(archiveFullPath)))
                {
                    //int progressLimiter = (fileList.Count - 1) / 100 + 1; //Reduce progress calls

                    foreach (var shortFileName in archivesFiles[archiveFullPath])
                    {
                        string fullName = destinationPath + "\\" + shortFileName;
                        Directory.CreateDirectory(Path.GetDirectoryName(fullName));
                        var entry = archive.Entries[shortFileName];
                        File.WriteAllBytes(fullName, entry.Uncompress());

                        //Report progress
                        if (progress != null) //&& i % progressLimiter == 0)
                            progress.Report(shortFileName);
                    }
                }
            }
        }

        public static void CreateFromDirectory(string sourceDirectoryFullName, string destinationArchiveFileName, CompressionMethod flavor, IProgress<string> progress)
        {
            destinationArchiveFileName = Path.GetFullPath(destinationArchiveFileName);
            var fileList = GetDirectoryFileList(sourceDirectoryFullName);
            CreateFromManySources(fileList, destinationArchiveFileName, flavor, progress);
        }

        public static PathCollection GetDirectoryFileList(string sourceDirectoryFullName)
        {
            var fileList = new PathCollection();
            sourceDirectoryFullName = Path.GetFullPath(sourceDirectoryFullName);
            fileList.Add(sourceDirectoryFullName, new SortedSet<string>());
            var files = Directory.EnumerateFiles(sourceDirectoryFullName, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                fileList[sourceDirectoryFullName].Add(file.Substring(sourceDirectoryFullName.Length + 1));
            }
            return fileList;
        }

        public static void CreateFromManySources(PathCollection sources, string destinationArchiveFileName, CompressionMethod flavor, IProgress<string> progress)
        {
            var duplicates = FindDuplicateContent(sources);

            var files = new SortedDictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var dirOrArchivePath in sources.Keys)
            {
                //Check if source is a directory or HPI archive
                if(Directory.Exists(dirOrArchivePath))
                    foreach (var shortFileName in sources[dirOrArchivePath])
                    {
                        string fullName = Path.Combine(dirOrArchivePath, shortFileName);
                        var file = new FileInfo(fullName);
                        if (file.Length > Int32.MaxValue)
                            throw new Exception("File is too large: " + shortFileName + ". Maximum allowed size is 2GBybtes.");
                        byte[] buffer = File.ReadAllBytes(fullName);
                        if(!files.ContainsKey(shortFileName))
                            files.Add(shortFileName, new FileEntry(buffer, flavor, fullName, progress));
                    }
                else //Is HPI archive
                    using (var archive = new HpiArchive(File.OpenRead(dirOrArchivePath)))
                        foreach (var shortFileName in sources[dirOrArchivePath])
                        {
                            var buffer = archive.Entries[shortFileName].Uncompress();

                            if (!files.ContainsKey(shortFileName))
                                files.Add(shortFileName, new FileEntry(buffer, flavor, shortFileName, progress));
                        }
            }

            WriteToFile(destinationArchiveFileName, files, duplicates);
        }

        public static Dictionary<string, string> FindDuplicateContent(PathCollection sources)
        {
            var toHashCandidates = new Dictionary<long, List<(string, string)>>();

            foreach (var dirOrArchivePath in sources.Keys)
                //Check if source is a directory or HPI archive
                if (Directory.Exists(dirOrArchivePath))
                    foreach (var shortFileName in sources[dirOrArchivePath])
                    {
                        string fullName = Path.Combine(dirOrArchivePath, shortFileName);
                        var fileSize = new FileInfo(fullName).Length;

                        if (toHashCandidates.ContainsKey(fileSize))
                            toHashCandidates[fileSize].Add((dirOrArchivePath, shortFileName));
                        else
                            toHashCandidates.Add(fileSize, new List<(string, string)>() { (dirOrArchivePath, shortFileName) });
                    }
                else //Is HPI archive
                    using (var archive = new HpiArchive(System.IO.File.OpenRead(dirOrArchivePath)))
                        foreach (var shortFileName in sources[dirOrArchivePath])
                        {
                            var entry = archive.Entries[shortFileName];
                            var fileSize = entry.UncompressedSize;

                            if (toHashCandidates.ContainsKey(fileSize))
                                toHashCandidates[fileSize].Add((dirOrArchivePath, shortFileName));
                            else
                                toHashCandidates.Add(fileSize, new List<(string, string)>() { (dirOrArchivePath, shortFileName) });
                        }


            var hashedFiles = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var pathPair in toHashCandidates.Values)
                if(pathPair.Count > 1)
                foreach (var candidate in pathPair)
                    //Check if source is a directory or HPI archive
                    if (Directory.Exists(candidate.Item1))
                    {
                        string fullName = Path.Combine(candidate.Item1, candidate.Item2);
                        string hash;

                        using (SHA256 sha256Hash = SHA256.Create())
                            hash = BitConverter.ToString(sha256Hash.ComputeHash(System.IO.File.ReadAllBytes(fullName)));

                        if (hashedFiles.ContainsKey(hash))
                            hashedFiles[hash].Add(candidate);
                        else
                            hashedFiles.Add(hash, new List<(string, string)>() { candidate });
                        }
                    else //Is HPI archive
                        using (var archive = new HpiArchive(System.IO.File.OpenRead(candidate.Item1)))
                        {
                            var entry = archive.Entries[candidate.Item2];
                            string hash;

                            using (SHA256 sha256Hash = SHA256.Create())
                                hash = BitConverter.ToString(sha256Hash.ComputeHash(entry.Uncompress()));

                            if (hashedFiles.ContainsKey(hash))
                                hashedFiles[hash].Add(candidate);
                            else
                                hashedFiles.Add(hash, new List<(string, string)>() { candidate });
                        }

            var duplicateResults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hash in hashedFiles.Keys)
                if (hashedFiles[hash].Count > 1)
                {
                    string first = string.Empty;
                    foreach (var file in hashedFiles[hash])
                        if (first == string.Empty)
                            first = file.Item2;
                        else
                            if(!duplicateResults.ContainsKey(file.Item2))
                            duplicateResults.Add(file.Item2, first);
                }

            return duplicateResults;
        }


        
        private static void WriteToFile(string destinationArchiveFileName, SortedDictionary<string, FileEntry> entries, Dictionary<string, string> duplicates)
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

                        file.Value.OffsetOfCompressedData = (uint)chunkWriter.BaseStream.Position;

                        if (file.Value.FlagCompression != CompressionMethod.StoreUncompressed)
                            foreach (var size in file.Value.compressedChunkSizes)
                                chunkWriter.Write(size);

                        foreach (var chunk in file.Value.ChunkBytes)
                            chunk.WriteTo(chunkWriter.BaseStream);
                    }
                    else
                        file.Value.OffsetOfCompressedData = entries[duplicates[file.Key]].OffsetOfCompressedData;
                }
                string mandatoryEndString = String.Format("Copyright {0} Cavedog Entertainment", DateTime.Now.Year);
                chunkWriter.Write(System.Text.Encoding.GetEncoding(437).GetBytes(mandatoryEndString));

                BinaryWriter headerWriter = new BinaryWriter(new MemoryStream());

                headerWriter.Write(HpiArchive.HeaderMarker);
                headerWriter.Write(HpiArchive.DefaultVersion);
                headerWriter.Write(chunkStartPosition);
                headerWriter.Write(HpiArchive.NoObfuscationKey);
                headerWriter.Write(HpiArchive.HeaderSize); //Directory Start

                IEnumerator<FileEntry> sequence = entries.Values.ToList().GetEnumerator();
                HpiArchive.SetEntries(tree, headerWriter, sequence);

                fileStream.Position = 0;
                headerWriter.BaseStream.Position = 0;
                headerWriter.BaseStream.CopyTo(fileStream);

                if(fileStream.Length > Int32.MaxValue)
                    MessageBox.Show("The HPI file was created, but its size exceeds 2GB (2 147 483 647 bytes). A fatal error may occur when loading the game.", "Oversize Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }
    }
}
