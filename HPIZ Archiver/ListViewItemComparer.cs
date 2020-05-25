using System;
using System.Collections;
using System.Windows.Forms;

namespace HPIZArchiver
{
    class ListViewItemCheckComparerAsc : IComparer
    {
        private int col;
        public ListViewItemCheckComparerAsc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return ((ListViewItem)x).Checked.CompareTo(((ListViewItem)y).Checked);
        }
    }
    class ListViewItemCheckComparerDesc : IComparer
    {
        private int col;
        public ListViewItemCheckComparerDesc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return ((ListViewItem)y).Checked.CompareTo(((ListViewItem)x).Checked);
        }
    }
    class ListViewItemStringComparerAsc : IComparer
    {
        private int col;
        public ListViewItemStringComparerAsc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text);
        }
    }
    class ListViewItemStringComparerDesc : IComparer
    {
        private int col;
        public ListViewItemStringComparerDesc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return String.Compare(((ListViewItem)y).SubItems[col].Text, ((ListViewItem)x).SubItems[col].Text);
        }
    }
    class ListViewItemIntComparerAsc : IComparer
    {
        private int col;
        public ListViewItemIntComparerAsc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return ((int)((ListViewItem)x).SubItems[col].Tag).CompareTo((int)((ListViewItem)y).SubItems[col].Tag);
        }
    }
    class ListViewItemIntComparerDesc : IComparer
    {
        private int col;
        public ListViewItemIntComparerDesc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return ((int)((ListViewItem)y).SubItems[col].Tag).CompareTo((int)((ListViewItem)x).SubItems[col].Tag);
        }
    }
    class ListViewItemFloatComparerAsc : IComparer
    {
        private int col;
        public ListViewItemFloatComparerAsc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return ((float)((ListViewItem)x).SubItems[col].Tag).CompareTo((float)((ListViewItem)y).SubItems[col].Tag);
        }
    }
    class ListViewItemFloatComparerDesc : IComparer
    {
        private int col;
        public ListViewItemFloatComparerDesc(int column = 0)
        {
            col = column;
        }
        public int Compare(object x, object y)
        {
            return ((float)((ListViewItem)y).SubItems[col].Tag).CompareTo((float)((ListViewItem)x).SubItems[col].Tag);
        }
    }
}
