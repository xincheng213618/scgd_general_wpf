using ColorVision.Common.Utilities;
using System;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotCredentialProtector
    {
        private const string ProtectedPrefix = "dpapi:v1:";
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("ColorVision.Copilot.Credentials.v1");

        public static string Protect(string? plaintext)
        {
            var normalized = plaintext?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized) || IsProtected(normalized))
                return normalized;

            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(normalized),
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            return ProtectedPrefix + Convert.ToBase64String(protectedBytes);
        }

        public static bool TryUnprotect(string? protectedValue, out string plaintext, out bool usedLegacyEncryption)
        {
            plaintext = string.Empty;
            usedLegacyEncryption = false;
            var normalized = protectedValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return true;

            if (IsProtected(normalized))
            {
                try
                {
                    var protectedBytes = Convert.FromBase64String(normalized[ProtectedPrefix.Length..]);
                    var plaintextBytes = ProtectedData.Unprotect(
                        protectedBytes,
                        OptionalEntropy,
                        DataProtectionScope.CurrentUser);
                    plaintext = Encoding.UTF8.GetString(plaintextBytes).Trim();
                    return !string.IsNullOrWhiteSpace(plaintext);
                }
                catch (Exception exception) when (exception is FormatException or CryptographicException)
                {
                    return false;
                }
            }

            var legacyPlaintext = Cryptography.AESDecrypt(
                normalized,
                CopilotConfig.ConfigAESKey,
                CopilotConfig.ConfigAESVector).Trim();
            if (string.IsNullOrWhiteSpace(legacyPlaintext))
                return false;

            plaintext = legacyPlaintext;
            usedLegacyEncryption = true;
            return true;
        }

        public static bool IsProtected(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
    }
}
