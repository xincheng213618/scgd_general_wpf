using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision
{
    internal static class SingleInstanceMutexName
    {
        public static string Create(string executablePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

            string normalizedExecutablePath = Path.GetFullPath(executablePath).ToUpperInvariant();
            byte[] pathHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedExecutablePath));
            return $"ColorVision-{Convert.ToHexString(pathHash)}";
        }
    }
}
