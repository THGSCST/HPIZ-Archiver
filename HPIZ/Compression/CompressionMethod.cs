namespace HPIZ
{
    public enum CompressionMethod : byte
    {
        StoreUncompressed = 0,
        LZ77 = 1,
        ZLibDeflate = 2,
        i5ZopfliDeflate = 5,
        i10ZopfliDeflate = 10,
        i15ZopfliDeflate = 15
    }
}
