using System;
using System.Collections.Generic;
using System.Linq;

namespace HPIZ
{
    public class DirectoryTree : List<DirectoryNode>
    {
        private const string defaultSeparator = "\\";

        public DirectoryTree()
        {
        }

        public void AddEntry(string entry)
        {
             AddEntry(entry, 0);
        }

        /// <summary>
        /// Parses and adds the entry to the hierarchy, creating any parent entries as required.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="startIndex">The start index.</param>
        private void AddEntry(string entry, int startIndex)
        {
            if (startIndex >= entry.Length)
            {
                return;
            }

            var endIndex = entry.IndexOf(defaultSeparator, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex == -1)
            {
                endIndex = entry.Length;
            }
            var key = entry.Substring(startIndex, endIndex - startIndex);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            DirectoryNode item = this.FirstOrDefault(n => String.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                item = new DirectoryNode() { Key = key };
                Add(item);
            }
            // Now add the rest to the new item's children
            item.Children.AddEntry(entry, endIndex + 1);
        }

        public int CalculateSize()
        {
            return CalculateSize(this);
        }
        private int CalculateSize(DirectoryTree tree)
        {
            int totalSize = 8;

            foreach (var node in tree)
            {
                totalSize += 9;
                totalSize += node.Key.Length + 1;
                if (node.Children.Count != 0)
                    totalSize += CalculateSize(node.Children);
                else
                    totalSize += 9;
            }
            return totalSize;
        }
    }

    public class DirectoryNode
    {
        public string Key { get; set; }
        public DirectoryTree Children { get; set; }
        public DirectoryNode()
        {
            Children = new DirectoryTree();
        }
    }
}