/*
Copyright 2011 Google Inc. All Rights Reserved.
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
Author: lode.vandevenne@gmail.com (Lode Vandevenne)
Author: jyrki.alakuijala@gmail.com (Jyrki Alakuijala)
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompressSharper.Zopfli
{
    /// <summary>
    /// DEFLATE using the Zopfli algorithm
    /// </summary>
    public sealed class ZopfliDeflater
    {
        #region Constructor

        /// <summary>
        /// Constructs a new ZopfliDeflater
        /// </summary>
        /// <param name="stream">The stream to write the compressed output</param>
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

        #endregion Constructor

        #region Private Fields

        /// <summary>
        /// For longest match cache. max 256. Uses huge amounts of memory but makes it
        /// faster. Uses this many times three bytes per single byte of the input buffer.
        /// This is so because longest match finding has to find the exact distance
        /// that belongs to each length for the best lz77 strategy.
        /// Good values: e.g. 5, 8.
        /// </summary>
        private const int CacheLength = 8;

        /// <summary>
        /// Maximum length that can be encoded in deflate.
        /// </summary>
        private const int MaximumMatch = 258;

        /// <summary>
        /// The maximum size of a uncompressed block
        /// </summary>
        private const int MaximumUncompressedBlockSize = 65535;

        /// <summary>
        /// Minimum and maximum length that can be encoded in deflate.
        /// </summary>
        private const int MinimumMatch = 3;

        /// <summary>
        /// The window mask used to wrap indices into the window. This is why the
        /// window blockEnd must be a power of two.
        /// </summary>
        private const int WindowMask = (WindowSize - 1);

        /// <summary>
        /// The window blockEnd for deflate. Must be a power of two. This should be 32768, the
        /// maximum possible by the deflate spec. Anything less hurts compression more than
        /// speed.
        /// </summary>
        private const int WindowSize = 32768;

        /// <summary>
        /// The bitwriter that writes the compressed output to the stream
        /// </summary>
        private BitWriter _writer;

        #endregion Private Fields

        #region Properties

        /// <summary>
        /// Specifies how to block split the compressed blocks. BlockSplitting.First
        /// is faster than BlockSplitting.Last since it uses parallelism
        /// </summary>
        public BlockSplitting BlockSplitting { get; set; }

        /// <summary>
        /// A block structure of huge, non-smart, blocks to divide the input into, to allow
        /// operating on huge files without exceeding memory, such as the 1GB wiki9 corpus.
        /// The whole compression algorithm, including the smarter block splitting, will
        /// be executed independently on each huge block.
        /// Dividing into huge blocks hurts compression, but not much relative to the blockEnd.
        /// Set this to, for example, 20MB (20000000). Set it to 0 to disable master blocks.
        /// </summary>
        public int MasterBlockSize { get; set; }

        /// <summary>
        /// Maximum amount of blocks to split
        /// </summary>
        public int MaximumBlockSplitting { get; set; }

        /// <summary>
        /// Limit the max hash chain hits for this hash value. This has an effect only
        /// on files where the hash value is the same very often. On these files, this
        /// gives worse compression (the value should ideally be 32768, which is the
        /// WindowSize, while zlib uses 4096 even for best level), but makes it
        /// faster on some specific files.
        /// Good value: e.g. 8192.
        /// </summary>
        public int MaximumChainHits { get; set; }

        /// <summary>
        /// Maximum amount of times to rerun forward and backward pass to optimize LZ77
        /// compression cost. Good values: 10, 15 for small files, 5 for files over
        /// several MB in size or it will be too slow.
        /// </summary>
        public int NumberOfIterations { get; set; }

        /// <summary>
        /// Whether to use the longest match cache for FindLongestMatch. This cache
        /// consumes a lot of memory but speeds it up. No effect on compression blockEnd.
        /// </summary>
        public bool UseCache { get; set; }

        /// <summary>
        /// Whether to use the longest match cache for FindLongestMatch. This cache
        /// consumes a lot of memory but speeds it up. No effect on compression blockEnd.
        /// </summary>
        public bool UseLazyMatching { get; set; }

        #endregion Properties

        #region Deflate/DeflatePart

        /// <summary>
        /// Compresses according to the DEFLATE specification using BlockType.Dynamic.
        /// This function will usually output multiple deflate blocks. If final is finalBlock, then
        /// the final bit will be set on the last block.
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="finalBlock">Whether this is the last section of the input, sets the final bit to the last deflate block.</param>
        public void Deflate(byte[] buffer, bool finalBlock)
        {
            Deflate(buffer, finalBlock, BlockType.Dynamic);
        }

        /// <summary>
        /// Compresses according to the DEFLATE specification.
        /// This function will usually output multiple deflate blocks. If final is finalBlock, then
        /// the final bit will be set on the last block.
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="finalBlock">Whether this is the last section of the input, sets the final bit to the last deflate block.</param>
        /// <param name="blockType">The deflate block type (use BlockType.Dynamic for best results)</param>
        public void Deflate(byte[] buffer, bool finalBlock, BlockType blockType)
        {
            if (buffer == null || buffer.Length == 0)
            {
                throw new ArgumentNullException("buffer", "buffer must not be null or empty");
            }

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
        /// Deflate a part, to allow Deflate() to use multiple master blocks if needed.
        /// It is possible to call this function multiple times in a row, shifting
        /// instart and inend to next bytes of the buffer. If instart is larger than 0, then
        /// previous bytes are used as the initial dictionary for LZ77.
        /// This function will usually output multiple deflate blocks. If finalBlock is true, then
        /// the final bit will be set on the last block.
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="blockType">The type of block to deflate to</param>
        /// <param name="finalBlock">True if this is the final block</param>
        public void DeflatePart(byte[] buffer, int bufferStart, int bufferEnd, BlockType blockType, bool finalBlock)
        {
            if (blockType == BlockType.Uncompressed)
            {
                DeflateNonCompressedBlock(buffer, bufferStart, bufferEnd, finalBlock);
            }
            else if (blockType == BlockType.Fixed || BlockSplitting == BlockSplitting.None) //do not block split fixed blocks
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
                throw new NotImplementedException();
            }
        }

        #endregion Deflate/DeflatePart

        #region BlockSplitting

        /// <summary>
        /// Does blocksplitting on uncompressed data.
        /// The output splitpoints are indices in the uncompressed bytes.
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <returns>The split point coordinates which are indices in the buffer.</returns>
        private List<int> BlockSplit(byte[] buffer, int bufferStart, int bufferEnd)
        {
            BlockState blockState = new BlockState(this)
            {
                _blockStart = bufferStart,
                _blockEnd = bufferEnd,
                _cache = null
            };

            // Unintuitively, Using a simple LZ77 method here instead of FindOptimalBlock results in better blocks.
            var store = blockState.FindStandardBlock(buffer);

            var lz77splitpoints = store.BlockSplit(MaximumBlockSplitting);
            var nlz77points = lz77splitpoints.Count;

            /* Convert LZ77 positions to positions in the uncompressed input. */
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

        /// <summary>
        /// Does squeeze strategy where block is first split , then each block is squeezed.
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="blockType">The type of block to deflate to</param> // WHERE IS BLOCKYPE???
        /// <param name="finalBlock">True if this is the final block</param>
        private void DeflateSplittingFirst(byte[] buffer, int bufferStart, int bufferEnd, bool finalBlock)
        {
            var splitpoints = BlockSplit(buffer, bufferStart, bufferEnd);
            var stores = new BlockStore[splitpoints.Count + 1];

            Parallel.For(0, splitpoints.Count + 1, (i) =>
            {
                int splitStart = i == 0 ? bufferStart : splitpoints[i - 1];
                int splitEnd = i == splitpoints.Count ? bufferEnd : splitpoints[i];

                stores[i] = DeflateDynamicBlock(buffer, splitStart, splitEnd, i == splitpoints.Count && finalBlock, true);
            });

            for (int i = 0; i < stores.Length; i++)
            {
                stores[i].WriteBlock(0, stores[i].Size, i == stores.Length - 1 && finalBlock, _writer);
            }
        }

        /// <summary>
        /// Does squeeze strategy where first the best possible lz77 is split, and then based
        /// on that buffer, block splitting is done.
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="blockType">The type of block to deflate to</param> //Where is blocktype?
        /// <param name="finalBlock">True if this is the final block</param>
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

        #endregion BlockSplitting

        #region DeflateBlock Routines

        /// <summary>
        /// Deflates a single block
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="blockType">The type of block to deflate to</param>
        /// <param name="finalBlock">True if this is the final block</param>
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

        /// <summary>
        /// Deflates a dynamic compressed block
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="finalBlock">True if this is the final block</param>
        /// <param name="delayWrite">True to delay writing the block</param>
        /// <returns>Returns the a store representing a compressed block if delayWrite is true, otherwise null</returns>
        private BlockStore DeflateDynamicBlock(byte[] buffer, int blockStart, int blockEnd, bool finalBlock, bool delayWrite)
        {
            BlockState blockState = new BlockState(this)
            {
                _blockStart = blockStart,
                _blockEnd = blockEnd,
                _cache = UseCache ? new LongestMatchCache(blockEnd - blockStart) : null
            };

            var store = blockState.FindOptimalBlock(buffer);

            //For small block, encoding with fixed tree can be smaller. For large block,
            //don't bother doing this expensive test, dynamic tree will be better.
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

        /// <summary>
        /// Deflates a fixed block
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="finalBlock">True if this is the final block</param>
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

        /// <summary>
        /// Deflates a non compressed block
        /// </summary>
        /// <param name="buffer">The input buffer</param>
        /// <param name="bufferStart">The inclusive start of the input bytes in the buffer</param>
        /// <param name="bufferEnd">The non-inclusive end of the bytes in the buffer</param>
        /// <param name="finalBlock">True if this is the final block</param>
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

        #endregion DeflateBlock Routines

        #region SymbolStatistics

        /// <summary>
        /// Stores symbol statistics
        /// </summary>
        private class SymbolStatistics
        {
            /// <summary>
            /// length of each distance symbol in bits.
            /// </summary>
            internal double[] _distanceSymbolLengths = new double[32];

            /// <summary>
            /// The 32 unique distance symbols, not the 32768 possible _dists.
            /// </summary>
            internal int[] _dists = new int[32];

            /// <summary>
            /// length of each lit/len symbol in bits.
            /// </summary>
            internal double[] _literalLengthSymbolLengths = new double[288];

            /// <summary>
            /// The literal and length symbols
            /// </summary>
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

                ret._litlens[256] = 1;  /* End symbol. */

                ret.Calculate();

                return ret;
            }

            public void Calculate()
            {
                CalculateEntropy(_litlens, 288, ref _literalLengthSymbolLengths);
                CalculateEntropy(_dists, 32, ref _distanceSymbolLengths);
            }

            public void CalculateRandomized(Random random)
            {
                const int half = Int32.MaxValue / 2;

                for (int i = 0; i < 288; i++)
                    if (random.Next() > half)
                        _litlens[i] = _litlens[random.Next(0, 288)];

                for (int i = 0; i < 32; i++)
                    if (random.Next() > half)
                        _dists[i] = _dists[random.Next(0, 32)];

                _litlens[256] = 1;  /* End symbol. */

                Calculate();
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

            /* 1.0 / log(2.0) */
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
                    /* When the count of the symbol is 0, but its cost is requested anyway, it
                    means the symbol will appear at least once anyway, so give it the cost as if
                    its count is 1.*/
                    if (count[i] == 0)
                        bitlengths[i] = log2sum;

                    else
                        bitlengths[i] = log2sum - Math.Log(count[i]) * kInvLog2;

                    /* Depending on compiler and architecture, the above subtraction of two
                    floating point numbers may give a negative blockSymbolSize very close to zero
                    instead of zero (e.g. -5.973954e-17 with gcc 4.1.2 on Ubuntu 11.4). Clamp
                    it to zero. These floating point imprecisions do not affect the cost model
                    significantly so this is ok. */
                    if (bitlengths[i] < 0 && bitlengths[i] > -1e-5)
                        bitlengths[i] = 0;
                }
            }

            /* Adds the bit lengths. */
        }

        #endregion SymbolStatistics

        #region Node

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

            #region NodePool

            private class NodePool
            {
                static ConcurrentQueue<Node[]> _freePool = new ConcurrentQueue<Node[]>();
                private int _unusedNode;
                private Node[] _pool;

                public NodePool(int size)
                {
                    _unusedNode = 0;
                    if (! _freePool.TryDequeue(out _pool))
                    { 
                        _pool = new Node[size];
                        for (int i = 0; i < size; i++)
                            _pool[i] = new Node();
                    }
                }

                public void ReturnPool()
                {
                    _freePool.Enqueue(_pool);
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

            #endregion NodePool


            public static void LengthLimitedCodeLengths(int[] frequencies, int n, int maxBits, ref int[] bitlengths)
            {
                var lists = new Node[2][] { new Node[maxBits], new Node[maxBits] }; //Array of lists of chains. Each list requires only two lookahead chains at a time, so each list is a array of two Node*'blockState.

                var leafList = new List<Node>(n); //One leaf per symbol.

                int numsymbols = 0;  // Amount of symbols with frequency > 0.

                for (int i = 0; i < n; i++)  // Count used symbols and place them in the leafList.
                {
                    if (frequencies[i] > 0)
                    {
                        leafList.Add(new Node(frequencies[i], i, null));
                        numsymbols++;
                    }
                }

                //Check special cases and error conditions.

                if (numsymbols == 0) //No symbols at all. OK.
                    return;

                if (numsymbols == 1) //Only one symbol, give it bitlength 1, not 0. OK.
                {
                    bitlengths[leafList[0]._count] = 1;
                    return;
                }

                // Sort the leafList from lightest to heaviest.
                leafList.Sort((a, b) => {
                    if (a._weight != b._weight) return a._weight.CompareTo(b._weight);
                    else return a._count.CompareTo(b._count);
                });


                NodePool pool = new NodePool(8580); //maxbits(<=15) * 2 * numsymbols(<=286), the theoretical maximum. This needs about 170kb of memory, but is much faster than a node pool using garbage collection.

                /*Initializes each list with as lookahead chains the two leafList with lowest weights.*/
                Node node0 = pool.CreateNew(leafList[0]._weight, 1, null);
                Node node1 = pool.CreateNew(leafList[1]._weight, 2, null);

                for (int i = 0; i < maxBits; i++)
                {
                    lists[0][i] = node0;
                    lists[1][i] = node1;
                }

                /* In the last list, 2 * numsymbols - 2 active chains need to be created. Two
                are already created in the initialization. Each BoundaryPackageMerge run creates one. */
                var numBoundaryPMRuns = 2 * numsymbols - 4;
                for (int i = 0; i < numBoundaryPMRuns; i++)
                {
                    bool final = (i == numBoundaryPMRuns - 1);
                    BoundaryPackageMerge(ref lists, pool, maxBits, leafList, numsymbols, (maxBits - 1), final);
                }

                ExtractBitLengths(lists[1][maxBits - 1], leafList, ref bitlengths);

                pool.ReturnPool();
            }

            /// <summary>
            /// Performs a Boundary Package-Merge step. Puts a new chain in the given list. The
            /// new chain is, depending on the weights, a leaf or a combination of two chains
            /// from the previous list.
            /// </summary>
            /// <param name="lists">The lists of chains.</param>
            /// <param name="maxbits">Number of lists.</param>
            /// <param name="leaves">The leafList, one per symbol.</param>
            /// <param name="numsymbols">Number of leafList.</param>
            /// <param name="index">The index of the list in which a new chain or leaf is required.</param>
            /// <param name="final">Whether this is the last time this function is called. If it is then it is no more needed to recursively call self.</param>
            private static void BoundaryPackageMerge(ref Node[][] lists, NodePool pool, int maxbits, List<Node> leaves, int numsymbols, int index, bool final)
            {
                int lastcount = lists[1][index]._count;  /* Count of last chain of list. */

                if (index == 0 && lastcount >= numsymbols)
                    return;

                Node newchain = pool.CreateNew(0, 0, null); //new Node(0, 0, null);
                Node oldchain = lists[1][index];

                /* These are set up before the recursive calls below, so that there is a list
                pointing to the new node, to let the garbage collection know it'blockState in use. */
                lists[0][index] = oldchain;
                lists[1][index] = newchain;

                if (index == 0)
                {
                    /* New leaf node in list 0. */
                    newchain._weight = leaves[lastcount]._weight;
                    newchain._count = lastcount + 1;
                }
                else
                {
                    int sum = lists[0][index - 1]._weight + lists[1][index - 1]._weight;
                    if (lastcount < numsymbols && sum > leaves[lastcount]._weight)
                    {
                        /* New leaf inserted in list, so count is incremented. */
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
                            /* Two lookahead chains of previous list used up, create new ones. */
                            BoundaryPackageMerge(ref lists, pool, maxbits, leaves, numsymbols, index - 1, false);
                            BoundaryPackageMerge(ref lists, pool, maxbits, leaves, numsymbols, index - 1, false);
                        }
                    }
                }
            }

            /// <summary>
            /// Converts blockSymbolSize of boundary package-merge to the bitLengths. The blockSymbolSize in the
            /// last chain of the last list contains the amount of active leafList in each list.
            /// </summary>
            /// <param name="chain">Chain to extract the bit length from (last chain from last list).</param>
            /// <param name="leaves">The leafList</param>
            /// <param name="bitlengths">the bitLengths</param>
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

        #endregion Node

        #region BlockStore

        private sealed class BlockStore
        {
            #region Constructor

            public BlockStore(BlockType blockType)
            {
                _literalLengths = null;
                _distances = null;
                _blockType = blockType;
            }

            #endregion Constructor

            #region Fields

            /// <summary>
            /// If 0: indicates literal in corresponding litlens,
            /// if > 0: length in corresponding litlens, this is the distance.
            /// </summary>
            internal List<int> _distances;

            /// <summary>
            /// Contains the literal symbols or length values.
            /// </summary>
            internal List<int> _literalLengths;

            private BlockType _blockType;

            #endregion Fields

            #region Property

            public int Size => (_literalLengths == null) ? 0 : _literalLengths.Count;

            #endregion Property

            #region Add

            /// <summary>
            /// Appends the length and distance to the LZ77 arrays of the BlockStore.
            /// </summary>
            /// <param name="length">The length to append</param>
            /// <param name="distance">the distance to append</param>
            public void Add(int length, int distance)
            {
                if (_literalLengths == null)
                    _literalLengths = new List<int>();

                if (_distances == null)
                    _distances = new List<int>();

                _literalLengths.Add(length);
                _distances.Add(distance);
            }

            #endregion Add

            #region WriteBlock

            /// <summary>
            /// Adds a deflate block with the given LZ77 buffer to the output.
            /// </summary>
            public void WriteBlock(int start, int end, bool finalBlock, BitWriter writer)
            {

                int[] ll_counts = new int[288];
                int[] d_counts = new int[32];
                int[] ll_lengths = new int[288];
                int[] d_lengths = new int[32];
                int[] ll_symbols = new int[288];
                int[] d_symbols = new int[32];

                writer.Write(finalBlock);

                if (_blockType == BlockType.Fixed)
                {
                    writer.Write(true);
                    writer.Write(false);

                    /* Fixed block. */
                    GetFixedTree(ref ll_lengths, ref d_lengths);
                }
                else
                {
                    writer.Write(false);
                    writer.Write(true);

                    /* Dynamic block. */
                    CalculateLZ77Counts(start, end, ref ll_counts, ref d_counts);
                    CalculateBitLengths(ll_counts, 288, 15, ref ll_lengths);
                    CalculateBitLengths(d_counts, 32, 15, ref d_lengths);
                    PatchDistanceCodesForBuggyDecoders(ref d_lengths);
                    AddDynamicTree(ll_lengths, d_lengths, writer);

                }

                ConvertLengthsToSymbols(ll_lengths, 288, 15, ref ll_symbols);
                ConvertLengthsToSymbols(d_lengths, 32, 15, ref d_symbols);

                WriteBlockData(start, end, ll_symbols, ll_lengths, d_symbols, d_lengths, writer);

                /* End symbol. */

                writer.WriteHuffman(ll_symbols[256], ll_lengths[256]);
            }

            #endregion WriteBlock

            #region WriteBlockData

            /// <summary>
            /// Adds all lit/len and distance codes from the lists as huffman symbols. Does not add
            /// blockEnd code 256.
            /// </summary>
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

            #endregion WriteBlockData

            #region CalculateDynamicTreeSize

            /// <summary>
            /// Gives the exact blockEnd of the tree, in bits, as it will be encoded in DEFLATE.
            /// </summary>
            /// <param name="literalLengthLengths"></param>
            /// <param name="distanceLengths"></param>
            /// <returns></returns>
            private static int CalculateDynamicTreeSize(int[] ll_lengths, int[] d_lengths)
            {
                return AddDynamicTree(ll_lengths, d_lengths, null);
            }

            #endregion CalculateDynamicTreeSize

            #region AddDynamic Tree

            /* The order in which code length code lengths are encoded as per deflate. */
            private static readonly int[] _addDynamicTreeOrderTable = new int[] { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

            private static int AddDynamicTree(int[] ll_lengths, int[] d_lengths, BitWriter writer)
            {
                bool writeOutput = writer != null;
                int[] lld_lengths;  /* All litlen and distance lengths with ending zeros trimmed together in one array. */
                int lld_total;  /* Size of lld_lengths. */
                List<int> rle = new List<int>();  /* Runlength encoded version of lengths of litlen and distance trees. */
                List<int> rle_bits = new List<int>();  /* Extra bits for rle values 16, 17 and 18. */
                int hlit = 29; /* 286 - 257 */
                int hdist = 29;  /* 32 - 1, but gzip does not like hdist > 29.*/
                int hclen;
                int[] clcounts = new int[19];
                int[] clcl = new int[19];  /* Code length code lengths. */
                int[] clsymbols = new int[19];

                /* Trim zeros. */
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
                            int repeat = count - 1;  /* Since the first one is hardcoded. */

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
                /* Trim zeros. */
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

                    /* Extra bits. */
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

            #endregion AddDynamic Tree

            #region GetFixedTree

            private static void GetFixedTree(ref int[] ll_lengths, ref int[] d_lengths)
            {
                for (int i = 0; i < 144; i++) ll_lengths[i] = 8;
                for (int i = 144; i < 256; i++) ll_lengths[i] = 9;
                for (int i = 256; i < 280; i++) ll_lengths[i] = 7;
                for (int i = 280; i < 288; i++) ll_lengths[i] = 8;
                for (int i = 0; i < 32; i++) d_lengths[i] = 5;
            }

            #endregion GetFixedTree

            #region ConvertLengthsToSymbols

            private static void ConvertLengthsToSymbols(int[] lengths, int symbolSize, int maxbits, ref int[] symbols)
            {
                int[] blCount = new int[maxbits + 1];
                int[] nextCode = new int[(maxbits + 1)];
                int bits;

                /* 1) Count the number of codes for each code length. Let blCount[N] be the
                number of codes of length N, N >= 1. */
                for (int i = 0; i < symbolSize; i++)
                {
                    blCount[lengths[i]]++;
                }

                /* 2) Find the numerical value of the smallest code for each code length. */
                int code = 0;
                blCount[0] = 0;
                for (bits = 1; bits <= maxbits; bits++)
                {
                    code = ((code + blCount[bits - 1]) << 1);
                    nextCode[bits] = code;
                }

                /* 3) Assign numerical values to all codes, using consecutive values for all
                codes of the same length with the base values determined at step 2. */
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

            #endregion ConvertLengthsToSymbols

            #region PatchDistanceCodesForBuggyDecoders

            /// <summary>
            /// Ensures there are at least 2 distance codes to support buggy decoders.
            /// Zlib 1.2.1 and below have a bug where it fails if there isn't at least 1
            /// distance code (with length > 0), even though it'blockState valid according to the
            /// deflate spec to have 0 distance codes. On top of that, some mobile phones
            /// require at least two distance codes. To support these decoders too (but
            /// potentially at the cost of a few bytes), add dummy code lengths of 1.
            /// References to this bug can be found in the changelog of
            /// Zlib 1.2.2 and here: http://www.jonof.id.au/forum/index.php?topic=515.0.
            /// </summary>
            /// <param name="distanceLengths">the 32 lengths of the distance codes.</param>
            private static void PatchDistanceCodesForBuggyDecoders(ref int[] d_lengths)
            {
                int numberOfDistanceCodes = 1; /* Amount of non-zero distance codes */

                for (int i = 0; i < 30 /* Ignore the two unused codes from the spec */; i++)
                {
                    if (d_lengths[i] != 0) numberOfDistanceCodes++;

                    if (numberOfDistanceCodes >= 2)
                        return; /* Two or more codes is fine. */
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

            #endregion PatchDistanceCodesForBuggyDecoders

            #region CalculateBlockSize

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
                PatchDistanceCodesForBuggyDecoders(ref distanceLengths);

                return 3 + CalculateDynamicTreeSize(literalLengthLengths, distanceLengths) + CalculateBlockSymbolSize(literalLengthLengths, distanceLengths, start, end);
            }

            #endregion CalculateBlockSize

            #region CalculateBlockSymbolSize

            /// <summary>
            /// Calculates blockEnd of the part after the header and tree of an LZ77 block, in bits.
            /// </summary>
            /// <param name="literalLengthLengths"></param>
            /// <param name="distanceLengths"></param>
            /// <param name="litlens"></param>
            /// <param name="_dists"></param>
            /// <param name="blockStart"></param>
            /// <param name="blockEnd"></param>
            /// <returns></returns>
            private long CalculateBlockSymbolSize(int[] ll_lengths, int[] d_lengths, int start, int end)
            {
                long blockSymbolSize = ll_lengths[256]; /*blockEnd symbol*/

                Parallel.For(start, end, () => { return 0L; },
                    (i, loop, blockSymbolSubSize) =>
                    {
                        if (_distances[i] == 0)
                        {
                            blockSymbolSubSize += ll_lengths[_literalLengths[i]];
                        }
                        else
                        {
                            var dist = _distances[i];

                            int distSymbol;
                            long distExtraBits;

                            #region find distance symbol

                            if (dist < 193)
                            {
                                if (dist < 13)
                                {  /* distance 0..13. */
                                    if (dist < 5) distSymbol = dist - 1;
                                    else if (dist < 7) distSymbol = 4;
                                    else if (dist < 9) distSymbol = 5;
                                    else distSymbol = 6;
                                }
                                else
                                {  /* distance 13..193. */
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
                                {  /* distance 193..2049. */
                                    if (dist < 257) distSymbol = 15;
                                    else if (dist < 385) distSymbol = 16;
                                    else if (dist < 513) distSymbol = 17;
                                    else if (dist < 769) distSymbol = 18;
                                    else if (dist < 1025) distSymbol = 19;
                                    else if (dist < 1537) distSymbol = 20;
                                    else distSymbol = 21;
                                }
                                else
                                {  /* distance 2049..32768. */
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

                            #endregion find distance symbol

                            #region find extra bits

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

                            #endregion find extra bits

                            blockSymbolSubSize +=
                                ll_lengths[_lengthSymbolTable[_literalLengths[i]]] +
                                _lengthExtraBitsTable[_literalLengths[i]] +
                                d_lengths[distSymbol] +
                                distExtraBits;
                        }

                        return blockSymbolSubSize;
                    },
                        (v) => { Interlocked.Add(ref blockSymbolSize, v); }
                    );

                return blockSymbolSize;
            }

            #endregion CalculateBlockSymbolSize

            #region CalculateBitLengths


            private static void CalculateBitLengths(int[] count, int n, int maxBits, ref int[] bitLengths)
            {
                Node.LengthLimitedCodeLengths(count, n, maxBits, ref bitLengths);
            }

            #endregion CalculateBitLengths

            #region CalculateBlockCounts


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

                        #region find distance symbol

                        if (dist < 193)
                        {
                            if (dist < 13)
                            {  /* distance 0..13. */
                                if (dist < 5) distSymbol = dist - 1;
                                else if (dist < 7) distSymbol = 4;
                                else if (dist < 9) distSymbol = 5;
                                else distSymbol = 6;
                            }
                            else
                            {  /* distance 13..193. */
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
                            {  /* distance 193..2049. */
                                if (dist < 257) distSymbol = 15;
                                else if (dist < 385) distSymbol = 16;
                                else if (dist < 513) distSymbol = 17;
                                else if (dist < 769) distSymbol = 18;
                                else if (dist < 1025) distSymbol = 19;
                                else if (dist < 1537) distSymbol = 20;
                                else distSymbol = 21;
                            }
                            else
                            {  /* distance 2049..32768. */
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

                        #endregion find distance symbol

                        ll_count[_lengthSymbolTable[_literalLengths[i]]]++;
                        d_count[distSymbol]++;
                    }
                }

                ll_count[256] = 1;  /* End symbol. */
            }

            #endregion CalculateBlockCounts

            #region BlockSplit

            public List<int> BlockSplit(int maxBlocks)
            {
                var storeSize = _literalLengths.Count;

                if (storeSize < 10)
                    return new List<int>();  /* This code fails on tiny files. */

                //maintains a list of split blockStart points that will increase the cost of the block
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
                        break;  /* No further split will probably reduce compression. */
                    }
                }

                return splitPoints;
            }

            #endregion BlockSplit

            #region Find Minimum

            /// <summary>
            /// Finds minimum of function function(index) where is is of type size_t, function(index) is of type
            /// double, index is in range blockStart-blockEnd (excluding blockEnd).
            /// </summary>
            /// <param name="function"></param>
            /// <param name="blockStart"></param>
            /// <param name="blockEnd"></param>
            /// <returns></returns>
            private static int FindMinimum(Func<int, double> function, int start, int end)
            {
                /* Try to find minimum faster by recursively checking multiple points. */
                var point = new int[9];
                var valuePoint = new double[9];
                double lastbest = double.MaxValue;

                int position = start;

                while (true)
                {
                    if (end - start <= 9)
                        break;

                    var mulFactor = (end - start) / (9 + 1);

                    Parallel.For(0, 9, i =>
                    {
                        point[i] = start + (i + 1) * mulFactor;
                        valuePoint[i] = function(point[i]);
                    }); // Parallel.For
                      
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

            #endregion Find Minimum

            #region FindLargestSplittableBlock

            /// <summary>
            /// Finds next block to try to split, the largest of the available ones.
            /// The largest is chosen to make sure that if only a limited amount of blocks is
            /// requested, their sizes are spread evenly.
            /// </summary>
            /// <param name="storeSize">the blockEnd of the LL77 buffer, which is the blockEnd of the splitFail array here.</param>
            /// <param name="splitFail">array indicating which blocks starting at that blockStart are no longer
            /// splittable (splitting them increases rather than decreases cost).</param>
            /// <param name="splitPoints">the splitPoints found so far.</param>
            /// <param name="splitPointCount">the amount of splitPoints found so far.</param>
            /// <param name="blockStart">output variable, giving blockStart of block.</param>
            /// <param name="blockEnd">output variable, giving blockEnd of block</param>
            /// <returns>True if a block was found, false if no block found (all are splitFail).</returns>

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

            #endregion FindLargestSplittableBlock

            #region GetStatistics

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

            #endregion GetStatistics

            #region Copy

            public BlockStore Copy()
            {
                if (_literalLengths == null || _distances == null)
                    return new BlockStore(_blockType);

                BlockStore store = new BlockStore(_blockType);

                store._literalLengths = new List<int>(_literalLengths);
                store._distances = new List<int>(_distances);
                return store;
            }

            #endregion Copy

            #region TrySetSize

            public void TrySetSize(int size)
            {
                if (_literalLengths == null)
                {
                    _literalLengths = new List<int>(size);
                }
                if (_distances == null)
                {
                    _distances = new List<int>(size);
                }
            }

            #endregion TrySetSize
        }

        #endregion BlockStore

        #region LongestMatchCache

        /// <summary>
        /// _cache used by FindLongestMatch to remember previously found length/distance
        /// values.
        /// This is needed because the squeeze runs will ask these values multiple times for
        /// the same blockStart.
        /// Uses large amounts of memory, since it has to remember the distance belonging
        /// to every possible shorter-than-the-best length (the so called "sublen" array).
        /// </summary>
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

            /// <summary>
            /// Stores the found sublen, distance and length in the longest match cache, if possible.
            /// </summary>
            /// <param name="blockState"></param>
            /// <param name="index"></param>
            /// <param name="limit"></param>
            /// <param name="sublen"></param>
            /// <param name="distance"></param>
            /// <param name="length"></param>
            public void StoreInLongestMatchCache(int lmcpos, int[] sublen, int distance, int length)
            {
                //Length > 0 and distance 0 is invalid combination, which indicates on purpose that this cache value is not filled in yet.
                //limit == maximum match
                if (sublen != null && !(_length[lmcpos] == 0 || _dist[lmcpos] != 0))
                {
                    _dist[lmcpos] = length < MinimumMatch ? 0 : distance;
                    _length[lmcpos] = length < MinimumMatch ? 0 : length;

                    SublenToCache(sublen, lmcpos, length);
                }
            }

            /// <summary>
            /// Gets distance, length and sublen values from the cache if possible.
            /// </summary>
            /// <param name="lmcpos"></param>
            /// <param name="limit">Updates the limit value to a smaller one if possible with more limited information from the cache.</param>
            /// <param name="sublen"></param>
            /// <returns></returns>
            public Match? TryGetFromLongestMatchCache(int lmcpos, ref int limit, ref int[] sublen)
            {
                var length = _length[lmcpos];
                var distance = _dist[lmcpos];
                var maxCachedSublen = MaxCachedSublen(lmcpos);

                //Length > 0 and distance 0 is invalid combination, which indicates on purpose that this cache value is not filled in yet. //
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

                //Can't use much of the cache, since the "sublens" need to be calculated, but at  least we already know when to stop.
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

            /// <summary>
            /// Returns the length up to which could be stored in the cache.
            /// </summary>

            private int MaxCachedSublen(int position)
            {
                int cachePos = (CacheLength * 3) * position;

                if (_sublen[cachePos + 1] == 0 && _sublen[cachePos + 2] == 0)
                    return 0;  /* No sublen cached. */

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

        #endregion LongestMatchCache

        #region Match

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

        #endregion Match

        #region Static Helpers

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

        private static int GetDistSymbol(int dist)
        {
            if (dist < 193)
            {
                if (dist < 13)
                {  /* distance 0..13. */
                    if (dist < 5) return dist - 1;
                    else if (dist < 7) return 4;
                    else if (dist < 9) return 5;
                    else return 6;
                }
                else
                {  /* distance 13..193. */
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
                {  /* distance 193..2049. */
                    if (dist < 257) return 15;
                    else if (dist < 385) return 16;
                    else if (dist < 513) return 17;
                    else if (dist < 769) return 18;
                    else if (dist < 1025) return 19;
                    else if (dist < 1537) return 20;
                    else return 21;
                }
                else
                {  /* distance 2049..32768. */
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

        /// <summary>
        /// Finds how long the match of scan and match is. Can be used to find how many
        /// bytes starting from scan, and from match, are equal. Returns the last byte
        /// after scan, which is still equal to the correspondinb byte after match.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="scanPosition">scan is the blockStart to compare</param>
        /// <param name="matchPosition">match is the earlier blockStart to compare.</param>
        /// <param name="arrayEndPos">blockEnd is the last possible byte, beyond which to stop looking.</param>
        /// <returns></returns>
        private static int GetMatch(byte[] buffer, int scanPosition, int matchPosition, int searchLimit)
        {
            int searchScanPosition = scanPosition;
            int searchMatchPosition = matchPosition;

            /* The remaining few bytes. */
            while (searchScanPosition != searchLimit && buffer[searchScanPosition] == buffer[searchMatchPosition])
            {
                searchScanPosition++; searchMatchPosition++;
            }

            return searchScanPosition;
        }

        private static Hash InitializeHash(byte[] buffer, int bufferStart, int bufferEnd)
        {
            int windowStart = bufferStart > ZopfliDeflater.WindowSize ? bufferStart - ZopfliDeflater.WindowSize : 0;
            Hash hash = new Hash(ZopfliDeflater.WindowSize);
            hash.WarmUpHash(buffer, windowStart);
            for (int i = windowStart; i < bufferStart; i++)
            {
                hash.UpdateHash(buffer, i, bufferEnd);
            }
            return hash;
        }

        #region LengthBitTables

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

        private static readonly int[] _lengthExtraBitsValueTable = new int[] { //256
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

        /// <summary>
        /// symbol in range [257-285] (inclusive).
        /// </summary>
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

        #endregion LengthBitTables

        #endregion Static Helpers

        #region HashClass

        private sealed class Hash
        {
            /// <summary>
            /// Index to hash value at this index.
            /// </summary>
            internal int[] _hashval;

            /// <summary>
            /// Index to hash value at this index.
            /// </summary>
            internal int[] _hashval2;

            /// <summary>
            /// Hash value to index of its most recent occurance.
            /// </summary>
            internal int[] _head;

            /// <summary>
            /// Hash value to index of its most recent occurance.
            /// </summary>
            internal int[] _head2;

            /// <summary>
            /// Index to index of prev. occurance of same hash.
            /// </summary>
            internal int[] _prev;

            /// <summary>
            /// Index to index of prev. occurance of same hash.
            /// </summary>
            internal int[] _prev2;

            /// <summary>
            /// Amount of repetitions of same byte after this.
            /// </summary>
            internal int[] _same;

            /// <summary>
            /// Current hash value.
            /// </summary>
            internal int _value;

            /// <summary>
            /// Current hash value.
            /// </summary>
            internal int _value2;

            private const int HashMask = 32767;

            private const int HashShift = 5;

            public Hash(int windowSize)
            {
                _value = 0;
                _head = new int[65536];
                _head2 = new int[65536];
                _prev = new int[windowSize];
                _prev2 = new int[windowSize];
                _hashval = new int[windowSize];
                _hashval2 = new int[windowSize];

                for (int i = 0; i < _head.Length; i++)
                    _head[i] = -1;

                for (int j = 0; j < windowSize; j++)
                {
                    _prev[j] = j;
                    _hashval[j] = -1;
                }

                _value2 = 0;

                Array.Copy(_head, _head2, _head.Length);
                Array.Copy(_prev, _prev2, _prev.Length);
                Array.Copy(_hashval, _hashval2, _hashval.Length);

                _same = new int[windowSize]; //no need to set to zero - default value
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

                    /* Update "same". */

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

            public void WarmUpHash(byte[] buffer, int index)
            {
                UpdateHashValue(buffer[index + 0]);
                UpdateHashValue(buffer[index + 1]);
            }

            /// <summary>
            /// Update the sliding hash value with the given byte. All calls to this function
            /// must be made on consecutive input characters. Since the hash value exists out
            /// of multiple input bytes, a few warmups with this function are needed initially.
            /// </summary>
            /// <param name="value"></param>

            private void UpdateHashValue(byte value)
            {
                _value = (((_value) << HashShift) ^ (value)) & HashMask;
            }
        }

        #endregion HashClass

        #region BlockState

        private sealed class BlockState
        {
            #region Constructor

            public BlockState(ZopfliDeflater deflater)
            {
                _deflater = deflater;
                _cache = null;
                _blockStart = _blockEnd = 0;
            }

            #endregion Constructor

            #region Fields

            /// <summary>
            /// The blockEnd (not inclusive) of the current block.
            /// </summary>
            internal int _blockEnd;

            /// <summary>
            /// The blockStart (inclusive) of the current block.
            /// </summary>
            internal int _blockStart;

            /// <summary>
            /// Cache for length/distance pairs found so far.
            /// </summary>
            internal LongestMatchCache _cache;

            private ZopfliDeflater _deflater;

            #endregion Fields

            #region TraceBackwards

            /// <summary>
            /// Calculates the optimal path of lz77 lengths to use, from the calculated
            /// lengthArray. The lengthArray must contain the optimal length to reach that
            /// byte. The path will be filled with the lengths to use, so its buffer blockEnd will be
            /// the amount of lz77 symbols.
            /// </summary>

            private static List<int> TraceBackwards(int[] lengthArray)
            {
                List<int> pathList = new List<int>(lengthArray.Length);

                if (lengthArray.Length == 1)
                    return pathList;

                for (int index = lengthArray.Length - 1; index > 0; index -= lengthArray[index])
                {
                    pathList.Add(lengthArray[index]);
                }

                pathList.Reverse();
                return pathList;
            }

            #endregion TraceBackwards

            #region FindStandardBlock


            public BlockStore FindStandardBlock(byte[] buffer)
            {
                if (_deflater.UseLazyMatching)
                    return FindStandardBlockLazyMatching(buffer);

                return FindStandardBlockNoLazyMatching(buffer);
            }

            #endregion FindStandardBlock

            #region FindStandardBlockLazyMatching

            private static int GetLengthScore(int length, int distance)
            {
                return (length == 3 && distance > 1024) || (length == 4 && distance > 2048) ||
                       (length == 5 && distance > 4096) ? length - 1 : length;
            }

            private BlockStore FindStandardBlockLazyMatching(byte[] buffer)
            {
                /*
                 * Explanation the LengthScore heuristic which has been inlined
                 *
                 * Gets a score of the length given the distance. Typically, the score of the
                 * length is the length itself, but if the distance is very long, decrease the
                 * score of the length a bit to make up for the fact that long _distances use large
                 * amounts of extra bits.
                 * This is not an accurate score, it is a heuristic only for the greedy LZ77
                 * implementation. More accurate cost models are employed later. Making this
                 * heuristic more accurate may hurt rather than improve compression.
                 * The two direct uses of this heuristic are:
                 * avoid using a length of 3 in combination with a long distance. This only has
                 * an effect if length == 3.
                 * -make a slightly better choice between the two options of the lazy matching.
                 * Indirectly, this affects:
                 * -the block split points if the default of block splitting first is used, in a
                 * rather unpredictable way
                 * -the first zopfli run, so it affects the chance of the first run being closer
                 * to the optimal output
                 * At 1024, the distance uses 9+ extra bits and this seems to be the sweet spot
                 * on tested files.
                 */

                var bufferStart = _blockStart;
                var bufferEnd = _blockEnd;

                int[] dummySubLen = new int[259];

                /* Lazy matching. */
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

                    //Lazy Matching

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
                            /* Add previous to output. */
                            length = previousLength;
                            distance = previousMatch;
                            /* Add to output. */

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

                    /* Add to output. */
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

                return store;
            }

            #endregion FindStandardBlockLazyMatching

            #region FindStandardBlockNoLazyMatching

            private BlockStore FindStandardBlockNoLazyMatching(byte[] buffer)
            {
                /*
                 * Explanation the LengthScore heuristic which has been inlined
                 *
                 * Gets a score of the length given the distance. Typically, the score of the
                 * length is the length itself, but if the distance is very long, decrease the
                 * score of the length a bit to make up for the fact that long _distances use large
                 * amounts of extra bits.
                 * This is not an accurate score, it is a heuristic only for the greedy LZ77
                 * implementation. More accurate cost models are employed later. Making this
                 * heuristic more accurate may hurt rather than improve compression.
                 * The two direct uses of this heuristic are:
                 * avoid using a length of 3 in combination with a long distance. This only has
                 * an effect if length == 3.
                 * -make a slightly better choice between the two options of the lazy matching.
                 * Indirectly, this affects:
                 * -the block split points if the default of block splitting first is used, in a
                 * rather unpredictable way
                 * -the first zopfli run, so it affects the chance of the first run being closer
                 * to the optimal output
                 * At 1024, the distance uses 9+ extra bits and this seems to be the sweet spot
                 * on tested files.
                 */

                var bufferStart = _blockStart;
                var bufferEnd = _blockEnd;

                int[] dummySubLen = new int[259];

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

                    /* Add to output. */
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

                return store;
            }

            #endregion FindStandardBlockNoLazyMatching

            #region FindOptimalBlock

            public BlockStore FindOptimalBlock(byte[] buffer)
            {
                BlockStore bestStore = null;
                SymbolStatistics beststats = null;
                double bestCost = double.MaxValue;
                double lastCost = 0;

                // Try randomizing the costs a bit once the blockEnd stabilizes.
                var random = new Random();
                var randomizeStarted = false;

                //Do regular deflate, then loop multiple shortest path runs, each time using the statistics of the previous run.

                // Initial run.
                var currentStore = FindStandardBlock(buffer);

                var stats = currentStore.GetStatistics();
                var numberOfIterations = _deflater.NumberOfIterations;
                //Repeat statistics with each time the cost model from the previous stat run.
                for (int i = 0; i < numberOfIterations; i++)
                {
                    currentStore = OptimalRun(buffer,
                        (litlen, dist) =>
                        {
                            if (dist == 0)
                                return stats._literalLengthSymbolLengths[litlen];

                            #region GetDSymbol

                            int dsym;//GetDistSymbol(distance);

                            if (dist < 193)
                            {
                                if (dist < 13)
                                {  /* distance 0..13. */
                                    if (dist < 5) dsym = dist - 1;
                                    else if (dist < 7) dsym = 4;
                                    else if (dist < 9) dsym = 5;
                                    else dsym = 6;
                                }
                                else
                                {  /* distance 13..193. */
                                    if (dist < 17) dsym = 7;
                                    else if (dist < 25) dsym = 8;
                                    else if (dist < 33) dsym = 9;
                                    else if (dist < 49) dsym = 10;
                                    else if (dist < 65) dsym = 11;
                                    else if (dist < 97) dsym = 12;
                                    else if (dist < 129) dsym = 13;
                                    else dsym = 14;
                                }
                            }
                            else
                            {
                                if (dist < 2049)
                                {  /* distance 193..2049. */
                                    if (dist < 257) dsym = 15;
                                    else if (dist < 385) dsym = 16;
                                    else if (dist < 513) dsym = 17;
                                    else if (dist < 769) dsym = 18;
                                    else if (dist < 1025) dsym = 19;
                                    else if (dist < 1537) dsym = 20;
                                    else dsym = 21;
                                }
                                else
                                {  /* distance 2049..32768. */
                                    if (dist < 3073) dsym = 22;
                                    else if (dist < 4097) dsym = 23;
                                    else if (dist < 6145) dsym = 24;
                                    else if (dist < 8193) dsym = 25;
                                    else if (dist < 12289) dsym = 26;
                                    else if (dist < 16385) dsym = 27;
                                    else if (dist < 24577) dsym = 28;
                                    else dsym = 29;
                                }
                            }

                            #endregion GetDSymbol

                            #region GetDistExtra

                            int dbits;
                            if (dist < 5) dbits = 0;
                            else if (dist < 9) dbits = 1;
                            else if (dist < 17) dbits = 2;
                            else if (dist < 33) dbits = 3;
                            else if (dist < 65) dbits = 4;
                            else if (dist < 129) dbits = 5;
                            else if (dist < 257) dbits = 6;
                            else if (dist < 513) dbits = 7;
                            else if (dist < 1025) dbits = 8;
                            else if (dist < 2049) dbits = 9;
                            else if (dist < 4097) dbits = 10;
                            else if (dist < 8193) dbits = 11;
                            else if (dist < 16385) dbits = 12;
                            else dbits = 13;

                            #endregion GetDistExtra

                            return stats._literalLengthSymbolLengths[_lengthSymbolTable[litlen]] + _lengthExtraBitsTable[litlen] + stats._distanceSymbolLengths[dsym] + dbits;
                        }, BlockType.Dynamic);

                    var cost = currentStore.CalculateBlockSize(0, currentStore.Size);

                    if (cost < bestCost)
                    {
                        //Copy to the output bestStore.
                        bestStore = currentStore.Copy();
                        beststats = stats.Copy();
                        bestCost = cost;
                    }

                    SymbolStatistics lastStats = stats;
                    stats = currentStore.GetStatistics();

                    //This makes it converge slower but better. Do it only once the
                    //randomness kicks in so that if the user does few iterations, it gives a
                    //better blockSymbolSize sooner.
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

            #endregion FindOptimalBlock

            #region FindOptimalFixedBlock

            /// <summary>
            /// Does the same as FindOptimalBlock, but optimized for the fixed tree of the
            /// deflate standard.
            /// The fixed tree never gives the best compression. But this gives the best
            /// possible LZ77 encoding possible with the fixed tree.
            /// This does not create or output any fixed tree, only LZ77 buffer optimized for
            /// using with a fixed tree.
            /// If blockStart is larger than 0, it uses values before instart as starting
            /// dictionary.
            /// </summary>
            /// <param name="blockState">The blockstate</param>
            /// <param name="buffer">The input bytes</param>
            /// <param name="blockStart">The inclusive blockStart of the bytes</param>
            /// <param name="blockEnd">The non-inclusive blockEnd of the bytes</param>
            /// <param name="bestStore">The buffer bestStore</param>
            public BlockStore FindOptimalFixedBlock(byte[] buffer)
            {
                //Shortest path for fixed tree This one should give the shortest possible blockSymbolSize
                //for fixed tree, no repeated runs are needed since the tree is known.
                return OptimalRun(buffer,
                    (litlen, dist) =>
                    {
                        if (dist == 0)
                        {
                            if (litlen <= 143) return 8;
                            else return 9;
                        }
                        else
                        {
                            int dbits = GetDistExtraBits(dist);
                            int lbits = _lengthExtraBitsTable[litlen];
                            int lsym = _lengthSymbolTable[litlen];
                            int cost = 0;
                            if (lsym <= 279) cost += 7;
                            else cost += 8;
                            cost += 5;  /* Every distance symbol has length 5. */
                            return cost + dbits + lbits;
                        }
                    }, BlockType.Fixed);
            }

            #endregion FindOptimalFixedBlock

            #region OptimalRun

            /// <summary>
            /// Does a single run for FindOptimalBlock. For good compression, repeated runs
            /// with updated statistics should be performed.
            /// </summary>
            /// <param name="blockState">the blockstate</param>
            /// <param name="buffer">the input buffer bytes</param>
            /// <param name="blockStart">the inclusive blockStart</param>
            /// <param name="blockEnd">the non-inclusive blockEnd</param>
            /// <param name="path">the path</param>
            /// <param name="pathsize">the blockEnd of the path</param>
            /// <param name="lengthArray">the array to bestStore lengths</param>
            /// <param name="costModel">the cost model to use</param>
            /// <param name="costContext">the cost model context</param>
            /// <param name="bestStore">the bestStore for the LZ77 buffer</param>
            /// <returns>the cost that was, according to the costModel, needed to get to the blockEnd.
            /// This is not the actual cost.</returns>
            private BlockStore OptimalRun(byte[] buffer, Func<int, int, double> costModel, BlockType blockType)
            {
                var lengthArray = GetBestLengths(buffer, costModel);

                var path = BlockState.TraceBackwards(lengthArray);

                return FollowPath(buffer, path, blockType);
            }

            #endregion OptimalRun

            #region FollowPath

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

                    /* Add to output. */
                    if (length >= MinimumMatch)
                    {
                        /* Get the distance by recalculating longest match. The found length should match the length from the path. */
                        //Why would you do this???
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

                return store;
            }

            #endregion FollowPath

            #region GetBestLengths


            /// <summary>
            /// Performs the forward pass for "squeeze". Gets the most optimal length to reach
            /// every byte from a previous byte, using cost calculations.
            /// </summary>
            /// <param name="blockState">the BlockState</param>
            /// <param name="buffer">the input buffer array</param>
            /// <param name="blockStart">where to blockStart</param>
            /// <param name="blockEnd">where to stop (not inclusive)</param>
            /// <param name="costModel">function to calculate the cost of some lit/len/distance pair.</param>
            /// <param name="costContext">abstract context for the costModel function</param>
            /// <param name="lengthArray"></param>
            /// <returns>output array of blockEnd (inend - instart) which will receive the best length to reach this byte from a previous byte.</returns>
            private int[] GetBestLengths(byte[] buffer, Func<int, int, double> costModel)
            {
                int bufferStart = _blockStart;
                int bufferEnd = _blockEnd;

                if (bufferStart == bufferEnd)
                    return Array.Empty<int>();

                /* Best cost to get here so far. */
                int blockSize = bufferEnd - bufferStart;

                int[] sublen = null;
                int[] lengthArray = null;
                double[] costs = null;
                double mincost = 0;
                double symbolCost = 0;
                Hash hash = null;

                Parallel.Invoke(
                    () => { mincost = GetCostModelMinCost(costModel); },
                    () => { symbolCost = costModel(ZopfliDeflater.MaximumMatch, 1); },
                    () =>
                    {
                        hash = InitializeHash(buffer, bufferStart, bufferEnd);

                        sublen = new int[259];
                        lengthArray = new int[blockSize + 1];
                        costs = new double[blockSize + 1];

                        lengthArray[0] = 0;

                        costs[0] = 0d;  /* Because it'blockState the blockStart. */

                        for (int i = 1; i < blockSize + 1; i++)
                            costs[i] = double.MaxValue;
                    });

                for (int bufferIndex = bufferStart, lengthArrayIndex = 0; bufferIndex < bufferEnd; bufferIndex++, lengthArrayIndex++)
                {
                    hash.UpdateHash(buffer, bufferIndex, bufferEnd);

                    //If we're in a long repetition of the same character and have more than MaximumMatch characters before and after our blockStart.

                    if (bufferIndex > bufferStart + (ZopfliDeflater.MaximumMatch + 1)
                        && bufferIndex + ((ZopfliDeflater.MaximumMatch * 2) + 1) < bufferEnd
                        && hash._same[bufferIndex & ZopfliDeflater.WindowMask] > (ZopfliDeflater.MaximumMatch * 2)
                        && hash._same[(bufferIndex - ZopfliDeflater.MaximumMatch) & ZopfliDeflater.WindowMask] > ZopfliDeflater.MaximumMatch)
                    {
                        //Set the length to reach each one to MaximumMatch, and the cost to
                        //the cost corresponding to that length. Doing this, we skip
                        //MaximumMatch values to avoid calling FindLongestMatch.
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

                    /* Literal. */
                    if (bufferIndex + 1 <= bufferEnd)
                    {
                        double newCost = costs[lengthArrayIndex] + costModel(buffer[bufferIndex], 0);
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
                        /* Calling the cost model is expensive, avoid this if we are already at the minimum possible cost that it can return. */
                        if (costs[lengthArrayIndex + k] <= mincostaddcostj)
                            continue;

                        var newCost = costs[lengthArrayIndex] + costModel(k, sublen[k]);

                        if (newCost < costs[lengthArrayIndex + k])
                        {
                            costs[lengthArrayIndex + k] = newCost;
                            lengthArray[lengthArrayIndex + k] = k;
                        }
                    }
                }

                return lengthArray;
            }

            #endregion GetBestLengths

            #region GetCostModelMinCost

            /// <summary>
            /// Table of _distances that have a different distance symbol in the deflate
            /// specification. Each value is the first distance that has a new symbol. Only
            /// different symbols affect the cost model so only these need to be checked.
            /// See RFC 1951 section 3.2.5. Compressed blocks (length and distance codes).
            /// </summary>
            private static readonly int[] _costModelMinCostDistanceSymbols = new int[] //30
                { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257,
                    385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };

            private static double GetCostModelMinCost(Func<int, int, double> costModel)
            {
                int bestlength = 0; /* length that has lowest cost in the cost model */
                int bestdist = 0; /* distance that has lowest cost in the cost model */

                double mincost = double.MaxValue;
                for (int i = 3; i < 259; i++)
                {
                    double cost = costModel(i, 1);
                    if (cost < mincost)
                    {
                        bestlength = i;
                        mincost = cost;
                    }
                }

                mincost = double.MaxValue;

                for (int i = 0; i < 30; i++)
                {
                    double cost = costModel(3, _costModelMinCostDistanceSymbols[i]);
                    if (cost < mincost)
                    {
                        bestdist = _costModelMinCostDistanceSymbols[i];
                        mincost = cost;
                    }
                }

                return costModel(bestlength, bestdist);
            }

            #endregion GetCostModelMinCost

            #region FindLongestMatch

            /// <summary>
            /// Finds the longest match (length and corresponding distance) for LZ77 compression.
            /// Even when not using "sublen", it can be more efficient to provide an array, because only then the caching is used.
            /// </summary>
            /// <param name="blockState"></param>
            /// <param name="hash"></param>
            /// <param name="buffer"></param>
            /// <param name="bufferStart"></param>
            /// <param name="bufferEnd"></param>
            /// <param name="limit">limit length to maximum this value (default should be 258). This allows finding a shorter dist for that length (= less extra bits). Must be in the range [3, 258].</param>
            /// <param name="sublen">output array of 259 elements, or null. Has, for each length, the smallest distance required to reach this length. Only 256 of its 259 values are used, the first 3 are ignored (the shortest length is 3. It is purely for convenience that the array is made 3 longer).</param>
            private Match FindLongestMatch(Hash hash, byte[] buffer, int bufferStart, int bufferEnd, ref int limit, ref int[] sublen)
            {
                if (bufferEnd - bufferStart < MinimumMatch) //The rest of the code assumes there are at least MinimumMatch bytes to try.
                    return new Match(0, 0);

                int bestDistance = 0;
                int bestlength = 1;
                bool useFirstHashValue = true;

                int chainCounter = _deflater.MaximumChainHits;  /* For quitting early. */

                if (_cache != null)
                {
                    //The LMC cache starts at the beginning of the block rather than the beginning of the whole array.
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

                /* Go through all _distances. */
                while (dist < ZopfliDeflater.WindowSize)
                {
                    int currentlength = 0;

                    if (dist > 0)
                    {
                        var scanPosition = bufferStart;
                        var matchPosition = bufferStart - dist;

                        /* Testing the byte at blockStart bestlength first, goes slightly faster. */
                        if (bufferStart + bestlength >= bufferEnd ||
                            buffer[scanPosition + bestlength] == buffer[matchPosition + bestlength])
                        {
                            int same0 = hash._same[bufferStart & ZopfliDeflater.WindowMask];
                            if (same0 > 2 && buffer[scanPosition] == buffer[matchPosition])
                            {
                                int same1 = hash.GetSameValue(((bufferStart - dist) & ZopfliDeflater.WindowMask));

                                int same = same0 < same1 ? same0 : same1;

                                if (same > limit)
                                    same = limit;

                                scanPosition += same;
                                matchPosition += same;
                            }

                            scanPosition = GetMatch(buffer, scanPosition, matchPosition, arrayEndPos);

                            currentlength = scanPosition - bufferStart;  /* The found length. */
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

                    /* Switch to the other hash once this will be more efficient. */
                    if (useFirstHashValue && bestlength >= hash.GetSameValue(hashPosition) &&
                        hash.GetHashValue(false) == hash.GetHashValue(hashPoint, false))
                    {
                        /* Now use the hash that encodes the length and first byte. */
                        useFirstHashValue = false;
                    }

                    previousHashPoint = hashPoint;
                    hashPoint = hash.GetHashPrev(hashPoint, useFirstHashValue);

                    if (hashPoint == previousHashPoint)
                        break;  /* Uninited prev value. */

                    dist += (hashPoint < previousHashPoint ? previousHashPoint - hashPoint : ((ZopfliDeflater.WindowSize - hashPoint) + previousHashPoint));

                    if (--chainCounter < 0)
                        break;
                }


                var distance = bestDistance;
                var length = bestlength;


                if (sublen.Length > 0 && _cache != null && limit == MaximumMatch)
                    /* The LMC cache starts at the beginning of the block rather than the beginning of the whole array. */
                    _cache.StoreInLongestMatchCache(bufferStart - _blockStart, sublen, distance, length);

                return new Match(length, distance);
            }

            #endregion FindLongestMatch
        }

        #endregion BlockState

    }

    #region BitWriter

    /// <summary>
    /// Used by the Deflater to write bit encoded to an output stream
    /// </summary>
    internal sealed class BitWriter
    {
        #region Constructor

        /// <summary>
        /// Constructs a new BitWritter
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        public BitWriter(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (!stream.CanRead && !stream.CanWrite)
                throw new ArgumentException("Stream must be writable", "stream");

            _stream = stream;
        }

        #endregion Constructor

        #region Properties/Fields

        private byte[] _buffer = new byte[2];
        private int _bitBuffer;
        private int _bitCount;
        private Stream _stream;
        #endregion Properties/Fields

        #region Public Methods

        /// <summary>
        /// Writes the stored bits to the stream
        /// </summary>
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

        /// <summary>
        /// Write a single bit
        /// </summary>
        /// <param name="value">The bit to write</param>
        public void Write(bool value)
        {
            if(value)
                _bitBuffer |= 1 << _bitCount;
            _bitCount++;
        }

        /// <summary>
        /// Writes the bits
        /// </summary>
        /// <param name="value">The packed bits</param>
        /// <param name="numberOfBits">The number if bits to write</param>
        public void Write(int value, int numberOfBits)
        {
            PrivateWrite(value, numberOfBits);
        }

        /// <summary>
        /// Writes a number of bytes to the stream
        /// </summary>
        /// <param name="buffer">The bytes to write</param>
        public void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Writes a number of bytes to the stream
        /// </summary>
        /// <param name="buffer">The bytes to write</param>
        /// <param name="offset">The offset to blockStart</param>
        /// <param name="count">The total number of bytes to write</param>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (_bitCount == 0)
                _stream.Write(buffer, offset, count);
            else
                for (int i = offset; i < (offset + count); i++)
                    PrivateWrite(buffer[i], 8);
        }

        /// <summary>
        /// Writes the bits
        /// </summary>
        /// <param name="value">The packed bits</param>
        /// <param name="numberOfBits">The number if bits to write</param>
        public void WriteHuffman(int value, int numberOfBits)
        {
            for (int i = numberOfBits - 1; i >= 0; i--)
            {
                _bitBuffer |= ((value >> i) & 1) << _bitCount++;
            }
            WriteBuffer();
        }
        #endregion Public Methods

        #region Private Methods

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

        #endregion Private Methods
    }

    #endregion

    #region Enum

    public enum BlockType
    {
        Uncompressed,
        Fixed,
        Dynamic
    }

    /// <summary>
    /// Determines how to split the blocks
    /// </summary>
    public enum BlockSplitting
    {
        /// <summary>
        /// No block splitting
        /// </summary>
        None,

        /// <summary>
        /// Chooses the blocksplit points first, then does iterative LZ77 on each individual block
        /// </summary>
        First,

        /// <summary>
        /// Chooses the blocksplit points last
        /// </summary>
        Last
    }

    #endregion
}