using System;
using System.Collections.Generic;
using System.Text;

namespace HPIZ
{
    public enum CompressionFlavor
    {
        StoreUncompressed = 0,
        ZLibDeflate = 1,
        i5ZopfliDeflate = 5,
        i10ZopfliDeflate = 10,
        i15ZopfliDeflate = 15

    }
}
