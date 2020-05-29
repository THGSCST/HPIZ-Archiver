using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public static void CreateFromDirectory(string sourceDirectoryFullName, string destinationArchiveFileName, CompressionFlavor flavor, IProgress<string> progress)
        {
            destinationArchiveFileName = Path.GetFullPath(destinationArchiveFileName);
            var fileList = GetDirectoryFileList(sourceDirectoryFullName);
            CreateFromFileList(fileList.ToArray(), sourceDirectoryFullName, destinationArchiveFileName, progress, flavor);
        }

        public static SortedSet<string> GetDirectoryFileList(string sourceDirectoryFullName)
        {
            var fileList = new SortedSet<string>();
            sourceDirectoryFullName = Path.GetFullPath(sourceDirectoryFullName);
            DirectoryInfo directoryInfo = new DirectoryInfo(sourceDirectoryFullName);
            var files = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                fileList.Add(file.FullName.Substring(sourceDirectoryFullName.Length + 1));
            }
            return fileList;
        }

        public static void CreateFromFileList(string[] fileListShortName, string sourceDirectoryPath, string destinationArchiveFileName, IProgress<string> progress, CompressionFlavor flavor)
        {
            var files = new SortedDictionary<string, FileEntry>();

            for (int i = 0; i < fileListShortName.Length; i++)
            {

                string fullName = Path.Combine(sourceDirectoryPath, fileListShortName[i]);
                var file = new FileInfo(fullName);
                if (file.Length > Int32.MaxValue)
                    throw new Exception("File is too large: " + fileListShortName[i] + "Maximum allowed size is 2GB (2 147 483 647 bytes).");
                byte[] buffer = File.ReadAllBytes(fullName);

                files.Add(fileListShortName[i], new FileEntry(buffer, flavor, fullName, progress));

            }

            WriteToFile(destinationArchiveFileName, files);

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
                        File.WriteAllBytes(fullName, archive.Extract(entry));

                        //Report progress
                        if (progress != null) //&& i % progressLimiter == 0)
                            progress.Report(shortFileName);
                    }

                }
            }
        }

        public static void Merge(PathCollection archivesFiles, string destinationArchiveFileName, CompressionFlavor flavor, IProgress<string> progress)
        {
            var files = new SortedDictionary<string, FileEntry>();
            foreach (var archiveFullPath in archivesFiles.Keys)
                using (var archive = new HpiArchive(File.OpenRead(archiveFullPath)))
                    foreach (var shortFileName in archivesFiles[archiveFullPath])
                    {

                        var entry = archive.Entries[shortFileName];
                        var buffer = archive.Extract(entry);

                        if (files.ContainsKey(shortFileName))
                            files[shortFileName] = new FileEntry(buffer, flavor, shortFileName, progress);
                        else
                        files.Add(shortFileName, new FileEntry(buffer, flavor, shortFileName, progress));
                    }
            WriteToFile(destinationArchiveFileName, files);
        }

        private static void WriteToFile(string destinationArchiveFileName, SortedDictionary<string, FileEntry> entries)
        {
            var tree = new DirectoryTree(); //Provisory Tree
            foreach (var fileName in entries.Keys)
                tree.AddEntry(fileName);
            int directorySize = tree.CalculateSize() + 20;

            using (var fileStream = File.Create(destinationArchiveFileName))
            {
                BinaryWriter chunckWriter = new BinaryWriter(fileStream);

                chunckWriter.BaseStream.Position = directorySize;

                foreach (var file in entries)
                {
                    if(chunckWriter.BaseStream.Position > Int32.MaxValue)
                        throw new Exception("Maximum allowed size is 2GB (2 147 483 647 bytes).");

                    file.Value.OffsetOfCompressedData = (int)chunckWriter.BaseStream.Position;

                    if (file.Value.FlagCompression != CompressionMethod.None)
                        foreach (var size in file.Value.compressedChunkSizes)
                            chunckWriter.Write(size);

                    foreach (var chunk in file.Value.ChunkBytes)
                        chunckWriter.Write(chunk);
                }
                chunckWriter.Write("Copyright " + DateTime.Now.Year.ToString() + " Cavedog Entertainment"); //Endfile mandatory string

                BinaryWriter headerWriter = new BinaryWriter(new MemoryStream());

                headerWriter.BaseStream.Position = 0;

                headerWriter.Write(HpiArchive.HeaderMarker);
                headerWriter.Write(HpiArchive.DefaultVersion);

                tree = new DirectoryTree(); //Definitive Tree
                foreach (var item in entries.Keys)
                    tree.AddEntry(item);
                directorySize = tree.CalculateSize() + 20; //TO IMPROVE, BAD CODE
                headerWriter.Write(directorySize);

                headerWriter.Write(HpiArchive.NoObfuscationKey);

                int directoryStart = 20;
                headerWriter.Write(directoryStart); //Directory Start at Pos20

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
