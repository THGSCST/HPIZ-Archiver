using System;
using System.Collections.Generic;
using System.IO;

namespace CompressSharper.Zopfli
{
    /// <summary>
    /// Produces raw DEFLATE streams using iterative Zopfli LZ77 optimization.
    /// </summary>
    public sealed class ZopfliDeflater
    {
        /// <summary>
        /// Creates a compressor that writes to <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">Writable destination for the raw DEFLATE stream.</param>
        public ZopfliDeflater(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream", "stream cannot be null");

            if (!stream.CanWrite)
                throw new ArgumentException("stream must be writable", "stream");

            _writer = new BitWriter(stream);

            MasterBlockSize = 1000000;
            MaximumChainHits = 8192;
            UseCache = true;
            NumberOfIterations = 15;
            MaximumBlockSplitting = 15;
            BlockSplitting = BlockSplitting.First;
            UseLazyMatching = true;
        }

        private const int CacheLength = 8;

        private const int MaximumMatch = 258;

        private const int MaximumUncompressedBlockSize = 65535;

        private const int MinimumMatch = 3;

        private const int WindowMask = (WindowSize - 1);

        private const int WindowSize = 32768;
        private const int HuffmanNodePoolSize = 8866;

        private readonly BitWriter _writer;

        /// <summary>Gets or sets when dynamic blocks are split.</summary>
        public BlockSplitting BlockSplitting { get; set; }

        /// <summary>
        /// Gets or sets the independently optimized master-block size, or zero to disable it.
        /// </summary>
        public int MasterBlockSize { get; set; }

        /// <summary>Gets or sets the maximum number of dynamic-block split points.</summary>
        public int MaximumBlockSplitting { get; set; }

        /// <summary>Gets or sets the maximum hash-chain candidates examined per match.</summary>
        public int MaximumChainHits { get; set; }

        /// <summary>Gets or sets the number of iterative optimal-LZ77 passes.</summary>
        public int NumberOfIterations { get; set; }

        /// <summary>Gets or sets whether longest-match results are cached.</summary>
        public bool UseCache { get; set; }

        /// <summary>Gets or sets whether the initial greedy pass uses lazy matching.</summary>
        public bool UseLazyMatching { get; set; }

        /// <summary>Compresses the complete buffer as dynamic DEFLATE blocks.</summary>
        /// <param name="buffer">Non-empty input buffer.</param>
        /// <param name="finalBlock">Whether the emitted block terminates the stream.</param>
        public void Deflate(byte[] buffer, bool finalBlock)
        {
            Deflate(buffer, finalBlock, BlockType.Dynamic);
        }

        /// <summary>Compresses the complete buffer using the requested block encoding.</summary>
        /// <param name="buffer">Non-empty input buffer.</param>
        /// <param name="finalBlock">Whether the emitted block terminates the stream.</param>
        /// <param name="blockType">Block encoding to emit.</param>
        public void Deflate(byte[] buffer, bool finalBlock, BlockType blockType)
        {
            ValidateBufferRange(buffer, 0, buffer == null ? 0 : buffer.Length);
            ValidateOptions(blockType, true);

            if (MasterBlockSize == 0 && blockType != BlockType.Uncompressed)
            {
                DeflatePart(buffer, 0, buffer.Length, blockType, finalBlock);
            }
            else
            {
                int maxBlockSize = (blockType == BlockType.Uncompressed) ? MaximumUncompressedBlockSize : MasterBlockSize;

                for (int i = 0; i < buffer.Length; i += maxBlockSize)
                {
                    bool masterFinal = (i + maxBlockSize >= buffer.Length);
                    DeflatePart(buffer, i, i + (masterFinal ? buffer.Length - i : maxBlockSize), blockType, finalBlock && masterFinal);
                }
            }

            if (finalBlock)
            {
                _writer.FlushBits();
            }
        }

        /// <summary>
        /// Compresses a non-empty buffer range and uses preceding bytes as its dictionary.
        /// </summary>
        /// <param name="buffer">Input buffer containing the range and optional dictionary.</param>
        /// <param name="bufferStart">Inclusive range start.</param>
        /// <param name="bufferEnd">Exclusive range end.</param>
        /// <param name="blockType">Block encoding to emit.</param>
        /// <param name="finalBlock">Whether the emitted block terminates the stream.</param>
        public void DeflatePart(byte[] buffer, int bufferStart, int bufferEnd, BlockType blockType, bool finalBlock)
        {
            ValidateBufferRange(buffer, bufferStart, bufferEnd);
            ValidateOptions(blockType, false);

            if (blockType == BlockType.Uncompressed)
            {
                DeflateNonCompressedBlock(buffer, bufferStart, bufferEnd, finalBlock);
            }
            else if (blockType == BlockType.Fixed || BlockSplitting == BlockSplitting.None)
            {
                DeflateBlock(buffer, bufferStart, bufferEnd, blockType, finalBlock);
            }
            else if (BlockSplitting == BlockSplitting.First)
            {
                DeflateSplittingFirst(buffer, bufferStart, bufferEnd, finalBlock);
            }
            else if (BlockSplitting == BlockSplitting.Last)
            {
                DeflateSplittingLast(buffer, bufferStart, bufferEnd, finalBlock);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(BlockSplitting));
            }
        }

        private void ValidateOptions(BlockType blockType, bool validateMasterBlockSize)
        {
            if (blockType < BlockType.Uncompressed || blockType > BlockType.Dynamic)
                throw new ArgumentOutOfRangeException(nameof(blockType));

            if (blockType != BlockType.Uncompressed && MaximumChainHits < 0)
                throw new InvalidOperationException("MaximumChainHits cannot be negative.");
            if (validateMasterBlockSize && blockType != BlockType.Uncompressed && MasterBlockSize < 0)
                throw new InvalidOperationException("MasterBlockSize cannot be negative.");

            if (blockType == BlockType.Dynamic)
            {
                if (BlockSplitting < BlockSplitting.None || BlockSplitting > BlockSplitting.Last)
                    throw new ArgumentOutOfRangeException(nameof(BlockSplitting));
                if (NumberOfIterations < 1)
                    throw new InvalidOperationException("NumberOfIterations must be at least 1.");
                if (MaximumBlockSplitting < 0)
                    throw new InvalidOperationException("MaximumBlockSplitting cannot be negative.");
            }
        }

        private static void ValidateBufferRange(byte[] buffer, int bufferStart, int bufferEnd)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (bufferStart < 0 || bufferStart >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(bufferStart));
            if (bufferEnd <= bufferStart || bufferEnd > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(bufferEnd));
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

        private void DeflateSplittingLast(byte[] buffer, int bufferStart, int bufferEnd, bool finalBlock)
        {
            BlockState blockState = new BlockState(this)
            {
                _blockStart = bufferStart,
                _blockEnd = bufferEnd,
                _cache = UseCache ? new LongestMatchCache(bufferEnd - bufferStart) : null
            };

            var store = blockState.FindOptimalBlock(buffer);

            var splitPoints = store.BlockSplit(MaximumBlockSplitting);
            var splitPointCount = splitPoints.Count;

            for (int i = 0; i <= splitPointCount; i++)
            {
                var splitStart = i == 0 ? 0 : splitPoints[i - 1];
                var splitEnd = i == splitPointCount ? store.Size : splitPoints[i];

                store.WriteBlock(splitStart, splitEnd, i == splitPointCount && finalBlock, _writer);
            }
        }

        private void DeflateBlock(byte[] buffer, int bufferStart, int bufferEnd, BlockType blockType, bool finalBlock)
        {
            if (blockType == BlockType.Fixed)
            {
                DeflateFixedBlock(buffer, bufferStart, bufferEnd, finalBlock);
            }
            else
            {
                DeflateDynamicBlock(buffer, bufferStart, bufferEnd, finalBlock, false);
            }
        }

        private BlockStore DeflateDynamicBlock(byte[] buffer, int blockStart, int blockEnd, bool finalBlock, bool delayWrite)
        {
            BlockState blockState = new BlockState(this)
            {
                _blockStart = blockStart,
                _blockEnd = blockEnd,
                _cache = UseCache ? new LongestMatchCache(blockEnd - blockStart) : null
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

            if (!delayWrite)
            {
                store.WriteBlock(0, store.Size, finalBlock, _writer);
                return null;
            }

            return store;
        }

        private void DeflateFixedBlock(byte[] buffer, int bufferStart, int bufferEnd, bool finalBlock)
        {
            BlockState blockState = new BlockState(this)
            {
                _blockStart = bufferStart,
                _blockEnd = bufferEnd,
                _cache = (UseCache) ? new LongestMatchCache(bufferEnd - bufferStart) : null
            };

            var store = blockState.FindOptimalFixedBlock(buffer);

            store.WriteBlock(0, store.Size, finalBlock, _writer);
        }

        private void DeflateNonCompressedBlock(byte[] buffer, int bufferStart, int bufferEnd, bool finalBlock)
        {
            unchecked
            {

                _writer.Write(finalBlock);
                _writer.Write(0, 2);

                _writer.FlushBits();

                int count = bufferEnd - bufferStart;
                int ncount = ~count;

                _writer.Write(new byte[]
                {
                    (byte) count,  (byte) (count >> 8),
                    (byte) ncount, (byte) (ncount >> 8)
                });

                _writer.Write(buffer, bufferStart, count);
            }
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

            public static SymbolStatistics CalculateWeighted(SymbolStatistics stats1, double w1, SymbolStatistics stats2, double w2)
            {
                SymbolStatistics ret = new SymbolStatistics();

                int i;
                for (i = 0; i < 288; i++)
                    ret._litlens[i] = (int)(stats1._litlens[i] * w1 + stats2._litlens[i] * w2);

                for (i = 0; i < 32; i++)
                    ret._dists[i] = (int)(stats1._dists[i] * w1 + stats2._dists[i] * w2);

                ret._litlens[256] = 1;

                ret.Calculate();

                return ret;
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

            public SymbolStatistics Copy()
            {
                var ret = new SymbolStatistics();

                Array.Copy(_litlens, ret._litlens, _litlens.Length);
                Array.Copy(_dists, ret._dists, _dists.Length);

                Array.Copy(_literalLengthSymbolLengths, ret._literalLengthSymbolLengths, _literalLengthSymbolLengths.Length);
                Array.Copy(_distanceSymbolLengths, ret._distanceSymbolLengths, _distanceSymbolLengths.Length);

                return ret;
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
                var lists = new Node[2][] { new Node[maxBits], new Node[maxBits] };

                NodePool pool = new NodePool(HuffmanNodePoolSize);
                var leafList = new List<Node>(n);

                int numsymbols = 0;

                for (int i = 0; i < n; i++)
                {
                    if (frequencies[i] > 0)
                    {
                        leafList.Add(pool.CreateNew(frequencies[i], i, null));
                        numsymbols++;
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

                leafList.Sort((a, b) => {
                    if (a._weight != b._weight) return a._weight.CompareTo(b._weight);
                    else return a._count.CompareTo(b._count);
                });

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

            private static void BoundaryPackageMerge(ref Node[][] lists, NodePool pool, int maxbits, List<Node> leaves, int numsymbols, int index, bool final)
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

            private static void ExtractBitLengths(Node chain, List<Node> leaves, ref int[] bitlengths)
            {

                for (Node node = chain; node != null; node = node._tail)
                {
                    for (int i = 0; i < node._count; i++)
                    {
                        bitlengths[leaves[i]._count]++;
                    }
                }
            }
        }

        private sealed class BlockStore
        {
            private const int InitialCapacity = 16;

            public BlockStore(BlockType blockType)
            {
                _blockType = blockType;
            }

            internal ushort[] _distances;

            internal ushort[] _literalLengths;

            private readonly BlockType _blockType;
            private int _count;

            public int Size => _count;

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

                int[] ll_lengths = new int[288];
                int[] d_lengths = new int[32];
                int[] ll_symbols = new int[288];
                int[] d_symbols = new int[32];

                writer.Write(finalBlock);

                if (_blockType == BlockType.Fixed)
                {
                    writer.Write(true);
                    writer.Write(false);

                    GetFixedTree(ref ll_lengths, ref d_lengths);
                }
                else
                {
                    writer.Write(false);
                    writer.Write(true);

                    GetDynamicLengths(start, end, out ll_lengths, out d_lengths);
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
                int[] lld_lengths;
                int lld_total;
                List<int> rle = new List<int>(320);
                List<int> rle_bits = new List<int>(320);
                int hlit = 29;
                int hdist = 29;
                int hclen;
                int[] clcounts = new int[19];
                int[] clcl = new int[19];
                int[] clsymbols = new int[19];

                while (hlit > 0 && ll_lengths[257 + hlit - 1] == 0) hlit--;
                while (hdist > 0 && d_lengths[1 + hdist - 1] == 0) hdist--;

                lld_total = hlit + 257 + hdist + 1;
                lld_lengths = new int[lld_total];

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
                                rle.Add(18);
                                rle_bits.Add(count - 11);
                            }
                            else
                            {
                                rle.Add(17);
                                rle_bits.Add(count - 3);
                            }
                        }
                        else
                        {
                            int repeat = count - 1;

                            rle.Add(lld_lengths[i]);
                            rle_bits.Add(0);

                            while (repeat >= 6)
                            {
                                rle.Add(16);
                                rle_bits.Add(6 - 3);
                                repeat -= 6;
                            }
                            if (repeat >= 3)
                            {
                                rle.Add(16);
                                rle_bits.Add(3 - 3);

                                repeat -= 3;
                            }
                            while (repeat != 0)
                            {
                                rle.Add(lld_lengths[i]);
                                rle_bits.Add(0);
                                repeat--;
                            }
                        }

                        i += count - 1;
                    }
                    else
                    {
                        rle.Add(lld_lengths[i]);
                        rle_bits.Add(0);
                    }
                }

                for (int i = 0; i < rle.Count; i++)
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

                for (int i = 0; i < rle.Count; i++)
                {
                    int symbol = clsymbols[rle[i]];

                    if (writeOutput)
                        writer.WriteHuffman(symbol, clcl[rle[i]]);

                    bitSize += clcl[rle[i]];

                    if (rle[i] == 16)
                    {
                        if (writeOutput)
                            writer.Write(rle_bits[i], 2);

                        bitSize += 2;
                    }
                    else if (rle[i] == 17)
                    {
                        if (writeOutput)
                            writer.Write(rle_bits[i], 3);

                        bitSize += 3;
                    }
                    else if (rle[i] == 18)
                    {
                        if (writeOutput)
                            writer.Write(rle_bits[i], 7);

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

            private static void ConvertLengthsToSymbols(int[] lengths, int symbolSize, int maxbits, ref int[] symbols)
            {
                int[] blCount = new int[maxbits + 1];
                int[] nextCode = new int[(maxbits + 1)];
                int bits;

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
                int[] literalLengthCounts = new int[288];
                int[] distanceCounts = new int[32];

                int[] literalLengthLengths = new int[288];
                int[] distanceLengths = new int[32];

                if (_blockType == BlockType.Fixed)
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

            private void GetDynamicLengths(int start, int end, out int[] ll_lengths, out int[] d_lengths)
            {
                int[] ll_counts = new int[288];
                int[] d_counts = new int[32];
                CalculateLZ77Counts(start, end, ref ll_counts, ref d_counts);

                ll_lengths = new int[288];
                d_lengths = new int[32];
                CalculateBitLengths(ll_counts, 288, 15, ref ll_lengths);
                CalculateBitLengths(d_counts, 32, 15, ref d_lengths);
                PatchDistanceCodesForBuggyDecoders(d_lengths);
                long size1 = CalculateDynamicTreeSize(ll_lengths, d_lengths)
                    + CalculateBlockSymbolSizeGivenCounts(ll_counts, d_counts, ll_lengths, d_lengths);

                int[] ll_counts2 = (int[])ll_counts.Clone();
                int[] d_counts2 = (int[])d_counts.Clone();
                OptimizeHuffmanCountsForRle(32, d_counts2);
                OptimizeHuffmanCountsForRle(288, ll_counts2);

                int[] ll_lengths2 = new int[288];
                int[] d_lengths2 = new int[32];
                CalculateBitLengths(ll_counts2, 288, 15, ref ll_lengths2);
                CalculateBitLengths(d_counts2, 32, 15, ref d_lengths2);
                PatchDistanceCodesForBuggyDecoders(d_lengths2);

                long size2 = CalculateDynamicTreeSize(ll_lengths2, d_lengths2)
                    + CalculateBlockSymbolSizeGivenCounts(ll_counts, d_counts, ll_lengths2, d_lengths2);

                if (size2 < size1)
                {
                    ll_lengths = ll_lengths2;
                    d_lengths = d_lengths2;
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

                bool[] goodForRle = new bool[length];

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

                        int distSymbol;
                        long distExtraBits;

                        if (dist < 193)
                        {
                            if (dist < 13)
                            {
                                if (dist < 5) distSymbol = dist - 1;
                                else if (dist < 7) distSymbol = 4;
                                else if (dist < 9) distSymbol = 5;
                                else distSymbol = 6;
                            }
                            else
                            {
                                if (dist < 17) distSymbol = 7;
                                else if (dist < 25) distSymbol = 8;
                                else if (dist < 33) distSymbol = 9;
                                else if (dist < 49) distSymbol = 10;
                                else if (dist < 65) distSymbol = 11;
                                else if (dist < 97) distSymbol = 12;
                                else if (dist < 129) distSymbol = 13;
                                else distSymbol = 14;
                            }
                        }
                        else
                        {
                            if (dist < 2049)
                            {
                                if (dist < 257) distSymbol = 15;
                                else if (dist < 385) distSymbol = 16;
                                else if (dist < 513) distSymbol = 17;
                                else if (dist < 769) distSymbol = 18;
                                else if (dist < 1025) distSymbol = 19;
                                else if (dist < 1537) distSymbol = 20;
                                else distSymbol = 21;
                            }
                            else
                            {
                                if (dist < 3073) distSymbol = 22;
                                else if (dist < 4097) distSymbol = 23;
                                else if (dist < 6145) distSymbol = 24;
                                else if (dist < 8193) distSymbol = 25;
                                else if (dist < 12289) distSymbol = 26;
                                else if (dist < 16385) distSymbol = 27;
                                else if (dist < 24577) distSymbol = 28;
                                else distSymbol = 29;
                            }
                        }

                        if (dist < 5) distExtraBits = 0;
                        else if (dist < 9) distExtraBits = 1;
                        else if (dist < 17) distExtraBits = 2;
                        else if (dist < 33) distExtraBits = 3;
                        else if (dist < 65) distExtraBits = 4;
                        else if (dist < 129) distExtraBits = 5;
                        else if (dist < 257) distExtraBits = 6;
                        else if (dist < 513) distExtraBits = 7;
                        else if (dist < 1025) distExtraBits = 8;
                        else if (dist < 2049) distExtraBits = 9;
                        else if (dist < 4097) distExtraBits = 10;
                        else if (dist < 8193) distExtraBits = 11;
                        else if (dist < 16385) distExtraBits = 12;
                        else distExtraBits = 13;

                        blockSymbolSize +=
                            ll_lengths[_lengthSymbolTable[_literalLengths[i]]] +
                            _lengthExtraBitsTable[_literalLengths[i]] +
                            d_lengths[distSymbol] +
                            distExtraBits;
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
                for (int i = start; i < end; i++)
                {
                    if (_distances[i] == 0)
                    {
                        ll_count[_literalLengths[i]]++;
                    }
                    else
                    {
                        var dist = _distances[i];
                        int distSymbol;

                        if (dist < 193)
                        {
                            if (dist < 13)
                            {
                                if (dist < 5) distSymbol = dist - 1;
                                else if (dist < 7) distSymbol = 4;
                                else if (dist < 9) distSymbol = 5;
                                else distSymbol = 6;
                            }
                            else
                            {
                                if (dist < 17) distSymbol = 7;
                                else if (dist < 25) distSymbol = 8;
                                else if (dist < 33) distSymbol = 9;
                                else if (dist < 49) distSymbol = 10;
                                else if (dist < 65) distSymbol = 11;
                                else if (dist < 97) distSymbol = 12;
                                else if (dist < 129) distSymbol = 13;
                                else distSymbol = 14;
                            }
                        }
                        else
                        {
                            if (dist < 2049)
                            {
                                if (dist < 257) distSymbol = 15;
                                else if (dist < 385) distSymbol = 16;
                                else if (dist < 513) distSymbol = 17;
                                else if (dist < 769) distSymbol = 18;
                                else if (dist < 1025) distSymbol = 19;
                                else if (dist < 1537) distSymbol = 20;
                                else distSymbol = 21;
                            }
                            else
                            {
                                if (dist < 3073) distSymbol = 22;
                                else if (dist < 4097) distSymbol = 23;
                                else if (dist < 6145) distSymbol = 24;
                                else if (dist < 8193) distSymbol = 25;
                                else if (dist < 12289) distSymbol = 26;
                                else if (dist < 16385) distSymbol = 27;
                                else if (dist < 24577) distSymbol = 28;
                                else distSymbol = 29;
                            }
                        }

                        ll_count[_lengthSymbolTable[_literalLengths[i]]]++;
                        d_count[distSymbol]++;
                    }
                }

                ll_count[256] = 1;
            }

            public List<int> BlockSplit(int maxBlocks)
            {
                var storeSize = _count;

                if (storeSize < 10)
                    return new List<int>();

                var splitFail = new Dictionary<int, bool>();

                double splitCost1 = 0, splitCost2 = 0, origcost = 0;

                var splitPoints = new List<int>();

                var storeStart = 0;
                var storeEnd = storeSize;

                for (int numBlocks = 1; numBlocks < maxBlocks && (storeEnd - storeStart) >= 10;)
                {
                    var llpos = FindMinimum((index) =>
                    {
                        double ec1 = 0;
                        double ec2 = 0;

                            ec1 = CalculateBlockSize(storeStart, index);
                            ec2 = CalculateBlockSize(index, storeEnd);

                        return ec1 + ec2;
                    }, storeStart + 1, storeEnd);

                    splitCost1 = CalculateBlockSize(storeStart, llpos);
                    splitCost2 = CalculateBlockSize(llpos, storeEnd);
                    origcost = CalculateBlockSize(storeStart, storeEnd);

                    if ((splitCost1 + splitCost2) > origcost || llpos == storeStart + 1 || llpos == storeEnd)
                    {
                        splitFail[storeStart] = true;
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

                return splitPoints;
            }

            private static int FindMinimum(Func<int, double> function, int start, int end)
            {

                var point = new int[9];
                var valuePoint = new double[9];
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
                        valuePoint[i] = function(point[i]);
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

            private static bool FindLargestSplittableBlock(int llsize, Dictionary<int, bool> splitFail, List<int> splitPoints, ref int lstart, ref int lend)
            {
                int longest = 0;
                bool found = false;

                var npoints = splitPoints.Count;

                for (int i = 0; i <= npoints; i++)
                {
                    int start = i == 0 ? 0 : splitPoints[i - 1];
                    int end = i == npoints ? llsize - 1 : splitPoints[i];

                    if (!splitFail.ContainsKey(start) && end - start > longest)
                    {
                        lstart = start;
                        lend = end;
                        found = true;
                        longest = end - start;
                    }
                }

                return found;
            }

            public SymbolStatistics GetStatistics()
            {
                SymbolStatistics stats = new SymbolStatistics();

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

                return stats;
            }

            public BlockStore Copy()
            {
                if (_count == 0)
                    return new BlockStore(_blockType);

                BlockStore store = new BlockStore(_blockType);
                store._literalLengths = new ushort[_count];
                store._distances = new ushort[_count];
                store._count = _count;
                Array.Copy(_literalLengths, store._literalLengths, _count);
                Array.Copy(_distances, store._distances, _count);
                return store;
            }

            public void TrySetSize(int size)
            {
                EnsureCapacity(size);
            }
        }

        private sealed class LongestMatchCache
        {
            private int[] _dist;
            private int[] _length;
            private int[] _sublen;

            public LongestMatchCache(int blockSize)
            {
                _length = new int[blockSize];
                _dist = new int[blockSize];
                _sublen = new int[CacheLength * blockSize * 3];

                for (int i = 0; i < blockSize; i++)
                    _length[i] = 1;
            }

            public void StoreInLongestMatchCache(int lmcpos, int[] sublen, int distance, int length)
            {

                if (sublen != null && !(_length[lmcpos] == 0 || _dist[lmcpos] != 0))
                {
                    _dist[lmcpos] = length < MinimumMatch ? 0 : distance;
                    _length[lmcpos] = length < MinimumMatch ? 0 : length;

                    SublenToCache(sublen, lmcpos, length);
                }
            }

            public Match? TryGetFromLongestMatchCache(int lmcpos, ref int limit, ref int[] sublen)
            {
                var length = _length[lmcpos];
                var distance = _dist[lmcpos];
                var maxCachedSublen = MaxCachedSublen(lmcpos);

                if (!((length == 0 || distance != 0) && (limit == ZopfliDeflater.MaximumMatch || length <= limit || (sublen.Length > 0 && maxCachedSublen >= limit))))
                    return null;

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
                    return new Match(length, distance);
                }

                limit = length;

                return null;
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
                        _sublen[cachePos2++] = i - 3;
                        _sublen[cachePos2++] = sublen[i] & 0xFF;
                        _sublen[cachePos2++] = sublen[i] >> 8;
                        bestlength = i;
                        j++;
                        if (j >= CacheLength) break;
                    }
                }
                if (j < CacheLength)
                {
                    _sublen[cachePos + ((CacheLength - 1) * 3)] = bestlength - 3;
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

        private static Hash InitializeHash(byte[] buffer, int bufferStart, int bufferEnd)
        {
            int windowStart = bufferStart > ZopfliDeflater.WindowSize ? bufferStart - ZopfliDeflater.WindowSize : 0;
            Hash hash = Hash.Rent(ZopfliDeflater.WindowSize);
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

            internal int[] _hashval;

            internal int[] _hashval2;

            internal int[] _head;

            internal int[] _head2;

            internal int[] _prev;

            internal int[] _prev2;

            internal int[] _same;

            internal int _value;

            internal int _value2;

            private const int HashMask = 32767;

            private const int HashShift = 5;

            private const int HeadSize = 65536;

            [ThreadStatic]
            private static Hash _pool;

            private Hash(int windowSize)
            {
                _head = new int[HeadSize];
                _head2 = new int[HeadSize];
                _prev = new int[windowSize];
                _prev2 = new int[windowSize];
                _hashval = new int[windowSize];
                _hashval2 = new int[windowSize];
                _same = new int[windowSize];
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

                for (int i = 0; i < HeadSize; i++)
                {
                    _head[i] = -1;
                    _head2[i] = -1;
                }

            }

            public int GetHashHead(int index, bool useFirstHash)
            {
                return useFirstHash ? _head[index] : _head2[index];
            }

            public int GetHashPrev(int index, bool useFirstHash)
            {
                return useFirstHash ? _prev[index] : _prev2[index];
            }

            public int GetHashValue(bool useFirstHash)
            {
                return useFirstHash ? _value : _value2;
            }

            public int GetHashValue(int index, bool useFirstHash)
            {
                return useFirstHash ? _hashval[index] : _hashval2[index];
            }

            public int GetSameValue(int index)
            {
                return _same[index];
            }

            public void UpdateHash(byte[] buffer, int position, int end)
            {

                unchecked
                {
                    int hashPosition = position & ZopfliDeflater.WindowMask;
                    int amount = 0;

                    byte hashValue = (position + ZopfliDeflater.MinimumMatch <= (end)) ? buffer[position + ZopfliDeflater.MinimumMatch - 1] : byte.MinValue;

                    UpdateHashValue(hashValue);

                    _hashval[hashPosition] = _value;

                    if (_head[_value] != -1 && _hashval[_head[_value]] == _value)
                        _prev[hashPosition] = _head[_value];

                    else
                        _prev[hashPosition] = hashPosition;

                    _head[_value] = hashPosition;

                    if (_same[(position - 1) & ZopfliDeflater.WindowMask] > 1)
                        amount = _same[(position - 1) & ZopfliDeflater.WindowMask] - 1;

                    while (position + amount + 1 < end &&
                        buffer[position] == buffer[position + amount + 1] && amount < ushort.MaxValue)
                    {
                        amount++;
                    }

                    _same[hashPosition] = amount;

                    _value2 = ((_same[hashPosition] - ZopfliDeflater.MinimumMatch) & 0xff) ^ _value;
                    _hashval2[hashPosition] = _value2;

                    if (_head2[_value2] != -1 && _hashval2[_head2[_value2]] == _value2)
                        _prev2[hashPosition] = _head2[_value2];

                    else
                        _prev2[hashPosition] = hashPosition;

                    _head2[_value2] = hashPosition;
                }
            }

            public void WarmUpHash(byte[] buffer, int index, int end)
            {
                _same[(index - 1) & ZopfliDeflater.WindowMask] = 0;
                UpdateHashValue(index < end ? buffer[index] : byte.MinValue);
                UpdateHashValue(index + 1 < end ? buffer[index + 1] : byte.MinValue);
            }

            private void UpdateHashValue(byte value)
            {
                _value = (((_value) << HashShift) ^ (value)) & HashMask;
            }
        }

        private sealed class BlockState
        {

            public BlockState(ZopfliDeflater deflater)
            {
                _deflater = deflater;
                _cache = null;
                _blockStart = _blockEnd = 0;
            }

            internal int _blockEnd;

            internal int _blockStart;

            internal LongestMatchCache _cache;

            private ZopfliDeflater _deflater;

            [ThreadStatic]
            private static double[] _costsScratch;

            [ThreadStatic]
            private static int[] _lengthArrayScratch;

            [ThreadStatic]
            private static int[] _sublenScratch;

            private static List<int> TraceBackwards(int[] lengthArray, int size)
            {
                List<int> pathList = new List<int>();

                if (size <= 1)
                    return pathList;

                for (int index = size - 1; index > 0; index -= lengthArray[index])
                {
                    pathList.Add(lengthArray[index]);
                }

                pathList.Reverse();
                return pathList;
            }

            public BlockStore FindStandardBlock(byte[] buffer)
            {
                if (_deflater.UseLazyMatching)
                    return FindStandardBlockLazyMatching(buffer);

                return FindStandardBlockNoLazyMatching(buffer);
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

                var hash = InitializeHash(buffer, bufferStart, bufferEnd);

                var store = new BlockStore(BlockType.Dynamic);

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

            private BlockStore FindStandardBlockNoLazyMatching(byte[] buffer)
            {

                var bufferStart = _blockStart;
                var bufferEnd = _blockEnd;

                int[] dummySubLen = GetSublenScratch();

                var hash = InitializeHash(buffer, bufferStart, bufferEnd);

                var store = new BlockStore(BlockType.Dynamic);

                for (int i = bufferStart; i < bufferEnd; i++)
                {
                    hash.UpdateHash(buffer, i, bufferEnd);

                    int limit = MaximumMatch;

                    var match = FindLongestMatch(hash, buffer, i, bufferEnd, ref limit, ref dummySubLen);

                    var length = match._length;
                    var distance = match._distance;

                    int lengthScore = GetLengthScore(length, distance);

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
                SymbolStatistics beststats = null;
                double bestCost = double.MaxValue;
                double lastCost = 0;

                var random = new RanState();
                var randomizeStarted = false;

                var currentStore = FindStandardBlock(buffer);

                var stats = currentStore.GetStatistics();

                for (int i = 0; i < _deflater.NumberOfIterations; i++)
                {
                    currentStore = OptimalRun(buffer, stats, BlockType.Dynamic);

                    var cost = currentStore.CalculateBlockSize(0, currentStore.Size);

                    if (cost < bestCost)
                    {

                        bestStore = currentStore.Copy();
                        beststats = stats.Copy();
                        bestCost = cost;
                    }

                    SymbolStatistics lastStats = stats;
                    stats = currentStore.GetStatistics();

                    if (randomizeStarted)
                        stats = SymbolStatistics.CalculateWeighted(stats, 1.0, lastStats, 0.5);

                    if (i > 5 && cost == lastCost)
                    {
                        if (beststats != null) stats = beststats.Copy();
                        stats.CalculateRandomized(random);
                        randomizeStarted = true;
                    }

                    lastCost = cost;
                }

                return bestStore;
            }

            public BlockStore FindOptimalFixedBlock(byte[] buffer)
            {
                return OptimalRun(buffer, null, BlockType.Fixed);
            }

            private BlockStore OptimalRun(byte[] buffer, SymbolStatistics stats, BlockType blockType)
            {
                var lengthArray = GetBestLengths(buffer, stats, blockType);

                int size = (_blockStart == _blockEnd) ? 0 : (_blockEnd - _blockStart) + 1;
                var path = BlockState.TraceBackwards(lengthArray, size);

                return FollowPath(buffer, path, blockType);
            }

            private BlockStore FollowPath(byte[] buffer, List<int> path, BlockType blockType)
            {
                int bufferStart = _blockStart;
                int bufferEnd = _blockEnd;

                if (bufferStart == bufferEnd)
                    return null;

                int[] dummySubLen = Array.Empty<int>();

                var hash = InitializeHash(buffer, bufferStart, bufferEnd);

                var store = new BlockStore(blockType);

                store.TrySetSize(path.Count);

                var position = bufferStart;
                for (int i = 0; i < path.Count; i++)
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

            private int[] GetBestLengths(byte[] buffer, SymbolStatistics stats, BlockType blockType)
            {
                int bufferStart = _blockStart;
                int bufferEnd = _blockEnd;

                if (bufferStart == bufferEnd)
                    return Array.Empty<int>();

                int blockSize = bufferEnd - bufferStart;

                int[] sublen = null;
                int[] lengthArray = null;
                double[] costs = null;
                double mincost = 0;
                double symbolCost = 0;
                Hash hash = null;

                mincost = GetCostModelMinCost(stats, blockType);
                symbolCost = GetCost(stats, blockType, ZopfliDeflater.MaximumMatch, 1);
                hash = InitializeHash(buffer, bufferStart, bufferEnd);

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
                    lengthArray = new int[blockSize + 1];
                    _lengthArrayScratch = lengthArray;
                }
                else
                {
                    Array.Clear(lengthArray, 0, blockSize + 1);
                }

                lengthArray[0] = 0;
                costs[0] = 0d;

                for (int i = 1; i < blockSize + 1; i++)
                    costs[i] = double.MaxValue;

                for (int bufferIndex = bufferStart, lengthArrayIndex = 0; bufferIndex < bufferEnd; bufferIndex++, lengthArrayIndex++)
                {
                    hash.UpdateHash(buffer, bufferIndex, bufferEnd);

                    if (bufferIndex > bufferStart + (ZopfliDeflater.MaximumMatch + 1)
                        && bufferIndex + ((ZopfliDeflater.MaximumMatch * 2) + 1) < bufferEnd
                        && hash._same[bufferIndex & ZopfliDeflater.WindowMask] > (ZopfliDeflater.MaximumMatch * 2)
                        && hash._same[(bufferIndex - ZopfliDeflater.MaximumMatch) & ZopfliDeflater.WindowMask] > ZopfliDeflater.MaximumMatch)
                    {

                        for (int k = 0; k < ZopfliDeflater.MaximumMatch; k++)
                        {
                            costs[lengthArrayIndex + ZopfliDeflater.MaximumMatch] = (costs[lengthArrayIndex] + symbolCost);
                            lengthArray[lengthArrayIndex + ZopfliDeflater.MaximumMatch] = ZopfliDeflater.MaximumMatch;
                            bufferIndex++;
                            lengthArrayIndex++;
                            hash.UpdateHash(buffer, bufferIndex, bufferEnd);
                        }
                    }

                    var limit = ZopfliDeflater.MaximumMatch;

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
                            lengthArray[lengthArrayIndex + k] = k;
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

            private static double GetCostModelMinCost(SymbolStatistics stats, BlockType blockType)
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
                BlockType blockType,
                int literalLength,
                int distance)
            {
                if (blockType == BlockType.Fixed)
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

                int chainCounter = _deflater.MaximumChainHits;

                if (_cache != null)
                {

                    var match = _cache.TryGetFromLongestMatchCache(bufferStart - _blockStart, ref limit, ref sublen);

                    if (match != null)
                    {
                        return match.Value;
                    }
                }

                if (bufferStart + limit > bufferEnd)
                    limit = bufferEnd - bufferStart;

                int arrayEndPos = bufferStart + limit;

                int hashPosition = bufferStart & ZopfliDeflater.WindowMask;
                int previousHashPoint = hash.GetHashHead(hash.GetHashValue(true), true);
                int hashPoint = hash.GetHashPrev(previousHashPoint, true);

                int dist = ((hashPoint < previousHashPoint) ? previousHashPoint - hashPoint : ((ZopfliDeflater.WindowSize - hashPoint) + previousHashPoint));

                fixed (byte* buf = buffer)
                {

                    while (dist < ZopfliDeflater.WindowSize)
                    {
                        int currentlength = 0;

                        if (dist > 0)
                        {
                            var scanPosition = bufferStart;
                            var matchPosition = bufferStart - dist;

                            if (bufferStart + bestlength >= bufferEnd ||
                                buf[scanPosition + bestlength] == buf[matchPosition + bestlength])
                            {
                                int same0 = hash._same[bufferStart & ZopfliDeflater.WindowMask];
                                if (same0 > 2 && buf[scanPosition] == buf[matchPosition])
                                {
                                    int same1 = hash.GetSameValue(((bufferStart - dist) & ZopfliDeflater.WindowMask));

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

                        dist += (hashPoint < previousHashPoint ? previousHashPoint - hashPoint : ((ZopfliDeflater.WindowSize - hashPoint) + previousHashPoint));

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

        private readonly byte[] _buffer = new byte[2];
        private int _bitBuffer;
        private int _bitCount;
        private readonly Stream _stream;

        public void FlushBits()
        {
            if (_bitCount > 0)
            {
                _buffer[0] = (byte)_bitBuffer;
                _stream.Write(_buffer, 0, 1);
                _bitCount = 0;
                _bitBuffer = 0;
            }
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
                _stream.Write(buffer, offset, count);
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
                _buffer[0] = (byte)_bitBuffer;
                _buffer[1] = (byte)(_bitBuffer >> 8);
                _stream.Write(_buffer, 0, 2);
                _bitCount -= 16;
                _bitBuffer >>= 16;
            }

            else if (_bitCount >= 8)
            {
                _buffer[0] = (byte)_bitBuffer;
                _stream.Write(_buffer, 0, 1);
                _bitCount -= 8;
                _bitBuffer >>= 8;
            }
        }
    }

    /// <summary>Selects the DEFLATE block encoding.</summary>
    public enum BlockType
    {
        /// <summary>Stores bytes without compression.</summary>
        Uncompressed,

        /// <summary>Uses the RFC 1951 fixed Huffman tree.</summary>
        Fixed,

        /// <summary>Builds Huffman trees from the block's symbol frequencies.</summary>
        Dynamic
    }

    /// <summary>Selects when split points are chosen for dynamic blocks.</summary>
    public enum BlockSplitting
    {
        /// <summary>Does not split the input into multiple dynamic blocks.</summary>
        None,

        /// <summary>Chooses split points from a greedy LZ77 pass before optimization.</summary>
        First,

        /// <summary>Optimizes LZ77 before choosing split points.</summary>
        Last
    }
}
