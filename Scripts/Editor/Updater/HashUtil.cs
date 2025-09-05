#if UNITY_EDITOR
using System;
using System.Security.Cryptography;
using System.Text;

namespace HoyoToon.Updater
{
    internal static class HashUtil
    {
        public static string GitBlobSha(byte[] bytes)
        {
            var header = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
            var combined = new byte[header.Length + bytes.Length];
            Buffer.BlockCopy(header, 0, combined, 0, header.Length);
            Buffer.BlockCopy(bytes, 0, combined, header.Length, bytes.Length);
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(combined);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
#endif
