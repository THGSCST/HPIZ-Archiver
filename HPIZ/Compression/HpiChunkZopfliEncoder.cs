using System;
using System.Collections.Generic;
using System.IO;

namespace HPIZ.Compression
{
    /// <summary>
    /// Encodes one independent HPI chunk with a Zopfli-derived optimizer.
    /// This is not a general-purpose Zopfli or DEFLATE API.
    /// </summary>
    internal sealed class HpiChunkZopfliEncoder
    {
        /// <summary>
        /// Creates an encoder for one HPI chunk.
        /// </summary>
        /// <param name="stream">Writable destination for the raw DEFLATE payload.</param>
        internal HpiChunkZopfliEncoder(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable.", nameof(stream));

            _writer = new BitWriter(stream);
        }

        private const int CacheLength = 8;
        internal const int MaxChunkSize = global::HPIZ.Chunk.MaxSize;
        private const int MaximumMatch = 258;
        private const int MinimumMatch = 3;
        private const int WindowMask = (WindowSize - 1);
        private const int WindowSize = 32768;
        private const int HuffmanNodePoolSize = 8866;
        private const int MaximumBlockSplitting = 15;
        private const int MaximumChainHits = 8192;
        private const int NumberOfIterations = 15;

        // i15 is the only Zopfli mode. It is a hard cap, while converged data can stop early
        // after the randomized cost model has had enough passes to explore.
        private const int MinIterationsBeforeEarlyExit = 7;
        private const int MaxIterationsWithoutImprovement = 3;
        private const double MeaningfulImprovementBits = 8.0;

        private readonly BitWriter _writer;
        private int _chunkStart;
        private bool _used;

        /// <summary>
        /// Compresses one independent HPI chunk as a complete raw DEFLATE stream.
        /// </summary>
        internal void EncodeChunk(byte[] buffer, int offset, int count)
        {
            if (_used)
                throw new InvalidOperationException("A chunk encoder instance cannot be reused.");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count <= 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();
            if (count > MaxChunkSize)
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    "The specialized engine accepts at most one 65,536-byte HPI chunk.");

            _used = true;
            _chunkStart = offset;
            int end = offset + count;
            DeflateSplittingFirst(buffer, offset, end, true);
            _writer.FlushBits();
        }

        private List<int> BlockSplit(byte[] buffer, int bufferStart, int bufferEnd)
        {
            BlockState blockState = new BlockState(this)
            {
                _blockStart = bufferStart,
                _blockEnd = bufferEnd,
                _cache = null
            };

            var store = blockState.FindStandardBlock(buffer);

            var lz77splitpoints = store.BlockSplit(MaximumBlockSplitting);
            var nlz77points = lz77splitpoints.Count;

            if (nlz77points == 0)
                return new List<int>();

            var split = new List<int>();
            var position = bufferStart;
            var storeSize = store.Size;

            for (int i = 0; i < storeSize; i++)
            {
                if (lz77splitpoints[split.Count] == i)
                {
                    split.Add(position);

                    if (split.Count == nlz77points)
                        break;
                }

                position += store._distances[i] == 0 ? 1 : store._literalLengths[i];
            }

            return split;
        }

        private void DeflateSplittingFirst(byte[] buffer, int bufferStart, int bufferEnd, bool finalBlock)
        {
            var splitpoints = BlockSplit(buffer, bufferStart, bufferEnd);
            var stores = new BlockStore[splitpoints.Count + 1];

            for (int i = 0; i < stores.Length; i++)
            {
                int splitStart = i == 0 ? bufferStart : splitpoints[i - 1];
                int splitEnd = i == splitpoints.Count ? bufferEnd : splitpoints[i];

                stores[i] = DeflateDynamicBlock(buffer, splitStart, splitEnd, i == splitpoints.Count && finalBlock, true);
            }

            for (int i = 0; i < stores.Length; i++)
            {
                stores[i].WriteBlock(0, stores[i].Size, i == stores.Length - 1 && finalBlock, _writer);
            }
        }

        private BlockStore DeflateDynamicBlock(byte[] buffer, int blockStart, int blockEnd, bool finalBlock, bool delayWrite)
        {
            var cache = LongestMatchCache.Rent(blockEnd - blockStart);
            BlockState blockState = new BlockState(this)
            {
                _blockStart = blockStart,
                _blockEnd = blockEnd,
                _cache = cache
            };

            var store = blockState.FindOptimalBlock(buffer);

            if (store.Size < 1000)
            {
                var fixedstore = blockState.FindOptimalFixedBlock(buffer);
                var dyncost = store.CalculateBlockSize(0, store.Size);
                var fixedcost = fixedstore.CalculateBlockSize(0, fixedstore.Size);

                if (fixedcost < dyncost)
                {
                    store = fixedstore;
                }
            }

            LongestMatchCache.Return(cache);

            if (!delayWrite)
            {
                store.WriteBlock(0, store.Size, finalBlock, _writer);
                return null;
            }

            return store;
        }

        private class SymbolStatistics
        {

            internal double[] _distanceSymbolLengths = new double[32];

            internal int[] _dists = new int[32];

            internal double[] _literalLengthSymbolLengths = new double[288];

            internal int[] _litlens = new int[288];

            public SymbolStatistics()
            {
            }

            public void CalculateWeighted(SymbolStatistics other, double thisWeight, double otherWeight)
            {
                int i;
                for (i = 0; i < 288; i++)
                    _litlens[i] = (int)(_litlens[i] * thisWeight + other._litlens[i] * otherWeight);

                for (i = 0; i < 32; i++)
                    _dists[i] = (int)(_dists[i] * thisWeight + other._dists[i] * otherWeight);

                _litlens[256] = 1;

                Calculate();
            }

            public void Calculate()
            {
                CalculateEntropy(_litlens, 288, ref _literalLengthSymbolLengths);
                CalculateEntropy(_dists, 32, ref _distanceSymbolLengths);
            }

            public void CalculateRandomized(RanState state)
            {

                RandomizeFreqs(state, _litlens, 288);
                RandomizeFreqs(state, _dists, 32);

                _litlens[256] = 1;

                Calculate();
            }

            private static void RandomizeFreqs(RanState state, int[] freqs, int n)
            {
                for (int i = 0; i < n; i++)
                    if ((state.Next() >> 4) % 3 == 0)
                        freqs[i] = freqs[(int)(state.Next() % (uint)n)];
            }

            public void CopyFrom(SymbolStatistics source)
            {
                Array.Copy(source._litlens, _litlens, _litlens.Length);
                Array.Copy(source._dists, _dists, _dists.Length);
                Array.Copy(source._literalLengthSymbolLengths, _literalLengthSymbolLengths, _literalLengthSymbolLengths.Length);
                Array.Copy(source._distanceSymbolLengths, _distanceSymbolLengths, _distanceSymbolLengths.Length);
            }

            private const double kInvLog2 = 1.4426950408889;

            private static void CalculateEntropy(int[] count, int n, ref double[] bitlengths)
            {
                long sum = 0;
                int i;
                for (i = 0; i < n; ++i)
                    sum += count[i];

                double log2sum = (sum == 0 ? Math.Log(n) : Math.Log(sum)) * kInvLog2;

                for (i = 0; i < n; i++)
                {

                    if (count[i] == 0)
                        bitlengths[i] = log2sum;

                    else
                        bitlengths[i] = log2sum - Math.Log(count[i]) * kInvLog2;

                    if (bitlengths[i] < 0 && bitlengths[i] > -1e-5)
                        bitlengths[i] = 0;
                }
            }

        }

        private sealed class RanState
        {
            private uint _z = 1;
            private uint _w = 2;

            public uint Next()
            {
                unchecked
                {
                    _z = 36969 * (_z & 65535) + (_z >> 16);
                    _w = 18000 * (_w & 65535) + (_w >> 16);
                    return (_z << 16) + _w;
                }
            }
        }

        private sealed class Node
        {
            [ThreadStatic]
            private static Node[][] _listsScratch;

            [ThreadStatic]
            private static Node[] _leavesScratch;

            private int _count;
            private int _weight;
            private Node _tail;

            private Node() { }

            private Node(int weight, int count, Node tail)
            {
                _weight = weight;
                _count = count;
                _tail = tail;
            }

            private sealed class NodePool
            {
                [ThreadStatic]
                private static Node[] _freePool;
                private int _unusedNode;
                private Node[] _pool;

                public NodePool(int size)
                {
                    _unusedNode = 0;
                    _pool = _freePool;
                    _freePool = null;
                    if (_pool == null || _pool.Length != size)
                    {
                        _pool = new Node[size];
                        for (int i = 0; i < size; i++)
                            _pool[i] = new Node();
                    }
                }

                public void ReturnPool()
                {
                    _freePool = _pool;
                }

                public Node CreateNew(int weight, int count, Node tail)
                {
                    Node ret = _pool[_unusedNode++];
                    ret._weight = weight;
                    ret._count = count;
                    ret._tail = tail;
                    return ret;
                }
            }

            public static void LengthLimitedCodeLengths(int[] frequencies, int n, int maxBits, ref int[] bitlengths)
            {
                Node[][] lists = _listsScratch;
                if (lists == null)
                {
                    lists = new[] { new Node[15], new Node[15] };
                    _listsScratch = lists;
                }

                NodePool pool = new NodePool(HuffmanNodePoolSize);
                Node[] leafList = _leavesScratch;
                if (leafList == null || leafList.Length < n)
                {
                    leafList = new Node[n];
                    _leavesScratch = leafList;
                }

                int numsymbols = 0;

                for (int i = 0; i < n; i++)
                {
                    if (frequencies[i] > 0)
                    {
                        leafList[numsymbols++] = pool.CreateNew(frequencies[i], i, null);
                    }
                }

                if (numsymbols == 0)
                {
                    pool.ReturnPool();
                    return;
                }

                if (numsymbols == 1)
                {
                    bitlengths[leafList[0]._count] = 1;
                    pool.ReturnPool();
                    return;
                }

                Array.Sort(leafList, 0, numsymbols, NodeWeightComparer.Instance);

                Node node0 = pool.CreateNew(leafList[0]._weight, 1, null);
                Node node1 = pool.CreateNew(leafList[1]._weight, 2, null);

                for (int i = 0; i < maxBits; i++)
                {
                    lists[0][i] = node0;
                    lists[1][i] = node1;
                }

                var numBoundaryPMRuns = 2 * numsymbols - 4;
                for (int i = 0; i < numBoundaryPMRuns; i++)
                {
                    bool final = (i == numBoundaryPMRuns - 1);
                    BoundaryPackageMerge(ref lists, pool, maxBits, leafList, numsymbols, (maxBits - 1), final);
                }

                ExtractBitLengths(lists[1][maxBits - 1], leafList, ref bitlengths);

                pool.ReturnPool();
            }

            private static void BoundaryPackageMerge(ref Node[][] lists, NodePool pool, int maxbits, Node[] leaves, int numsymbols, int index, bool final)
            {
                int lastcount = lists[1][index]._count;

                if (index == 0 && lastcount >= numsymbols)
                    return;

                Node newchain = pool.CreateNew(0, 0, null);
                Node oldchain = lists[1][index];

                lists[0][index] = oldchain;
                lists[1][index] = newchain;

                if (index == 0)
                {

                    newchain._weight = leaves[lastcount]._weight;
                    newchain._count = lastcount + 1;
                }
                else
                {
                    int sum = lists[0][index - 1]._weight + lists[1][index - 1]._weight;
                    if (lastcount < numsymbols && sum > leaves[lastcount]._weight)
                    {

                        newchain._weight = leaves[lastcount]._weight;
                        newchain._count = lastcount + 1;
                        newchain._tail = oldchain._tail;
                    }
                    else
                    {
                        newchain._weight = sum;
                        newchain._count = lastcount;
                        newchain._tail = lists[1][index - 1];

                        if (!final)
                        {

                            BoundaryPackageMerge(ref lists, pool, maxbits, leaves, numsymbols, index - 1, false);
                            BoundaryPackageMerge(ref lists, pool, maxbits, leaves, numsymbols, index - 1, false);
                        }
                    }
                }
            }

            private static void ExtractBitLengths(Node chain, Node[] leaves, ref int[] bitlengths)
            {

                for (Node node = chain; node != null; node = node._tail)
                {
                    for (int i = 0; i < node._count; i++)
                    {
                        bitlengths[leaves[i]._count]++;
                    }
                }
            }

            private sealed class NodeWeightComparer : IComparer<Node>
            {
                internal static readonly NodeWeightComparer Instance = new NodeWeightComparer();

                public int Compare(Node left, Node right)
                {
                    int weightComparison = left._weight.CompareTo(right._weight);
                    return weightComparison != 0
                        ? weightComparison
                        : left._count.CompareTo(right._count);
                }
            }
        }

        private sealed class BlockStore
        {
            private const int InitialCapacity = 16;

            [ThreadStatic]
            private static BlockSizeScratch _blockSizeScratch;

            [ThreadStatic]
            private static DynamicTreeScratch _dynamicTreeScratch;

            [ThreadStatic]
            private static WriteBlockScratch _writeBlockScratch;

            [ThreadStatic]
            private static DynamicLengthsScratch _dynamicLengthsScratch;

            [ThreadStatic]
            private static bool[] _goodForRleScratch;

            // Blocked prefix-sum histograms used only while this store is being block-split.
            // Flat checkpoint tables: _llPrefix[k*288 + sym] / _dPrefix[k*32 + sym] hold the
            // symbol counts over [0, k<<PrefixShiftBits). They turn CalculateLZ77Counts over a
            // range into O(#symbols + step) instead of O(range). Buffers are pooled per thread.
            private const int PrefixShiftBits = 8; // checkpoint every 256 LZ77 symbols

            [ThreadStatic]
            private static int[] _llPrefixScratch;

            [ThreadStatic]
            private static int[] _dPrefixScratch;

            public BlockStore(ChunkBlockType blockType)
            {
                _blockType = blockType;
            }

            internal ushort[] _distances;

            internal ushort[] _literalLengths;

            private readonly ChunkBlockType _blockType;
            private int _count;

            private int _prefixShift; // 0 = prefix histograms inactive
            private int[] _llPrefix;
            private int[] _dPrefix;

            public int Size => _count;

            public ChunkBlockType BlockType => _blockType;

            public void Add(int length, int distance)
            {
                EnsureCapacity(_count + 1);
                _literalLengths[_count] = (ushort)length;
                _distances[_count] = (ushort)distance;
                _count++;
            }

            private void EnsureCapacity(int requiredCapacity)
            {
                if (_literalLengths != null && requiredCapacity <= _literalLengths.Length)
                    return;

                int capacity = _literalLengths == null
                    ? InitialCapacity
                    : _literalLengths.Length * 2;
                if (capacity < requiredCapacity)
                    capacity = requiredCapacity;

                Array.Resize(ref _literalLengths, capacity);
                Array.Resize(ref _distances, capacity);
            }

            public void WriteBlock(int start, int end, bool finalBlock, BitWriter writer)
            {
                WriteBlockScratch scratch = _writeBlockScratch
                    ?? (_writeBlockScratch = new WriteBlockScratch());
                int[] ll_lengths = scratch.LiteralLengthLengths;
                int[] d_lengths = scratch.DistanceLengths;
                int[] ll_symbols = scratch.LiteralLengthSymbols;
                int[] d_symbols = scratch.DistanceSymbols;

                Array.Clear(ll_lengths, 0, ll_lengths.Length);
                Array.Clear(d_lengths, 0, d_lengths.Length);
                Array.Clear(ll_symbols, 0, ll_symbols.Length);
                Array.Clear(d_symbols, 0, d_symbols.Length);

                writer.Write(finalBlock);

                if (_blockType == ChunkBlockType.Fixed)
                {
                    writer.Write(true);
                    writer.Write(false);

                    GetFixedTree(ref ll_lengths, ref d_lengths);
                }
                else
                {
                    writer.Write(false);
                    writer.Write(true);

                    GetDynamicLengths(start, end, ll_lengths, d_lengths);
                    AddDynamicTree(ll_lengths, d_lengths, writer);
                }

                ConvertLengthsToSymbols(ll_lengths, 288, 15, ref ll_symbols);
                ConvertLengthsToSymbols(d_lengths, 32, 15, ref d_symbols);

                WriteBlockData(start, end, ll_symbols, ll_lengths, d_symbols, d_lengths, writer);

                writer.WriteHuffman(ll_symbols[256], ll_lengths[256]);
            }

            private void WriteBlockData(int symbolStart, int symbolEnd, int[] ll_symbols, int[] ll_lengths, int[] d_symbols, int[] d_lengths, BitWriter writer)
            {

                for (int i = symbolStart; i < symbolEnd; i++)
                {
                    int dist = _distances[i];
                    int litlen = _literalLengths[i];

                    if (dist == 0)
                    {
                        writer.WriteHuffman(ll_symbols[litlen], ll_lengths[litlen]);
                    }
                    else
                    {
                        int lls = _lengthSymbolTable[litlen];
                        int ds = GetDistSymbol(dist);

                        writer.WriteHuffman(ll_symbols[lls], ll_lengths[lls]);

                        writer.Write(_lengthExtraBitsValueTable[litlen], _lengthExtraBitsTable[litlen]);

                        writer.WriteHuffman(d_symbols[ds], d_lengths[ds]);

                        writer.Write(GetDistExtraBitsValue(dist), GetDistExtraBits(dist));

                    }
                }
            }

            private static int CalculateDynamicTreeSize(int[] ll_lengths, int[] d_lengths)
            {
                return AddDynamicTree(ll_lengths, d_lengths, null);
            }

            private static readonly int[] _addDynamicTreeOrderTable = new int[] { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

            private static int AddDynamicTree(int[] ll_lengths, int[] d_lengths, BitWriter writer)
            {
                bool writeOutput = writer != null;
                DynamicTreeScratch scratch = _dynamicTreeScratch
                    ?? (_dynamicTreeScratch = new DynamicTreeScratch());
                int[] lld_lengths = scratch.LldLengths;
                int[] rle = scratch.Rle;
                int[] rleBits = scratch.RleBits;
                int[] clcounts = scratch.CodeLengthCounts;
                int[] clcl = scratch.CodeLengthLengths;
                int[] clsymbols = scratch.CodeLengthSymbols;
                int lld_total;
                int rleCount = 0;
                int hlit = 29;
                int hdist = 29;
                int hclen;

                Array.Clear(clcounts, 0, clcounts.Length);
                Array.Clear(clcl, 0, clcl.Length);
                Array.Clear(clsymbols, 0, clsymbols.Length);

                while (hlit > 0 && ll_lengths[257 + hlit - 1] == 0) hlit--;
                while (hdist > 0 && d_lengths[1 + hdist - 1] == 0) hdist--;

                lld_total = hlit + 257 + hdist + 1;

                for (int i = 0; i < lld_total; i++)
                {
                    lld_lengths[i] = i < 257 + hlit
                        ? ll_lengths[i] : d_lengths[i - 257 - hlit];
                }

                for (int i = 0; i < lld_total; i++)
                {
                    int count = 0;

                    for (int j = i; j < lld_total && lld_lengths[i] == lld_lengths[j]; j++)
                    {
                        count++;
                    }
                    if (count >= 4 || (count >= 3 && lld_lengths[i] == 0))
                    {
                        if (lld_lengths[i] == 0)
                        {
                            if (count > 10)
                            {
                                if (count > 138) count = 138;
                                rle[rleCount] = 18;
                                rleBits[rleCount++] = count - 11;
                            }
                            else
                            {
                                rle[rleCount] = 17;
                                rleBits[rleCount++] = count - 3;
                            }
                        }
                        else
                        {
                            int repeat = count - 1;

                            rle[rleCount] = lld_lengths[i];
                            rleBits[rleCount++] = 0;

                            while (repeat >= 6)
                            {
                                rle[rleCount] = 16;
                                rleBits[rleCount++] = 3;
                                repeat -= 6;
                            }
                            if (repeat >= 3)
                            {
                                rle[rleCount] = 16;
                                rleBits[rleCount++] = 0;

                                repeat -= 3;
                            }
                            while (repeat != 0)
                            {
                                rle[rleCount] = lld_lengths[i];
                                rleBits[rleCount++] = 0;
                                repeat--;
                            }
                        }

                        i += count - 1;
                    }
                    else
                    {
                        rle[rleCount] = lld_lengths[i];
                        rleBits[rleCount++] = 0;
                    }
                }

                for (int i = 0; i < rleCount; i++)
                {
                    clcounts[rle[i]]++;
                }

                CalculateBitLengths(clcounts, 19, 7, ref clcl);
                ConvertLengthsToSymbols(clcl, 19, 7, ref clsymbols);

                hclen = 15;

                while (hclen > 0 && clcounts[_addDynamicTreeOrderTable[hclen + 4 - 1]] == 0) hclen--;

                if (writeOutput)
                {
                    writer.Write(hlit, 5);
                    writer.Write(hdist, 5);
                    writer.Write(hclen, 4);
                }

                int bitSize = 5 + 5 + 4;

                for (int i = 0; i < hclen + 4; i++)
                {
                    if (writeOutput)
                        writer.Write(clcl[_addDynamicTreeOrderTable[i]], 3);

                    bitSize += 3;
                }

                for (int i = 0; i < rleCount; i++)
                {
                    int symbol = clsymbols[rle[i]];

                    if (writeOutput)
                        writer.WriteHuffman(symbol, clcl[rle[i]]);

                    bitSize += clcl[rle[i]];

                    if (rle[i] == 16)
                    {
                        if (writeOutput)
                            writer.Write(rleBits[i], 2);

                        bitSize += 2;
                    }
                    else if (rle[i] == 17)
                    {
                        if (writeOutput)
                            writer.Write(rleBits[i], 3);

                        bitSize += 3;
                    }
                    else if (rle[i] == 18)
                    {
                        if (writeOutput)
                            writer.Write(rleBits[i], 7);

                        bitSize += 7;
                    }
                }

                return bitSize;
            }

            private static void GetFixedTree(ref int[] ll_lengths, ref int[] d_lengths)
            {
                for (int i = 0; i < 144; i++) ll_lengths[i] = 8;
                for (int i = 144; i < 256; i++) ll_lengths[i] = 9;
                for (int i = 256; i < 280; i++) ll_lengths[i] = 7;
                for (int i = 280; i < 288; i++) ll_lengths[i] = 8;
                for (int i = 0; i < 32; i++) d_lengths[i] = 5;
            }

            private static unsafe void ConvertLengthsToSymbols(int[] lengths, int symbolSize, int maxbits, ref int[] symbols)
            {
                int* blCount = stackalloc int[16];
                int* nextCode = stackalloc int[16];
                int bits;

                for (int i = 0; i <= maxbits; i++)
                {
                    blCount[i] = 0;
                    nextCode[i] = 0;
                }

                for (int i = 0; i < symbolSize; i++)
                {
                    blCount[lengths[i]]++;
                }

                int code = 0;
                blCount[0] = 0;
                for (bits = 1; bits <= maxbits; bits++)
                {
                    code = ((code + blCount[bits - 1]) << 1);
                    nextCode[bits] = code;
                }

                for (int i = 0; i < symbolSize; i++)
                {
                    int len = lengths[i];
                    if (len != 0)
                    {
                        symbols[i] = nextCode[len];
                        nextCode[len]++;
                    }
                }
            }

            private static void PatchDistanceCodesForBuggyDecoders(int[] d_lengths)
            {
                int numberOfDistanceCodes = 0;

                for (int i = 0; i < 30; i++)
                {
                    if (d_lengths[i] != 0 && ++numberOfDistanceCodes == 2)
                        return;
                }

                if (numberOfDistanceCodes == 0)
                {
                    d_lengths[0] = d_lengths[1] = 1;
                }
                else if (numberOfDistanceCodes == 1)
                {
                    d_lengths[d_lengths[0] != 0 ? 1 : 0] = 1;
                }
            }

            public long CalculateBlockSize(int start, int end)
            {
                BlockSizeScratch scratch = _blockSizeScratch
                    ?? (_blockSizeScratch = new BlockSizeScratch());
                int[] literalLengthCounts = scratch.LiteralLengthCounts;
                int[] distanceCounts = scratch.DistanceCounts;
                int[] literalLengthLengths = scratch.LiteralLengthLengths;
                int[] distanceLengths = scratch.DistanceLengths;

                Array.Clear(literalLengthCounts, 0, literalLengthCounts.Length);
                Array.Clear(distanceCounts, 0, distanceCounts.Length);
                Array.Clear(literalLengthLengths, 0, literalLengthLengths.Length);
                Array.Clear(distanceLengths, 0, distanceLengths.Length);

                if (_blockType == ChunkBlockType.Fixed)
                {
                    GetFixedTree(ref literalLengthLengths, ref distanceLengths);

                    return 3 + CalculateBlockSymbolSize(literalLengthLengths, distanceLengths, start, end);
                }

                CalculateLZ77Counts(start, end, ref literalLengthCounts, ref distanceCounts);
                CalculateBitLengths(literalLengthCounts, 288, 15, ref literalLengthLengths);
                CalculateBitLengths(distanceCounts, 32, 15, ref distanceLengths);
                PatchDistanceCodesForBuggyDecoders(distanceLengths);

                return 3 + CalculateDynamicTreeSize(literalLengthLengths, distanceLengths)
                    + CalculateBlockSymbolSizeGivenCounts(literalLengthCounts, distanceCounts, literalLengthLengths, distanceLengths);
            }

            private void GetDynamicLengths(int start, int end, int[] ll_lengths, int[] d_lengths)
            {
                DynamicLengthsScratch scratch = _dynamicLengthsScratch
                    ?? (_dynamicLengthsScratch = new DynamicLengthsScratch());
                int[] ll_counts = scratch.LiteralLengthCounts;
                int[] d_counts = scratch.DistanceCounts;
                int[] ll_counts2 = scratch.OptimizedLiteralLengthCounts;
                int[] d_counts2 = scratch.OptimizedDistanceCounts;
                int[] ll_lengths2 = scratch.OptimizedLiteralLengthLengths;
                int[] d_lengths2 = scratch.OptimizedDistanceLengths;

                Array.Clear(ll_counts, 0, ll_counts.Length);
                Array.Clear(d_counts, 0, d_counts.Length);
                Array.Clear(ll_lengths, 0, ll_lengths.Length);
                Array.Clear(d_lengths, 0, d_lengths.Length);
                Array.Clear(ll_lengths2, 0, ll_lengths2.Length);
                Array.Clear(d_lengths2, 0, d_lengths2.Length);
                CalculateLZ77Counts(start, end, ref ll_counts, ref d_counts);

                CalculateBitLengths(ll_counts, 288, 15, ref ll_lengths);
                CalculateBitLengths(d_counts, 32, 15, ref d_lengths);
                PatchDistanceCodesForBuggyDecoders(d_lengths);
                long size1 = CalculateDynamicTreeSize(ll_lengths, d_lengths)
                    + CalculateBlockSymbolSizeGivenCounts(ll_counts, d_counts, ll_lengths, d_lengths);

                Array.Copy(ll_counts, ll_counts2, ll_counts.Length);
                Array.Copy(d_counts, d_counts2, d_counts.Length);
                OptimizeHuffmanCountsForRle(32, d_counts2);
                OptimizeHuffmanCountsForRle(288, ll_counts2);

                CalculateBitLengths(ll_counts2, 288, 15, ref ll_lengths2);
                CalculateBitLengths(d_counts2, 32, 15, ref d_lengths2);
                PatchDistanceCodesForBuggyDecoders(d_lengths2);

                long size2 = CalculateDynamicTreeSize(ll_lengths2, d_lengths2)
                    + CalculateBlockSymbolSizeGivenCounts(ll_counts, d_counts, ll_lengths2, d_lengths2);

                if (size2 < size1)
                {
                    Array.Copy(ll_lengths2, ll_lengths, ll_lengths.Length);
                    Array.Copy(d_lengths2, d_lengths, d_lengths.Length);
                }
            }

            private static long CalculateBlockSymbolSizeGivenCounts(int[] ll_counts, int[] d_counts, int[] ll_lengths, int[] d_lengths)
            {
                long result = 0;

                for (int i = 0; i < 286; i++)
                    result += (long)ll_lengths[i] * ll_counts[i];

                for (int i = 265; i < 269; i++) result += ll_counts[i];
                for (int i = 269; i < 273; i++) result += (long)ll_counts[i] * 2;
                for (int i = 273; i < 277; i++) result += (long)ll_counts[i] * 3;
                for (int i = 277; i < 281; i++) result += (long)ll_counts[i] * 4;
                for (int i = 281; i < 285; i++) result += (long)ll_counts[i] * 5;

                for (int i = 0; i < 30; i++)
                    result += (long)d_lengths[i] * d_counts[i];

                for (int i = 4; i < 30; i++)
                    result += (long)((i - 2) / 2) * d_counts[i];

                return result;
            }

            private static void OptimizeHuffmanCountsForRle(int length, int[] counts)
            {

                for (; ; --length)
                {
                    if (length == 0) return;
                    if (counts[length - 1] != 0) break;
                }

                bool[] goodForRle = _goodForRleScratch;
                if (goodForRle == null || goodForRle.Length < length)
                {
                    goodForRle = new bool[length];
                    _goodForRleScratch = goodForRle;
                }
                else
                {
                    Array.Clear(goodForRle, 0, length);
                }

                long symbol = counts[0];
                int stride = 0;
                for (int i = 0; i < length + 1; ++i)
                {
                    if (i == length || counts[i] != symbol)
                    {
                        if ((symbol == 0 && stride >= 5) || stride >= 7)
                        {
                            for (int k = 0; k < stride; ++k)
                                goodForRle[i - k - 1] = true;
                        }
                        stride = 1;
                        if (i != length) symbol = counts[i];
                    }
                    else
                    {
                        ++stride;
                    }
                }

                const int streakLimit = 1240;
                stride = 0;
                long limit = 256L * (counts[0] + counts[1] + counts[2]) / 3 + 420;
                long sum = 0;
                for (int i = 0; i < length + 1; ++i)
                {
                    if (i == length || goodForRle[i] || (i != 0 && goodForRle[i - 1])
                        || Math.Abs((256L * counts[i]) - limit) >= streakLimit)
                    {
                        if (stride >= 4)
                        {

                            int count = (int)((sum + stride / 2) / stride);
                            if (count < 1 && sum != 0) count = 1;
                            for (int k = 0; k < stride; ++k)
                            {

                                counts[i - k - 1] = count;
                            }
                        }
                        stride = 0;
                        sum = 0;
                        if (i < length - 2)
                            limit = 256L * (counts[i] + counts[i + 1] + counts[i + 2]) / 3 + 420;
                        else
                            limit = i < length ? 256L * counts[i] : 0;
                    }
                    ++stride;
                    if (i != length)
                    {
                        sum += counts[i];
                        if (stride >= 4)
                            limit = (256L * sum + stride / 2) / stride;
                        if (stride == 4)
                            limit += 120;
                    }
                }
            }

            private long CalculateBlockSymbolSize(int[] ll_lengths, int[] d_lengths, int start, int end)
            {
                long blockSymbolSize = ll_lengths[256];

                for (int i = start; i < end; i++)
                {
                    if (_distances[i] == 0)
                    {
                        blockSymbolSize += ll_lengths[_literalLengths[i]];
                    }
                    else
                    {
                        var dist = _distances[i];

                        blockSymbolSize +=
                            ll_lengths[_lengthSymbolTable[_literalLengths[i]]] +
                            _lengthExtraBitsTable[_literalLengths[i]] +
                            d_lengths[GetDistSymbol(dist)] +
                            GetDistExtraBits(dist);
                    }
                }

                return blockSymbolSize;
            }

            private static void CalculateBitLengths(int[] count, int n, int maxBits, ref int[] bitLengths)
            {
                Node.LengthLimitedCodeLengths(count, n, maxBits, ref bitLengths);
            }

            private void CalculateLZ77Counts(int start, int end, ref int[] ll_count, ref int[] d_count)
            {
                if (_prefixShift != 0)
                {
                    int shift = _prefixShift;
                    int es = end >> shift;
                    int ss = start >> shift;
                    int eb = es * 288, sb = ss * 288;
                    for (int k = 0; k < 288; k++)
                        ll_count[k] += _llPrefix[eb + k] - _llPrefix[sb + k];
                    int ebd = es * 32, sbd = ss * 32;
                    for (int k = 0; k < 32; k++)
                        d_count[k] += _dPrefix[ebd + k] - _dPrefix[sbd + k];

                    // Add the symbols past the end checkpoint, undo the ones before start.
                    for (int i = es << shift; i < end; i++)
                    {
                        if (_distances[i] == 0)
                            ll_count[_literalLengths[i]]++;
                        else
                        {
                            ll_count[_lengthSymbolTable[_literalLengths[i]]]++;
                            d_count[GetDistSymbol(_distances[i])]++;
                        }
                    }
                    for (int i = ss << shift; i < start; i++)
                    {
                        if (_distances[i] == 0)
                            ll_count[_literalLengths[i]]--;
                        else
                        {
                            ll_count[_lengthSymbolTable[_literalLengths[i]]]--;
                            d_count[GetDistSymbol(_distances[i])]--;
                        }
                    }
                }
                else
                {
                    for (int i = start; i < end; i++)
                    {
                        if (_distances[i] == 0)
                        {
                            ll_count[_literalLengths[i]]++;
                        }
                        else
                        {
                            ll_count[_lengthSymbolTable[_literalLengths[i]]]++;
                            d_count[GetDistSymbol(_distances[i])]++;
                        }
                    }
                }

                ll_count[256] = 1;
            }

            // Build the blocked prefix-sum checkpoints for the current LZ77 symbols. Checkpoint
            // k covers [0, k<<PrefixShiftBits); the tail past the last checkpoint is recovered by
            // the partial walk in CalculateLZ77Counts. Buffers are pooled per thread.
            private void BuildPrefixHistograms()
            {
                int shift = PrefixShiftBits;
                int step = 1 << shift;
                int needed = (_count >> shift) + 1;

                int llSize = needed * 288;
                int dSize = needed * 32;

                int[] llPref = _llPrefixScratch;
                if (llPref == null || llPref.Length < llSize)
                {
                    llPref = new int[llSize];
                    _llPrefixScratch = llPref;
                }
                int[] dPref = _dPrefixScratch;
                if (dPref == null || dPref.Length < dSize)
                {
                    dPref = new int[dSize];
                    _dPrefixScratch = dPref;
                }

                Array.Clear(llPref, 0, 288);
                Array.Clear(dPref, 0, 32);

                for (int k = 1; k < needed; k++)
                {
                    int baseLL = k * 288;
                    int baseD = k * 32;
                    Array.Copy(llPref, baseLL - 288, llPref, baseLL, 288);
                    Array.Copy(dPref, baseD - 32, dPref, baseD, 32);

                    int to = k * step; // <= _count because k <= (_count >> shift)
                    for (int i = (k - 1) * step; i < to; i++)
                    {
                        if (_distances[i] == 0)
                            llPref[baseLL + _literalLengths[i]]++;
                        else
                        {
                            llPref[baseLL + _lengthSymbolTable[_literalLengths[i]]]++;
                            dPref[baseD + GetDistSymbol(_distances[i])]++;
                        }
                    }
                }

                _llPrefix = llPref;
                _dPrefix = dPref;
                _prefixShift = shift;
            }

            private void ReleasePrefixHistograms()
            {
                _prefixShift = 0;
                _llPrefix = null;
                _dPrefix = null;
            }

            public List<int> BlockSplit(int maxBlocks)
            {
                var storeSize = _count;

                if (storeSize < 10)
                    return new List<int>();

                var splitFail = new List<int>(MaximumBlockSplitting);

                double splitCost1 = 0, splitCost2 = 0, origcost = 0;

                var splitPoints = new List<int>(MaximumBlockSplitting);

                var storeStart = 0;
                var storeEnd = storeSize;

                BuildPrefixHistograms();
                try
                {
                    for (int numBlocks = 1; numBlocks < maxBlocks && (storeEnd - storeStart) >= 10;)
                    {
                        var llpos = FindMinimum(this, storeStart, storeEnd, storeStart + 1, storeEnd);

                        splitCost1 = CalculateBlockSize(storeStart, llpos);
                        splitCost2 = CalculateBlockSize(llpos, storeEnd);
                        origcost = CalculateBlockSize(storeStart, storeEnd);

                        if ((splitCost1 + splitCost2) > origcost || llpos == storeStart + 1 || llpos == storeEnd)
                        {
                            splitFail.Add(storeStart);
                        }
                        else
                        {
                            splitPoints.Add(llpos);
                            splitPoints.Sort();

                            numBlocks++;
                        }

                        if (!FindLargestSplittableBlock(storeSize, splitFail, splitPoints, ref storeStart, ref storeEnd))
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    ReleasePrefixHistograms();
                }

                return splitPoints;
            }

            private static unsafe int FindMinimum(BlockStore store, int blockStart, int blockEnd, int start, int end)
            {
                int* point = stackalloc int[9];
                double* valuePoint = stackalloc double[9];
                double lastbest = double.MaxValue;

                int position = start;

                while (true)
                {
                    if (end - start <= 9)
                        break;

                    var mulFactor = (end - start) / (9 + 1);

                    for (int i = 0; i < 9; i++)
                    {
                        point[i] = start + (i + 1) * mulFactor;
                        valuePoint[i] = store.CalculateBlockSize(blockStart, point[i])
                            + store.CalculateBlockSize(point[i], blockEnd);
                    }

                    int bestIndex = 0;
                    double best = valuePoint[0];

                    for (int i = 1; i < 9; i++)
                    {
                        if (valuePoint[i] < best)
                        {
                            best = valuePoint[i];
                            bestIndex = i;
                        }
                    }

                    if (best > lastbest)
                        break;

                    start = bestIndex == 0 ? start : point[bestIndex - 1];
                    end = bestIndex == 9 - 1 ? end : point[bestIndex + 1];

                    position = point[bestIndex];
                    lastbest = best;
                }

                return position;
            }

            private static bool FindLargestSplittableBlock(int llsize, List<int> splitFail, List<int> splitPoints, ref int lstart, ref int lend)
            {
                int longest = 0;
                bool found = false;

                var npoints = splitPoints.Count;

                for (int i = 0; i <= npoints; i++)
                {
                    int start = i == 0 ? 0 : splitPoints[i - 1];
                    int end = i == npoints ? llsize - 1 : splitPoints[i];

                    if (!splitFail.Contains(start) && end - start > longest)
                    {
                        lstart = start;
                        lend = end;
                        found = true;
                        longest = end - start;
                    }
                }

                return found;
            }

            public void GetStatistics(SymbolStatistics stats)
            {
                Array.Clear(stats._litlens, 0, stats._litlens.Length);
                Array.Clear(stats._dists, 0, stats._dists.Length);

                for (int i = 0; i < Size; i++)
                {
                    if (_distances[i] == 0)
                    {
                        stats._litlens[_literalLengths[i]]++;
                    }
                    else
                    {
                        stats._litlens[_lengthSymbolTable[_literalLengths[i]]]++;
                        stats._dists[GetDistSymbol(_distances[i])]++;
                    }
                }

                stats._litlens[256] = 0;

                stats.Calculate();
            }

            public BlockStore Copy()
            {
                if (_count == 0)
                    return new BlockStore(_blockType);

                BlockStore store = new BlockStore(_blockType);
                store.CopyFrom(this);
                return store;
            }

            public void CopyFrom(BlockStore source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (source._blockType != _blockType)
                    throw new InvalidOperationException("Block types must match.");

                EnsureCapacity(source._count);
                _count = source._count;
                if (_count > 0)
                {
                    Array.Copy(source._literalLengths, _literalLengths, _count);
                    Array.Copy(source._distances, _distances, _count);
                }
                ReleasePrefixHistograms();
            }

            public void TrySetSize(int size)
            {
                EnsureCapacity(size);
            }

            // Empties the store while keeping its backing arrays for reuse across squeeze
            // passes. Safe because callers only read [0, Size) and Add overwrites in order.
            public void Reset()
            {
                _count = 0;
            }

            private sealed class BlockSizeScratch
            {
                internal readonly int[] LiteralLengthCounts = new int[288];
                internal readonly int[] DistanceCounts = new int[32];
                internal readonly int[] LiteralLengthLengths = new int[288];
                internal readonly int[] DistanceLengths = new int[32];
            }

            private sealed class DynamicTreeScratch
            {
                internal readonly int[] LldLengths = new int[318];
                internal readonly int[] Rle = new int[320];
                internal readonly int[] RleBits = new int[320];
                internal readonly int[] CodeLengthCounts = new int[19];
                internal readonly int[] CodeLengthLengths = new int[19];
                internal readonly int[] CodeLengthSymbols = new int[19];
            }

            private sealed class WriteBlockScratch
            {
                internal readonly int[] LiteralLengthLengths = new int[288];
                internal readonly int[] DistanceLengths = new int[32];
                internal readonly int[] LiteralLengthSymbols = new int[288];
                internal readonly int[] DistanceSymbols = new int[32];
            }

            private sealed class DynamicLengthsScratch
            {
                internal readonly int[] LiteralLengthCounts = new int[288];
                internal readonly int[] DistanceCounts = new int[32];
                internal readonly int[] OptimizedLiteralLengthCounts = new int[288];
                internal readonly int[] OptimizedDistanceCounts = new int[32];
                internal readonly int[] OptimizedLiteralLengthLengths = new int[288];
                internal readonly int[] OptimizedDistanceLengths = new int[32];
            }
        }

        private sealed class LongestMatchCache
        {
            private ushort[] _dist;
            private ushort[] _length;
            private byte[] _sublen;

            // One cache services every squeeze pass of a single block and is then free.
            // Its backing arrays are the largest per-block allocation (the sublen table is
            // CacheLength*blockSize*3 bytes, ~1.5 MB on the LOH for a full chunk), so they
            // are pooled per thread to keep those allocations off the gen-2 GC, which would
            // otherwise stall the parallel chunk-compression pipeline. Bit-identical output.
            [ThreadStatic]
            private static LongestMatchCache _pool;

            private LongestMatchCache() { }

            public static LongestMatchCache Rent(int blockSize)
            {
                LongestMatchCache cache = _pool ?? new LongestMatchCache();
                _pool = null;
                cache.Initialize(blockSize);
                return cache;
            }

            public static void Return(LongestMatchCache cache)
            {
                _pool = cache;
            }

            private void Initialize(int blockSize)
            {
                if (_length == null || _length.Length < blockSize)
                {
                    _length = new ushort[blockSize];
                    _dist = new ushort[blockSize];
                }
                else
                {
                    Array.Clear(_dist, 0, blockSize);
                }

                int sublenLength = CacheLength * blockSize * 3;
                if (_sublen == null || _sublen.Length < sublenLength)
                    _sublen = new byte[sublenLength];

                for (int i = 0; i < blockSize; i++)
                    _length[i] = 1;
            }

            public void StoreInLongestMatchCache(int lmcpos, int[] sublen, int distance, int length)
            {

                if (sublen != null && !(_length[lmcpos] == 0 || _dist[lmcpos] != 0))
                {
                    _dist[lmcpos] = (ushort)(length < MinimumMatch ? 0 : distance);
                    _length[lmcpos] = (ushort)(length < MinimumMatch ? 0 : length);

                    SublenToCache(sublen, lmcpos, length);
                }
            }

            public bool TryGetFromLongestMatchCache(int lmcpos, ref int limit, ref int[] sublen, out Match match)
            {
                match = default(Match);
                int length = _length[lmcpos];
                int distance = _dist[lmcpos];
                var maxCachedSublen = MaxCachedSublen(lmcpos);

                if (!((length == 0 || distance != 0) && (limit == HpiChunkZopfliEncoder.MaximumMatch || length <= limit || (sublen.Length > 0 && maxCachedSublen >= limit))))
                    return false;

                if (sublen.Length == 0 || length <= maxCachedSublen)
                {
                    length = _length[lmcpos];
                    if (length > limit)
                        length = limit;

                    if (sublen.Length > 0)
                    {
                        CacheToSublen(lmcpos, length, ref sublen);
                        distance = sublen[length];
                    }
                    match = new Match(length, distance);
                    return true;
                }

                limit = length;

                return false;
            }

            private void CacheToSublen(int pos, int length, ref int[] sublen)
            {
                if (length < 3)
                    return;

                int maxlength = MaxCachedSublen(pos);
                int prevlength = 0;

                var cachePos = (CacheLength * 3) * pos;

                for (int j = 0; j < CacheLength; j++, cachePos += 3)
                {
                    int searchLength = _sublen[cachePos] + 3;
                    int distance = (_sublen[cachePos + 1] + (_sublen[cachePos + 2] << 8));

                    for (int i = prevlength; i <= searchLength; i++)
                        sublen[i] = distance;

                    if (searchLength == maxlength)
                        break;

                    prevlength = searchLength + 1;
                }
            }

            private int MaxCachedSublen(int position)
            {
                int cachePos = (CacheLength * 3) * position;

                if (_sublen[cachePos + 1] == 0 && _sublen[cachePos + 2] == 0)
                    return 0;

                return _sublen[cachePos + ((CacheLength - 1) * 3)] + 3;
            }

            private void SublenToCache(int[] sublen, int pos, int length)
            {
                if (length < 3) return;

                int j = 0;
                int bestlength = 0;
                int cachePos = CacheLength * 3 * pos;
                int cachePos2 = cachePos;

                for (int i = 3; i <= length; i++)
                {
                    if (i == length || sublen[i] != sublen[i + 1])
                    {
                        _sublen[cachePos2++] = (byte)(i - 3);
                        _sublen[cachePos2++] = (byte)sublen[i];
                        _sublen[cachePos2++] = (byte)(sublen[i] >> 8);
                        bestlength = i;
                        j++;
                        if (j >= CacheLength) break;
                    }
                }
                if (j < CacheLength)
                {
                    _sublen[cachePos + ((CacheLength - 1) * 3)] = (byte)(bestlength - 3);
                }
            }
        }

        private struct Match
        {
            internal int _distance;

            internal int _length;

            public Match(int length, int distance)
            {
                _length = length;
                _distance = distance;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int GetDistExtraBits(int dist)
        {
            if (dist < 5) return 0;
            else if (dist < 9) return 1;
            else if (dist < 17) return 2;
            else if (dist < 33) return 3;
            else if (dist < 65) return 4;
            else if (dist < 129) return 5;
            else if (dist < 257) return 6;
            else if (dist < 513) return 7;
            else if (dist < 1025) return 8;
            else if (dist < 2049) return 9;
            else if (dist < 4097) return 10;
            else if (dist < 8193) return 11;
            else if (dist < 16385) return 12;
            else return 13;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int GetDistExtraBitsValue(int dist)
        {
            if (dist < 5) return 0;
            else if (dist < 9) return (dist - 5) & 1;
            else if (dist < 17) return (dist - 9) & 3;
            else if (dist < 33) return (dist - 17) & 7;
            else if (dist < 65) return (dist - 33) & 15;
            else if (dist < 129) return (dist - 65) & 31;
            else if (dist < 257) return (dist - 129) & 63;
            else if (dist < 513) return (dist - 257) & 127;
            else if (dist < 1025) return (dist - 513) & 255;
            else if (dist < 2049) return (dist - 1025) & 511;
            else if (dist < 4097) return (dist - 2049) & 1023;
            else if (dist < 8193) return (dist - 4097) & 2047;
            else if (dist < 16385) return (dist - 8193) & 4095;
            else return (dist - 16385) & 8191;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int GetDistSymbol(int dist)
        {
            if (dist < 193)
            {
                if (dist < 13)
                {
                    if (dist < 5) return dist - 1;
                    else if (dist < 7) return 4;
                    else if (dist < 9) return 5;
                    else return 6;
                }
                else
                {
                    if (dist < 17) return 7;
                    else if (dist < 25) return 8;
                    else if (dist < 33) return 9;
                    else if (dist < 49) return 10;
                    else if (dist < 65) return 11;
                    else if (dist < 97) return 12;
                    else if (dist < 129) return 13;
                    else return 14;
                }
            }
            else
            {
                if (dist < 2049)
                {
                    if (dist < 257) return 15;
                    else if (dist < 385) return 16;
                    else if (dist < 513) return 17;
                    else if (dist < 769) return 18;
                    else if (dist < 1025) return 19;
                    else if (dist < 1537) return 20;
                    else return 21;
                }
                else
                {
                    if (dist < 3073) return 22;
                    else if (dist < 4097) return 23;
                    else if (dist < 6145) return 24;
                    else if (dist < 8193) return 25;
                    else if (dist < 12289) return 26;
                    else if (dist < 16385) return 27;
                    else if (dist < 24577) return 28;
                    else return 29;
                }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static unsafe int GetMatch(byte* buffer, int scanPosition, int matchPosition, int searchLimit)
        {
            int scan = scanPosition;
            int match = matchPosition;

            int safeEnd = searchLimit - 8;
            while (scan <= safeEnd && *(long*)(buffer + scan) == *(long*)(buffer + match))
            {
                scan += 8; match += 8;
            }

            while (scan != searchLimit && buffer[scan] == buffer[match])
            {
                scan++; match++;
            }

            return scan;
        }

        private static Hash InitializeHash(byte[] buffer, int chunkStart, int bufferStart, int bufferEnd)
        {
            int windowStart = Math.Max(chunkStart, bufferStart - WindowSize);
            Hash hash = Hash.Rent(HpiChunkZopfliEncoder.WindowSize);
            hash.WarmUpHash(buffer, windowStart, bufferEnd);
            for (int i = windowStart; i < bufferStart; i++)
            {
                hash.UpdateHash(buffer, i, bufferEnd);
            }
            return hash;
        }

        private static readonly int[] _lengthExtraBitsTable = new int[] {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1,
                2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
                3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
                3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
                4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
                4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
                4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
                4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
                5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0
              };

        private static readonly int[] _lengthExtraBitsValueTable = new int[] {
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 2, 3, 0,
    1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5,
    6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6,
    7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
    13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2,
    3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
    10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
    29, 30, 31, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
    18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 0, 1, 2, 3, 4, 5, 6,
    7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26,
    27, 28, 29, 30, 31, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 0
  };

        private static readonly int[] _lengthSymbolTable = new int[]{
                0, 0, 0, 257, 258, 259, 260, 261, 262, 263, 264,
                265, 265, 266, 266, 267, 267, 268, 268,
                269, 269, 269, 269, 270, 270, 270, 270,
                271, 271, 271, 271, 272, 272, 272, 272,
                273, 273, 273, 273, 273, 273, 273, 273,
                274, 274, 274, 274, 274, 274, 274, 274,
                275, 275, 275, 275, 275, 275, 275, 275,
                276, 276, 276, 276, 276, 276, 276, 276,
                277, 277, 277, 277, 277, 277, 277, 277,
                277, 277, 277, 277, 277, 277, 277, 277,
                278, 278, 278, 278, 278, 278, 278, 278,
                278, 278, 278, 278, 278, 278, 278, 278,
                279, 279, 279, 279, 279, 279, 279, 279,
                279, 279, 279, 279, 279, 279, 279, 279,
                280, 280, 280, 280, 280, 280, 280, 280,
                280, 280, 280, 280, 280, 280, 280, 280,
                281, 281, 281, 281, 281, 281, 281, 281,
                281, 281, 281, 281, 281, 281, 281, 281,
                281, 281, 281, 281, 281, 281, 281, 281,
                281, 281, 281, 281, 281, 281, 281, 281,
                282, 282, 282, 282, 282, 282, 282, 282,
                282, 282, 282, 282, 282, 282, 282, 282,
                282, 282, 282, 282, 282, 282, 282, 282,
                282, 282, 282, 282, 282, 282, 282, 282,
                283, 283, 283, 283, 283, 283, 283, 283,
                283, 283, 283, 283, 283, 283, 283, 283,
                283, 283, 283, 283, 283, 283, 283, 283,
                283, 283, 283, 283, 283, 283, 283, 283,
                284, 284, 284, 284, 284, 284, 284, 284,
                284, 284, 284, 284, 284, 284, 284, 284,
                284, 284, 284, 284, 284, 284, 284, 284,
                284, 284, 284, 284, 284, 284, 284, 285
              };

        private sealed class Hash
        {

            internal ushort[] _hashval;

            internal ushort[] _hashval2;

            internal ushort[] _head;

            internal ushort[] _head2;

            internal ushort[] _prev;

            internal ushort[] _prev2;

            internal ushort[] _same;

            internal int _value;

            internal int _value2;

            private const int HashMask = 32767;

            private const int HashShift = 5;

            private const int HeadSize = HashMask + 1;

            [ThreadStatic]
            private static Hash _pool;

            private Hash(int windowSize)
            {
                _head = new ushort[HeadSize];
                _head2 = new ushort[HeadSize];
                _prev = new ushort[windowSize];
                _prev2 = new ushort[windowSize];
                _hashval = new ushort[windowSize];
                _hashval2 = new ushort[windowSize];
                _same = new ushort[windowSize];
            }

            public static Hash Rent(int windowSize)
            {
                Hash hash = _pool;
                _pool = null;
                if (hash != null && hash._prev.Length == windowSize)
                {
                    hash.Reset();
                    return hash;
                }

                Hash fresh = new Hash(windowSize);
                fresh.Reset();
                return fresh;
            }

            public static void Return(Hash hash)
            {
                _pool = hash;
            }

            private void Reset()
            {
                _value = 0;
                _value2 = 0;

                Array.Clear(_head, 0, _head.Length);
                Array.Clear(_head2, 0, _head2.Length);
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int GetHashHead(int index, bool useFirstHash)
            {
                return DecodeHead(useFirstHash ? _head[index] : _head2[index]);
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int GetHashPrev(int index, bool useFirstHash)
            {
                return useFirstHash ? _prev[index] : _prev2[index];
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int GetHashValue(bool useFirstHash)
            {
                return useFirstHash ? _value : _value2;
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int GetHashValue(int index, bool useFirstHash)
            {
                return useFirstHash ? _hashval[index] : _hashval2[index];
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int GetSameValue(int index)
            {
                return _same[index];
            }

            public void UpdateHash(byte[] buffer, int position, int end)
            {

                unchecked
                {
                    int hashPosition = position & HpiChunkZopfliEncoder.WindowMask;
                    int amount = 0;

                    byte hashValue = (position + HpiChunkZopfliEncoder.MinimumMatch <= (end)) ? buffer[position + HpiChunkZopfliEncoder.MinimumMatch - 1] : byte.MinValue;

                    UpdateHashValue(hashValue);

                    _hashval[hashPosition] = (ushort)_value;

                    int head = DecodeHead(_head[_value]);
                    if (head != -1 && _hashval[head] == _value)
                        _prev[hashPosition] = (ushort)head;

                    else
                        _prev[hashPosition] = (ushort)hashPosition;

                    _head[_value] = EncodeHead(hashPosition);

                    if (_same[(position - 1) & HpiChunkZopfliEncoder.WindowMask] > 1)
                        amount = _same[(position - 1) & HpiChunkZopfliEncoder.WindowMask] - 1;

                    while (position + amount + 1 < end &&
                        buffer[position] == buffer[position + amount + 1] && amount < ushort.MaxValue)
                    {
                        amount++;
                    }

                    _same[hashPosition] = (ushort)amount;

                    _value2 = ((_same[hashPosition] - HpiChunkZopfliEncoder.MinimumMatch) & 0xff) ^ _value;
                    _hashval2[hashPosition] = (ushort)_value2;

                    int head2 = DecodeHead(_head2[_value2]);
                    if (head2 != -1 && _hashval2[head2] == _value2)
                        _prev2[hashPosition] = (ushort)head2;

                    else
                        _prev2[hashPosition] = (ushort)hashPosition;

                    _head2[_value2] = EncodeHead(hashPosition);
                }
            }

            public void WarmUpHash(byte[] buffer, int index, int end)
            {
                _same[(index - 1) & HpiChunkZopfliEncoder.WindowMask] = 0;
                UpdateHashValue(index < end ? buffer[index] : byte.MinValue);
                UpdateHashValue(index + 1 < end ? buffer[index + 1] : byte.MinValue);
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void UpdateHashValue(byte value)
            {
                _value = (((_value) << HashShift) ^ (value)) & HashMask;
            }

            private static int DecodeHead(ushort value)
            {
                return value == 0 ? -1 : value - 1;
            }

            private static ushort EncodeHead(int value)
            {
                return (ushort)(value + 1);
            }
        }

        private sealed class BlockState
        {

            public BlockState(HpiChunkZopfliEncoder engine)
            {
                _engine = engine;
                _cache = null;
                _blockStart = _blockEnd = 0;
            }

            internal int _blockEnd;

            internal int _blockStart;

            internal LongestMatchCache _cache;

            private readonly HpiChunkZopfliEncoder _engine;

            [ThreadStatic]
            private static double[] _costsScratch;

            [ThreadStatic]
            private static ushort[] _lengthArrayScratch;

            [ThreadStatic]
            private static int[] _sublenScratch;

            [ThreadStatic]
            private static ushort[] _pathScratch;

            private static int TraceBackwards(ushort[] lengthArray, int size, out ushort[] path)
            {
                path = _pathScratch;
                int required = Math.Max(0, size - 1);
                if (path == null || path.Length < required)
                {
                    path = new ushort[required];
                    _pathScratch = path;
                }

                if (size <= 1)
                    return 0;

                int count = 0;
                for (int index = size - 1; index > 0; index -= lengthArray[index])
                    path[count++] = (ushort)lengthArray[index];

                Array.Reverse(path, 0, count);
                return count;
            }

            public BlockStore FindStandardBlock(byte[] buffer)
            {
                return FindStandardBlockLazyMatching(buffer);
            }

            private static int GetLengthScore(int length, int distance)
            {
                return (length == 3 && distance > 1024) || (length == 4 && distance > 2048) ||
                       (length == 5 && distance > 4096) ? length - 1 : length;
            }

            private BlockStore FindStandardBlockLazyMatching(byte[] buffer)
            {

                var bufferStart = _blockStart;
                var bufferEnd = _blockEnd;

                int[] dummySubLen = GetSublenScratch();

                int previousLength = 0;
                int previousMatch = 0;
                bool availableMatch = false;

                var hash = InitializeHash(buffer, _engine._chunkStart, bufferStart, bufferEnd);

                var store = new BlockStore(ChunkBlockType.Dynamic);

                for (int i = bufferStart; i < bufferEnd; i++)
                {
                    hash.UpdateHash(buffer, i, bufferEnd);

                    int limit = MaximumMatch;

                    var match = FindLongestMatch(hash, buffer, i, bufferEnd, ref limit, ref dummySubLen);

                    var length = match._length;
                    var distance = match._distance;

                    int lengthScore = GetLengthScore(length, distance);

                    int previousLengthScore = GetLengthScore(previousLength, previousMatch);

                    if (availableMatch)
                    {
                        availableMatch = false;
                        if (lengthScore > previousLengthScore + 1)
                        {
                            store.Add(buffer[i - 1], 0);
                            if (lengthScore >= MinimumMatch && length < MaximumMatch)
                            {
                                availableMatch = true;
                                previousLength = length;
                                previousMatch = distance;
                                continue;
                            }
                        }
                        else
                        {

                            length = previousLength;
                            distance = previousMatch;

                            store.Add(length, distance);
                            for (int j = 2; j < length; j++)
                            {
                                i++;
                                hash.UpdateHash(buffer, i, bufferEnd);
                            }
                            continue;
                        }
                    }
                    else if (lengthScore >= MinimumMatch && length < MaximumMatch)
                    {
                        availableMatch = true;
                        previousLength = length;
                        previousMatch = distance;
                        continue;
                    }

                    if (lengthScore >= MinimumMatch)
                    {
                        store.Add(length, distance);
                    }
                    else
                    {
                        length = 1;
                        store.Add(buffer[i], 0);
                    }

                    for (int j = 1; j < length; j++)
                    {
                        i++;
                        hash.UpdateHash(buffer, i, bufferEnd);
                    }
                }

                Hash.Return(hash);
                return store;
            }

            public BlockStore FindOptimalBlock(byte[] buffer)
            {
                BlockStore bestStore = null;
                var stats = new SymbolStatistics();
                var nextStats = new SymbolStatistics();
                var bestStats = new SymbolStatistics();
                bool hasBestStats = false;
                double bestCost = double.MaxValue;
                double lastCost = 0;

                var random = new RanState();
                var randomizeStarted = false;

                var currentStore = FindStandardBlock(buffer);
                currentStore.GetStatistics(stats);

                int sinceImprovement = 0;

                // Reuse both stores across squeeze passes. A chunk can improve several times;
                // retaining the best store's backing arrays avoids repeated large allocations.
                BlockStore squeezeScratch = null;

                for (int i = 0; i < NumberOfIterations; i++)
                {
                    currentStore = OptimalRun(buffer, stats, ChunkBlockType.Dynamic, squeezeScratch);
                    squeezeScratch = currentStore;

                    var cost = currentStore.CalculateBlockSize(0, currentStore.Size);

                    if (cost < bestCost)
                    {
                        // Sub-byte wins are kept but do not reset the early-exit streak.
                        if (cost <= bestCost - MeaningfulImprovementBits)
                            sinceImprovement = 0;
                        else
                            sinceImprovement++;

                        if (bestStore == null)
                            bestStore = new BlockStore(ChunkBlockType.Dynamic);
                        bestStore.CopyFrom(currentStore);
                        bestStats.CopyFrom(stats);
                        hasBestStats = true;
                        bestCost = cost;
                    }
                    else
                    {
                        sinceImprovement++;
                    }

                    if (i + 1 >= MinIterationsBeforeEarlyExit
                        && sinceImprovement >= MaxIterationsWithoutImprovement)
                        break;

                    currentStore.GetStatistics(nextStats);

                    if (randomizeStarted)
                        nextStats.CalculateWeighted(stats, 1.0, 0.5);

                    if (i > 5 && cost == lastCost)
                    {
                        if (hasBestStats)
                            nextStats.CopyFrom(bestStats);
                        nextStats.CalculateRandomized(random);
                        randomizeStarted = true;
                    }

                    lastCost = cost;
                    SymbolStatistics previousStats = stats;
                    stats = nextStats;
                    nextStats = previousStats;
                }

                return bestStore;
            }

            public BlockStore FindOptimalFixedBlock(byte[] buffer)
            {
                return OptimalRun(buffer, null, ChunkBlockType.Fixed);
            }

            private BlockStore OptimalRun(byte[] buffer, SymbolStatistics stats, ChunkBlockType blockType, BlockStore reuse = null)
            {
                var lengthArray = GetBestLengths(buffer, stats, blockType);

                int size = (_blockStart == _blockEnd) ? 0 : (_blockEnd - _blockStart) + 1;
                ushort[] path;
                int pathCount = BlockState.TraceBackwards(lengthArray, size, out path);

                return FollowPath(buffer, path, pathCount, blockType, reuse);
            }

            private BlockStore FollowPath(byte[] buffer, ushort[] path, int pathCount, ChunkBlockType blockType, BlockStore reuse = null)
            {
                int bufferStart = _blockStart;
                int bufferEnd = _blockEnd;

                if (bufferStart == bufferEnd)
                    return null;

                int[] dummySubLen = Array.Empty<int>();

                var hash = InitializeHash(buffer, _engine._chunkStart, bufferStart, bufferEnd);

                // Reuse the caller's scratch store across squeeze passes to avoid reallocating
                // the (up to ~256 KB) symbol arrays every iteration; otherwise allocate.
                BlockStore store = reuse != null && reuse.BlockType == blockType ? reuse : new BlockStore(blockType);
                store.Reset();

                store.TrySetSize(pathCount);

                var position = bufferStart;
                for (int i = 0; i < pathCount; i++)
                {
                    int length = path[i];
                    hash.UpdateHash(buffer, position, bufferEnd);

                    if (length >= MinimumMatch)
                    {

                        var distance = FindLongestMatch(hash, buffer, position, bufferEnd, ref length, ref dummySubLen)._distance;
                        store.Add(length, distance);
                    }
                    else
                    {
                        length = 1;
                        store.Add(buffer[position], 0);
                    }

                    for (int j = 1; j < length; j++)
                    {
                        hash.UpdateHash(buffer, position + j, bufferEnd);
                    }

                    position += length;
                }

                Hash.Return(hash);
                return store;
            }

            private ushort[] GetBestLengths(byte[] buffer, SymbolStatistics stats, ChunkBlockType blockType)
            {
                int bufferStart = _blockStart;
                int bufferEnd = _blockEnd;

                if (bufferStart == bufferEnd)
                    return Array.Empty<ushort>();

                int blockSize = bufferEnd - bufferStart;

                int[] sublen = null;
                ushort[] lengthArray = null;
                double[] costs = null;
                double mincost = 0;
                double symbolCost = 0;
                Hash hash = null;

                mincost = GetCostModelMinCost(stats, blockType);
                symbolCost = GetCost(stats, blockType, HpiChunkZopfliEncoder.MaximumMatch, 1);
                hash = InitializeHash(buffer, _engine._chunkStart, bufferStart, bufferEnd);

                sublen = GetSublenScratch();

                costs = _costsScratch;
                if (costs == null || costs.Length < blockSize + 1)
                {
                    costs = new double[blockSize + 1];
                    _costsScratch = costs;
                }

                lengthArray = _lengthArrayScratch;
                if (lengthArray == null || lengthArray.Length < blockSize + 1)
                {
                    lengthArray = new ushort[blockSize + 1];
                    _lengthArrayScratch = lengthArray;
                }

                lengthArray[0] = 0;
                costs[0] = 0d;

                for (int i = 1; i < blockSize + 1; i++)
                    costs[i] = double.MaxValue;

                for (int bufferIndex = bufferStart, lengthArrayIndex = 0; bufferIndex < bufferEnd; bufferIndex++, lengthArrayIndex++)
                {
                    hash.UpdateHash(buffer, bufferIndex, bufferEnd);

                    if (bufferIndex > bufferStart + (HpiChunkZopfliEncoder.MaximumMatch + 1)
                        && bufferIndex + ((HpiChunkZopfliEncoder.MaximumMatch * 2) + 1) < bufferEnd
                        && hash._same[bufferIndex & HpiChunkZopfliEncoder.WindowMask] > (HpiChunkZopfliEncoder.MaximumMatch * 2)
                        && hash._same[(bufferIndex - HpiChunkZopfliEncoder.MaximumMatch) & HpiChunkZopfliEncoder.WindowMask] > HpiChunkZopfliEncoder.MaximumMatch)
                    {

                        for (int k = 0; k < HpiChunkZopfliEncoder.MaximumMatch; k++)
                        {
                            costs[lengthArrayIndex + HpiChunkZopfliEncoder.MaximumMatch] = (costs[lengthArrayIndex] + symbolCost);
                            lengthArray[lengthArrayIndex + HpiChunkZopfliEncoder.MaximumMatch] = HpiChunkZopfliEncoder.MaximumMatch;
                            bufferIndex++;
                            lengthArrayIndex++;
                            hash.UpdateHash(buffer, bufferIndex, bufferEnd);
                        }
                    }

                    var limit = HpiChunkZopfliEncoder.MaximumMatch;

                    var length = FindLongestMatch(hash, buffer, bufferIndex, bufferEnd, ref limit, ref sublen)._length;

                    if (bufferIndex + 1 <= bufferEnd)
                    {
                        double newCost = costs[lengthArrayIndex]
                            + GetCost(stats, blockType, buffer[bufferIndex], 0);
                        if (newCost < costs[lengthArrayIndex + 1])
                        {
                            costs[lengthArrayIndex + 1] = newCost;
                            lengthArray[lengthArrayIndex + 1] = 1;
                        }
                    }

                    int kend = Math.Min(length, bufferEnd - bufferIndex);
                    double mincostaddcostj = mincost + costs[lengthArrayIndex];
                    for (int k = 3; k <= kend; k++)
                    {

                        if (costs[lengthArrayIndex + k] <= mincostaddcostj)
                            continue;

                        var newCost = costs[lengthArrayIndex]
                            + GetCost(stats, blockType, k, sublen[k]);

                        if (newCost < costs[lengthArrayIndex + k])
                        {
                            costs[lengthArrayIndex + k] = newCost;
                            lengthArray[lengthArrayIndex + k] = (ushort)k;
                        }
                    }
                }

                Hash.Return(hash);
                return lengthArray;
            }

            private static int[] GetSublenScratch()
            {
                return _sublenScratch ?? (_sublenScratch = new int[259]);
            }

            private static readonly int[] _costModelMinCostDistanceSymbols = new int[]
                { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257,
                    385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };

            private static double GetCostModelMinCost(SymbolStatistics stats, ChunkBlockType blockType)
            {
                int bestlength = 0;
                int bestdist = 0;

                double mincost = double.MaxValue;
                for (int i = 3; i < 259; i++)
                {
                    double cost = GetCost(stats, blockType, i, 1);
                    if (cost < mincost)
                    {
                        bestlength = i;
                        mincost = cost;
                    }
                }

                mincost = double.MaxValue;

                for (int i = 0; i < 30; i++)
                {
                    double cost = GetCost(stats, blockType, 3, _costModelMinCostDistanceSymbols[i]);
                    if (cost < mincost)
                    {
                        bestdist = _costModelMinCostDistanceSymbols[i];
                        mincost = cost;
                    }
                }

                return GetCost(stats, blockType, bestlength, bestdist);
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static double GetCost(
                SymbolStatistics stats,
                ChunkBlockType blockType,
                int literalLength,
                int distance)
            {
                if (blockType == ChunkBlockType.Fixed)
                {
                    if (distance == 0)
                        return literalLength <= 143 ? 8 : 9;

                    int lengthSymbol = _lengthSymbolTable[literalLength];
                    return (lengthSymbol <= 279 ? 7 : 8)
                        + 5
                        + _lengthExtraBitsTable[literalLength]
                        + GetDistExtraBits(distance);
                }

                if (distance == 0)
                    return stats._literalLengthSymbolLengths[literalLength];

                return stats._literalLengthSymbolLengths[_lengthSymbolTable[literalLength]]
                    + _lengthExtraBitsTable[literalLength]
                    + stats._distanceSymbolLengths[GetDistSymbol(distance)]
                    + GetDistExtraBits(distance);
            }

            private unsafe Match FindLongestMatch(Hash hash, byte[] buffer, int bufferStart, int bufferEnd, ref int limit, ref int[] sublen)
            {
                if (bufferEnd - bufferStart < MinimumMatch)
                    return new Match(0, 0);

                int bestDistance = 0;
                int bestlength = 1;
                bool useFirstHashValue = true;

                int chainCounter = MaximumChainHits;

                if (_cache != null)
                {

                    Match match;
                    if (_cache.TryGetFromLongestMatchCache(
                        bufferStart - _blockStart,
                        ref limit,
                        ref sublen,
                        out match))
                    {
                        return match;
                    }
                }

                if (bufferStart + limit > bufferEnd)
                    limit = bufferEnd - bufferStart;

                int arrayEndPos = bufferStart + limit;

                int hashPosition = bufferStart & HpiChunkZopfliEncoder.WindowMask;
                int previousHashPoint = hash.GetHashHead(hash.GetHashValue(true), true);
                int hashPoint = hash.GetHashPrev(previousHashPoint, true);

                int dist = ((hashPoint < previousHashPoint) ? previousHashPoint - hashPoint : ((HpiChunkZopfliEncoder.WindowSize - hashPoint) + previousHashPoint));

                fixed (byte* buf = buffer)
                {

                    while (dist < HpiChunkZopfliEncoder.WindowSize)
                    {
                        int currentlength = 0;

                        if (dist > 0)
                        {
                            var scanPosition = bufferStart;
                            var matchPosition = bufferStart - dist;

                            if (bufferStart + bestlength >= bufferEnd ||
                                buf[scanPosition + bestlength] == buf[matchPosition + bestlength])
                            {
                                int same0 = hash._same[bufferStart & HpiChunkZopfliEncoder.WindowMask];
                                if (same0 > 2 && buf[scanPosition] == buf[matchPosition])
                                {
                                    int same1 = hash.GetSameValue(((bufferStart - dist) & HpiChunkZopfliEncoder.WindowMask));

                                    int same = same0 < same1 ? same0 : same1;

                                    if (same > limit)
                                        same = limit;

                                    scanPosition += same;
                                    matchPosition += same;
                                }

                                scanPosition = GetMatch(buf, scanPosition, matchPosition, arrayEndPos);

                                currentlength = scanPosition - bufferStart;
                            }

                            if (currentlength > bestlength)
                            {
                                if (sublen.Length > 0)
                                {
                                    for (int j = bestlength + 1; j <= currentlength; j++)
                                    {
                                        sublen[j] = dist;
                                    }
                                }

                                bestDistance = dist;
                                bestlength = currentlength;

                                if (currentlength >= limit)
                                    break;
                            }
                        }

                        if (useFirstHashValue && bestlength >= hash.GetSameValue(hashPosition) &&
                            hash.GetHashValue(false) == hash.GetHashValue(hashPoint, false))
                        {

                            useFirstHashValue = false;
                        }

                        previousHashPoint = hashPoint;
                        hashPoint = hash.GetHashPrev(hashPoint, useFirstHashValue);

                        if (hashPoint == previousHashPoint)
                            break;

                        dist += (hashPoint < previousHashPoint ? previousHashPoint - hashPoint : ((HpiChunkZopfliEncoder.WindowSize - hashPoint) + previousHashPoint));

                        if (--chainCounter < 0)
                            break;
                    }
                }

                var distance = bestDistance;
                var length = bestlength;

                if (sublen.Length > 0 && _cache != null && limit == MaximumMatch)

                    _cache.StoreInLongestMatchCache(bufferStart - _blockStart, sublen, distance, length);

                return new Match(length, distance);
            }
        }

    }

    internal sealed class BitWriter
    {

        public BitWriter(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable", "stream");

            _stream = stream;
        }

        private const int OutputBufferSize = 4096;
        private readonly byte[] _buffer = new byte[OutputBufferSize];
        private int _bufferCount;
        private int _bitBuffer;
        private int _bitCount;
        private readonly Stream _stream;

        public void FlushBits()
        {
            if (_bitCount > 0)
            {
                WriteByte((byte)_bitBuffer);
                _bitCount = 0;
                _bitBuffer = 0;
            }

            FlushOutputBuffer();
        }

        public void Write(bool value)
        {
            if(value)
                _bitBuffer |= 1 << _bitCount;
            _bitCount++;
        }

        public void Write(int value, int numberOfBits)
        {
            PrivateWrite(value, numberOfBits);
        }

        public void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_bitCount == 0)
            {
                FlushOutputBuffer();
                _stream.Write(buffer, offset, count);
            }
            else
                for (int i = offset; i < (offset + count); i++)
                    PrivateWrite(buffer[i], 8);
        }

        public void WriteHuffman(int value, int numberOfBits)
        {
            for (int i = numberOfBits - 1; i >= 0; i--)
            {
                _bitBuffer |= ((value >> i) & 1) << _bitCount++;
            }
            WriteBuffer();
        }

        private void PrivateWrite(int value, int numberOfBits)
        {
            var mask = (1 << numberOfBits) - 1;

            _bitBuffer |= (value & mask) << _bitCount;

            _bitCount += numberOfBits;

            WriteBuffer();
        }

        private void WriteBuffer()
        {
            if (_bitCount >= 16)
            {
                WriteByte((byte)_bitBuffer);
                WriteByte((byte)(_bitBuffer >> 8));
                _bitCount -= 16;
                _bitBuffer >>= 16;
            }

            else if (_bitCount >= 8)
            {
                WriteByte((byte)_bitBuffer);
                _bitCount -= 8;
                _bitBuffer >>= 8;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteByte(byte value)
        {
            if (_bufferCount == _buffer.Length)
                FlushOutputBuffer();

            _buffer[_bufferCount++] = value;
        }

        private void FlushOutputBuffer()
        {
            if (_bufferCount == 0)
                return;

            _stream.Write(_buffer, 0, _bufferCount);
            _bufferCount = 0;
        }
    }

    internal enum ChunkBlockType
    {
        Fixed = 1,
        Dynamic = 2
    }
}
