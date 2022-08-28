using System;
using System.IO;
using System.Security.Cryptography;

namespace HPIZArchiver
{
    internal static class Utils
    {
        static SHA256 mySHA256 = SHA256.Create();
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

        public static string CalculateSha256(string filename)
        {
            using (SHA256 mySHA256 = SHA256.Create())
                return BitConverter.ToString(mySHA256.ComputeHash(File.Open(filename, FileMode.Open))).ToLower().Replace("-", string.Empty);
        }

        public static string CalculateSha256(byte[] data)
        {
                return BitConverter.ToString(mySHA256.ComputeHash(data)).ToLower().Replace("-", string.Empty);
        }
    }
}
