using System;
using System.IO;

namespace HPIZ
{
    public static class LZ77
    {
        public static void Decompress(byte[] input, byte[] output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            var slidingWindow = new byte[0x1000];
            int wPos = 1; // window position
            int iPos = 0; // input position
            int oPos = 0; // output position

            while (true)
            {
                if (iPos >= input.Length)
                    throw new InvalidDataException("LZ77 stream ended before its end marker.");

                int flag = input[iPos++];

                for (int i = 0; i < 8; ++i)
                {
                    if ((flag & 1) == 0)
                    {
                        if (iPos >= input.Length)
                            throw new InvalidDataException("LZ77 literal exceeds the input buffer.");
                        if (oPos >= output.Length)
                            throw new InvalidDataException("LZ77 output exceeds the expected size.");

                        slidingWindow[wPos] = input[iPos++];
                        output[oPos++] = slidingWindow[wPos];
                        wPos = (wPos + 1) & 0xFFF; //Increment limited to slidingWindow size
                    }
                    else // sliding window works
                    {
                        if (iPos + 1 >= input.Length)
                            throw new InvalidDataException("LZ77 back-reference exceeds the input buffer.");

                        int windowReadPos = (input[iPos + 1] << 4) | input[iPos] >> 4;
                        if (windowReadPos == 0)
                        {
                            if (oPos != output.Length)
                                throw new InvalidDataException("LZ77 stream ended before the expected output size.");
                            return;
                        }

                        int count = (input[iPos] & 0xF) + 2;
                        iPos += 2;

                        while (count-- > 0)
                        {
                            if (oPos >= output.Length)
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
