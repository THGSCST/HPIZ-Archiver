using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

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
            if (filename == null)
                throw new ArgumentNullException(nameof(filename));

            try
            {
                using (FileStream fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return CalculateSha256(fileStream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while reading the file '{filename}'.\n\nDetails: {ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty; // Return an empty string in case of an error
            }
        }

        public static string CalculateSha256(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                return CalculateSha256(memoryStream);
            }
        }

        private static string CalculateSha256(Stream dataStream)
        {
            if (dataStream == null)
                throw new ArgumentNullException(nameof(dataStream));

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(dataStream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
