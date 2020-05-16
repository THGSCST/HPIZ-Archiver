using System;
using System.Collections;
using System.IO;
using System.Text;

namespace HPIZ
{
    public static class LZ77
    {
        public static void Decompress(byte[] input, byte[] output)
        {
            if(input == null || output == null)
                throw new NullReferenceException("Input or output cannot be null");

            var slidingWindow = new byte[0x1000];
            int wPos = 1; // window position index
            int iPos = 0; // input position index
            int oPos = 0; // output position index

            while (true)
            {
                int flag = input[iPos++];

                for (int i = 0; i < 8; ++i)
                {
                    if ((flag & 1) == 0) //if is odd next byte is literal
                    {
                        slidingWindow[wPos] = input[iPos++];
                        output[oPos++] = slidingWindow[wPos];
                        wPos = (wPos + 1) & 0xFFF; //Increment limited to slidingWindow size
                    }
                    else // sliding window works
                    {
                        int windowReadPos = (input[iPos + 1] << 4) | input[iPos] >> 4;
                        if (windowReadPos == 0) return; //Get out
                        
                        int count = (input[iPos] & 0x0F) + 2;
                        iPos += 2;

                        for (int j = 0; j < count; ++j)
                        {
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
