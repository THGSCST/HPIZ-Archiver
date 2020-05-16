using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
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
            using (FileStream fs = new FileStream(archiveFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
              return new HpiArchive(fs);
        }

        public static HpiArchive Create(string archiveFileName)
        {
            throw new System.NotImplementedException();
        }

        public static void CreateFromDirectory(string sourceDirectoryFullName, string destinationArchiveFileName, CompressionFlavor flavor)
        {
            destinationArchiveFileName = Path.GetFullPath(destinationArchiveFileName);
            var fileList = GetDirectoryFileList(sourceDirectoryFullName);
            CreateFromFileList(fileList, sourceDirectoryFullName, destinationArchiveFileName, null, flavor);
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

        public static void CreateFromFileList(SortedSet<string> fileList, string sourceDirectoryFullName, string destinationArchiveFileName, IProgress<string> progress, CompressionFlavor flavor)
        {
            int totalFiles = fileList.Count;
            var fileNameArray = fileList.ToArray();
            var entrys = new FileEntry[totalFiles];
            var chunkBuffer = new byte[totalFiles][][];


            for (int i = 0; i < totalFiles; i++)
            {

                string fullName = Path.Combine(sourceDirectoryFullName, fileNameArray[i]);
                var fileSize = (int)new FileInfo(fullName).Length;
                if (fileSize > Int32.MaxValue)
                    throw new Exception("File is too large: " + fileNameArray[i] + "Maximum allowed size is 2GB (2 147 483 647 bytes).");
                byte[] buffer = File.ReadAllBytes(fullName);

                if (flavor != CompressionFlavor.StoreUncompressed && fileSize > 128) //Skip compression of small files
                {
                    int chunkCount = (buffer.Length / Chunk.MaxSize) + (buffer.Length % Chunk.MaxSize == 0 ? 0 : 1);
                    chunkBuffer[i] = new byte[chunkCount][];
                    var chunkSizes = new int[chunkCount];

                    // Parallelize chunk compression
                    Parallel.For(0, chunkCount, j =>
                    {
                        int size = Chunk.MaxSize;
                        if (j + 1 == chunkCount && buffer.Length != Chunk.MaxSize) size = buffer.Length % Chunk.MaxSize; //Last loop


                        chunkBuffer[i][j] = Chunk.Compress(new MemoryStream(buffer, j * Chunk.MaxSize, size).ToArray(), flavor);

                        chunkSizes[j] = chunkBuffer[i][j].Length;

                        if (progress != null)
                            progress.Report(fileNameArray[i] + ":Chunk#" + j.ToString());

                    }); // Parallel.For                    

                    entrys[i] = new FileEntry(fileSize, CompressionMethod.ZLib, chunkSizes);
                }
                else
                {
                    entrys[i] = new FileEntry(fileSize, CompressionMethod.None, null);
                    chunkBuffer[i] = new byte[1][];
                    chunkBuffer[i][0] = buffer;
                    if (progress != null)
                        progress.Report(fileNameArray[i]);
                }
            }

        var serialWriter = new BinaryWriter(new MemoryStream());
        var sequence = new Queue<FileEntry>(totalFiles);
        for (int i = 0; i < totalFiles; i++)
			{
                entrys[i].OffsetOfCompressedData = (int) serialWriter.BaseStream.Position;
                sequence.Enqueue(entrys[i]);

                if(entrys[i].FlagCompression != CompressionMethod.None)
                foreach (var size in entrys[i].ChunkSizes)
                    serialWriter.Write(size);

                foreach (var chunk in chunkBuffer[i])
                    serialWriter.Write(chunk);
            }

            
        var tree = new DirectoryTree();
            foreach (var item in fileNameArray)
                tree.AddEntry(item);

  
        BinaryWriter bw = new BinaryWriter(new MemoryStream());

        bw.Write(HPIZ.HpiArchive.HeaderMarker);
        bw.Write(HpiArchive.DefaultVersion);

            int directorySize = HpiArchive.GetDirectorySize(tree) + 20;
        bw.Write(directorySize);

            bw.Write(HpiArchive.NoObfuscationKey);

            int directoryStart = 20;
        bw.Write(directoryStart); //Directory Start at Pos20, always start it next

            HpiArchive.SetEntries(tree, bw, sequence, directorySize);

            serialWriter.BaseStream.Position = 0;
            bw.BaseStream.Position = bw.BaseStream.Length;
            serialWriter.BaseStream.CopyTo(bw.BaseStream);

            bw.Write("Copyright " + DateTime.Now.Year.ToString() + " Cavedog Entertainment"); //Endfile mandatory string



            using (var fileStream = File.Create(destinationArchiveFileName))
            {
                bw.BaseStream.Position = 0;
                bw.BaseStream.CopyTo(fileStream);
            }

        }

        public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName)
        {
            throw new System.NotImplementedException();
        }


    }
}
