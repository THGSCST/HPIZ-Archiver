using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HPIZ
{
    public static class HpiFile
    {

        public static HpiArchive Open(string archiveFileName)
        { 
            using (FileStream fs = new FileStream(archiveFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
              return new HpiArchive(fs);
        }

        public static HpiArchive Create(string archiveFileName)
        {
            throw new System.NotImplementedException();
        }

        public static void CreateFromDirectory(string sourceDirectoryFullName, string destinationArchiveFileName)
        {
            destinationArchiveFileName = Path.GetFullPath(destinationArchiveFileName);
            var fileList = GetDirectoryFileList(sourceDirectoryFullName);
            CreateFromFileList(fileList, sourceDirectoryFullName, destinationArchiveFileName, null);
        }

        public static SortedSet<string> GetDirectoryFileList(string sourceDirectoryFullName)
        {
            var fileList = new SortedSet<string>();
            sourceDirectoryFullName = Path.GetFullPath(sourceDirectoryFullName);
            DirectoryInfo directoryInfo = new DirectoryInfo(sourceDirectoryFullName);
            long totalSize = 0;
            foreach (FileInfo fsi in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {

                totalSize += fsi.Length;
                if (totalSize > Int32.MaxValue) throw new Exception("Directory is too large. Maximum total size is 2GB (2 147 483 647 bytes).");
                fileList.Add(fsi.FullName.Substring(sourceDirectoryFullName.Length + 1));
            }
            return fileList;
        }


        public static void CreateFromFileList(SortedSet<string> fileList, string sourceDirectoryFullName, string destinationArchiveFileName, IProgress<string> progress)
        {
            var allFiles = FilesToChunks(fileList, sourceDirectoryFullName);

            Parallel.ForEach(fileList, file =>
             {
                 for (int index = 0; index < allFiles[file].Count; index++)
                 {
                      allFiles[file][index].Compress(true);

                     if (progress != null)
                         progress.Report(file + ":Chunk#" + index.ToString());
                 }

             });


            using (var fileStream = File.Create(destinationArchiveFileName))
            {
                var hpifile = HpiArchive.Encode(allFiles);
                hpifile.Position = 0;
                hpifile.CopyTo(fileStream);
            }

        }

        public static Stream SerializeChunks(SortedDictionary<string, List<Chunk>> chunks, out Queue<FileEntry> sequence)
        {
            var bw = new BinaryWriter(new MemoryStream());
            sequence = new Queue<FileEntry>(chunks.Count);
            foreach (var file in chunks)
            {
                
                int totalUncompressedSize = 0;
                int position = (int) bw.BaseStream.Position;
                foreach (var chunk in file.Value)
                    bw.Write(chunk.Data.Length + Chunk.SizeOfChunk);

                foreach (var chunk in file.Value)
                {
                    chunk.WriteBytes(bw);
                    totalUncompressedSize += chunk.DecompressedSize;
                }
                FileEntry fd = new FileEntry();
                fd.UncompressedSize = totalUncompressedSize;
                fd.OffsetOfCompressedData = position;
                fd.FlagCompression = CompressionMethod.ZLib;
                sequence.Enqueue(fd);

            }
            return bw.BaseStream;
        }

        private static SortedDictionary<string, List<Chunk>> FilesToChunks(SortedSet<string> fileList, string sourceDirectoryFullName)
        {
            var output = new SortedDictionary<string, List<Chunk>>();
            int chunkSize = 65536;

            foreach (var file in fileList)
            {
                string fullName = Path.Combine(sourceDirectoryFullName, file);
                using (FileStream fs = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, chunkSize, FileOptions.SequentialScan))
                {
                    if (fs.Length > Int32.MaxValue) throw new Exception("File is too large." + file + "Maximum total size is 2GB (2 147 483 647 bytes).");
                    int dataLenght = (int) fs.Length;
                    int chunkCount = (dataLenght / chunkSize) + (dataLenght % chunkSize == 0 ? 0 : 1);
                    int actualChunkSize = chunkSize;
                    var listChunk = new List<Chunk>(chunkCount);

                    for (int i = 0; i < chunkCount; i++)
                    {
                        if (chunkSize > dataLenght) actualChunkSize = dataLenght;
                        byte[] buffer = new byte[actualChunkSize];
                        dataLenght -= fs.Read(buffer, 0, actualChunkSize);
                        listChunk.Add(new Chunk(buffer, true));
                    }

                    output.Add(file, listChunk);
                }
            }
            return output;
        }


        public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName)
        {
            throw new System.NotImplementedException();
        }


    }
}
