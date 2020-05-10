# HPIZ Archiver
1997 Total Annihilation HPI Zopfli Archiver

A new tool for a great old game. View, extract and compress HPI files faster using multithreading and achieve maximum compression with Zopfli DEFLATE.

## Compression Benchmark

| File             | Original Size      | New Size (Zopfli) | Reduction | Time Elapsed |
|------------------|--------------------|-------------------|-----------|--------------|
| TA_Zero_Maps.ufo | 157 314 655 bytes  | 142 627 186 bytes | -9.3%     | ~32 minutes  |
| TAESC.gp3        |  99 095 144 bytes  |  88 196 692 bytes | -11.0%    | ~65 minutes  |
| ccmaps.ccx       | 153 714 300 bytes  | 139 594 570 bytes | -9.2%     | ~50 minutes  |
| totala4.hpi      | 147 577 290 bytes* | 111 326 120 bytes | -24.6%    | ~24 minutes  |

**totala4.hpi uses LZ77 compression method*

## Dependencies
Requires NET Framework 4.72 or higher.
