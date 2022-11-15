using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace HPIZ
{
    public class FilePathCollection : SortedList<string, string>
    {

        public FilePathCollection() : base(StringComparer.OrdinalIgnoreCase) { }

        public FilePathCollection(string fromDirectory)
        {
            fromDirectory = Path.GetFullPath(fromDirectory);
            var allFiles = Directory.EnumerateFiles(fromDirectory, "*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
                this.Add(file.Substring(fromDirectory.Length + 1), fromDirectory);
        }

        public FilePathCollection(string filePath, string sourcePath)
        {
            this.Add(filePath, sourcePath);
        }

        public void AddOrReplace(string filePath, string sourcePath)
        {
            if (this.ContainsKey(filePath))
                this[filePath] = sourcePath;
            else
                this.Add(filePath, sourcePath);
        }

    }
}
