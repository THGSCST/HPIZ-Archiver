using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;

namespace HPIZ
{
    public class PathCollection : SortedList<string, SortedSet<string>>
    {
        public PathCollection() : base(StringComparer.OrdinalIgnoreCase) {}
    }
}
