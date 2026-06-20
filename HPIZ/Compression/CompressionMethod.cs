namespace HPIZ
{
    public enum CompressionMethod : byte
    {
        StoreUncompressed = 0,
        LZ77 = 1,
        ZLibDeflate = 2,
        ZopfliDeflate = 15
    }
}
