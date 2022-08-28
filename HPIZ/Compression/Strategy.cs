using System;
using System.Collections.Generic;
using System.Text;

namespace HPIZ
{
    internal static class Strategy
    {
        //Size in bytes at which the compression becomes worthwhile. Small sizes get bigger if compressed.
        internal const int DeflateBreakEven = 64;

        //Try to compress, if there is NO reduction in size then let it store uncompressed.
        internal const bool TryCompressKeepIfWorthwhile = true;

        //Size in bytes where Zopfli compression starts to get better results than ZLib Deflate.
        internal const int ZopfliBreakEven = 1024;

    }
}
