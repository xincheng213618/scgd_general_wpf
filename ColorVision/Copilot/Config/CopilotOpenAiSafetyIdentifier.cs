using System;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotOpenAiSafetyIdentifier
    {
        private const string HashNamespace = "ColorVision.Copilot.SafetyIdentifier.v1\n";
        private static readonly Lazy<string> Current = new(CreateForCurrentUser);

        public static string GetCurrent() => Current.Value;

        internal static string Create(string? accountName)
        {
            var normalizedAccountName = (accountName ?? string.Empty).Trim();
            if (normalizedAccountName.Length == 0)
                return string.Empty;

            var source = HashNamespace + normalizedAccountName.ToUpperInvariant();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
                .ToLowerInvariant();
        }

        private static string CreateForCurrentUser()
        {
            var userName = Environment.UserName.Trim();
            if (userName.Length == 0)
                return string.Empty;
            var domainName = Environment.UserDomainName.Trim();
            return Create(domainName.Length == 0 ? userName : $"{domainName}\\{userName}");
        }
    }
}
