using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HPIZ
{
    public class DirectoryNode
    {
        internal Dictionary<string, DirectoryNode> Children { get; private set; }
        private DirectoryNode()
        {
            Children = new Dictionary<string, DirectoryNode>(StringComparer.OrdinalIgnoreCase);
        }

        public DirectoryNode(IEnumerable<string> paths) : this()
        {
            foreach (var path in paths)
            {
                var folders = path.Split('\\');
                DirectoryNode current = this;

                foreach (var folder in folders)
                {
                    if (!current.Children.TryGetValue(folder, out var next))
                    {
                        next = new DirectoryNode();
                        current.Children.Add(folder, next);
                    }
                    current = next;
                }
            }
        }

        public int CalculateTreeSize()
        {
            int totalSize = 8;
            foreach (var node in Children)
            {
                totalSize += 9;
                totalSize += node.Key.Length + 1;
                if (node.Value.Children.Count != 0)
                    totalSize += node.Value.CalculateTreeSize();
                else
                    totalSize += 9;
            }
            return totalSize;
        }

        public void WriteTree(BinaryWriter bw, SortedDictionary<string, FileEntry>.Enumerator sequence)
        {
            bw.Write(Children.Count); //Root Entries number in directory
            bw.Write((int)bw.BaseStream.Position + 4); //Entries Offset point to next

            foreach (var item in Children)
            {
                int positionNameOffset = (int)bw.BaseStream.Length;
                if (bw.BaseStream.Length == bw.BaseStream.Position)
                    positionNameOffset = (int)bw.BaseStream.Position + Children.Count * 9;
                
                bw.Write(positionNameOffset); //NameOffset;      /* points to the file name */
                int positionDataOrChild = positionNameOffset + item.Key.Length + 1;
                bw.Write(positionDataOrChild); //DataOffset;   /* points to directory data */
                bool isDirectory = item.Value.Children.Count != 0;
                bw.Write(isDirectory);

                int previousPosition = (int)bw.BaseStream.Position;
                bw.BaseStream.Position = positionNameOffset;

                WriteStringCP437NullTerminated(bw, item.Key);
                if (isDirectory)
                    item.Value.WriteTree(bw, sequence);
                else
                {
                    sequence.MoveNext();
                    Debug.Assert(sequence.Current.Key.EndsWith(item.Key, StringComparison.OrdinalIgnoreCase));
                    bw.Write(sequence.Current.Value.CompressedDataOffset); //OffsetOfData
                    bw.Write(sequence.Current.Value.UncompressedSize); //UncompressedSize 
                    bw.Write((byte)sequence.Current.Value.FlagCompression); //FlagCompression 
                }
                bw.BaseStream.Position = previousPosition;
            }
        }
        private static void WriteStringCP437NullTerminated(BinaryWriter reader, string text)
        {
            Encoding codePage437 = Encoding.GetEncoding(437);
            reader.Write(codePage437.GetBytes(text));
            reader.Write(byte.MinValue); //Zero byte to end string
        }
    }
}