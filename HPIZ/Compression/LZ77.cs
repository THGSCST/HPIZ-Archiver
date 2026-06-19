using System;
using System.IO;

namespace HPIZ
{
    public static class LZ77
    {
        public static void Decompress(byte[] input, byte[] output)
        {
            Decompress(input, 0, input?.Length ?? 0, output, 0, output?.Length ?? 0);
        }

        internal static void Decompress(
            byte[] input,
            int inputOffset,
            int inputCount,
            byte[] output,
            int outputOffset,
            int outputCount)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (inputOffset < 0 || inputCount < 0 || inputOffset > input.Length - inputCount)
                throw new ArgumentOutOfRangeException();
            if (outputOffset < 0 || outputCount < 0 || outputOffset > output.Length - outputCount)
                throw new ArgumentOutOfRangeException();

            var slidingWindow = new byte[0x1000];
            int wPos = 1; // window position
            int iPos = inputOffset;
            int inputEnd = inputOffset + inputCount;
            int oPos = outputOffset;
            int outputEnd = outputOffset + outputCount;

            while (true)
            {
                if (iPos >= inputEnd)
                    throw new InvalidDataException("LZ77 stream ended before its end marker.");

                int flag = input[iPos++];

                for (int i = 0; i < 8; ++i)
                {
                    if ((flag & 1) == 0)
                    {
                        if (iPos >= inputEnd)
                            throw new InvalidDataException("LZ77 literal exceeds the input buffer.");
                        if (oPos >= outputEnd)
                            throw new InvalidDataException("LZ77 output exceeds the expected size.");

                        slidingWindow[wPos] = input[iPos++];
                        output[oPos++] = slidingWindow[wPos];
                        wPos = (wPos + 1) & 0xFFF; //Increment limited to slidingWindow size
                    }
                    else // sliding window works
                    {
                        if (iPos + 1 >= inputEnd)
                            throw new InvalidDataException("LZ77 back-reference exceeds the input buffer.");

                        int windowReadPos = (input[iPos + 1] << 4) | input[iPos] >> 4;
                        if (windowReadPos == 0)
                        {
                            if (oPos != outputEnd)
                                throw new InvalidDataException("LZ77 stream ended before the expected output size.");
                            return;
                        }

                        int count = (input[iPos] & 0xF) + 2;
                        iPos += 2;

                        while (count-- > 0)
                        {
                            if (oPos >= outputEnd)
                                throw new InvalidDataException("LZ77 output exceeds the expected size.");

                            output[oPos++] = slidingWindow[windowReadPos];
                            slidingWindow[wPos] = slidingWindow[windowReadPos];
                            windowReadPos = (windowReadPos + 1) & 0xFFF;
                            wPos = (wPos + 1) & 0xFFF;
                        }
                    }

                    flag >>= 1;
                }
            }
        }


    }
}
