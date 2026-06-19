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

            using (var sha256 = SHA256.Create())
                return FormatHash(sha256.ComputeHash(data));
        }

        private static string CalculateSha256(Stream dataStream)
        {
            using (var sha256 = SHA256.Create())
            {
                return FormatHash(sha256.ComputeHash(dataStream));
            }
        }

        private static string FormatHash(byte[] hash)
        {
            var characters = new char[hash.Length * 2];
            const string Hex = "0123456789abcdef";
            for (int i = 0; i < hash.Length; i++)
            {
                characters[i * 2] = Hex[hash[i] >> 4];
                characters[(i * 2) + 1] = Hex[hash[i] & 0x0F];
            }

            return new string(characters);
        }
    }
}
