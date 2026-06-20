using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace HPIZ
{
    public static class HashUtility
    {
        public static string CalculateSha256(string fileName)
        {
            return CalculateSha256(fileName, CancellationToken.None);
        }

        public static string CalculateSha256(string fileName, CancellationToken cancellationToken)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            using (var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return CalculateSha256(fileStream, cancellationToken);
        }

        public static string CalculateSha256(byte[] data)
        {
            return CalculateSha256(data, CancellationToken.None);
        }

        public static string CalculateSha256(byte[] data, CancellationToken cancellationToken)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            cancellationToken.ThrowIfCancellationRequested();
            using (var sha256 = SHA256.Create())
                return FormatHash(sha256.ComputeHash(data));
        }

        private static string CalculateSha256(Stream dataStream, CancellationToken cancellationToken)
        {
            using (var sha256 = SHA256.Create())
            {
                var buffer = new byte[128 * 1024];
                int bytesRead;
                while ((bytesRead = dataStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return FormatHash(sha256.Hash);
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
