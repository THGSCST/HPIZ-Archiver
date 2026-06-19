using System;
using System.IO;
using System.Security.Cryptography;

namespace HPIZ
{
    public static class HashUtility
    {
        public static string CalculateSha256(string fileName)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            using (var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return CalculateSha256(fileStream);
        }

        public static string CalculateSha256(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using (var memoryStream = new MemoryStream(data))
                return CalculateSha256(memoryStream);
        }

        private static string CalculateSha256(Stream dataStream)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(dataStream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
