using System;

namespace HPIZArchiver
{
    internal static class Utils
    {
        public static string SizeSuffix(long byteCount, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (byteCount == 0) { return "0 bytes"; }
            int mag = (int)Math.Log(byteCount, 1024);
            decimal adjustedSize = (decimal)byteCount / (1L << (mag * 10));
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }
            string[] SizeSuffixes = { "bytes", "kB", "MB", "GB", "TB" };
            return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
        }

    }
}
