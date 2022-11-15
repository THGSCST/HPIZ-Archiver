using System;
using System.Collections.Generic;

namespace HPIZ
{
    public class DirectoryNode
    {
        internal Dictionary<string, DirectoryNode> Children { get; set; }
        public DirectoryNode()
        {
            Children = new Dictionary<string, DirectoryNode>(StringComparer.OrdinalIgnoreCase);
        }

        public void AddEntry(string entry)
        {
            var sections = entry.Split('\\');
            AddEntryRecursively(sections, 0);
        }

        private void AddEntryRecursively(string[] entry, int level)
        {
            if (entry.Length > level)
            {
                if (Children.ContainsKey(entry[level]))
                    Children[entry[level]].AddEntryRecursively(entry, level + 1);
                else
                    OnlyAddRecursively(entry, level);
            }
        }
        private void OnlyAddRecursively(string[] entry, int level)
        {
            if (entry.Length > level)
            {
                Children.Add(entry[level], new DirectoryNode());
                Children[entry[level]].OnlyAddRecursively(entry, level + 1);
            }
        }

        public int CalculateSize()
        {
            int totalSize = 8;
            foreach (var item in Children)
            {
                totalSize += 9;
                totalSize += item.Key.Length + 1;
                if (item.Value.Children.Count != 0)
                    totalSize += item.Value.CalculateSize();
                else
                    totalSize += 9;
            }
            return totalSize;
        }
    }
}