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

            var endIndex = entry.IndexOf(defaultSeparator, startIndex, StringComparison.Ordinal);
            if (endIndex == -1)
            {
                endIndex = entry.Length;
            }
            var key = entry.Substring(startIndex, endIndex - startIndex);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            DirectoryNode item = this.FirstOrDefault(n => n.Key == key);
            if (item == null)
            {
                item = new DirectoryNode() { Key = key };
                Add(item);
            }
            // Now add the rest to the new item's children
            item.Children.AddEntry(entry, endIndex + 1);
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