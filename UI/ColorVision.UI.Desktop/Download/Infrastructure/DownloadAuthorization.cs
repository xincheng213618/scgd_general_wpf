using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Desktop.Download
{
    internal static class DownloadAuthorization
    {
        private const string Prefix = "dpapi:";
        public static string? Encode(string? authorization) => string.IsNullOrEmpty(authorization) ? null : Prefix + Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(authorization), null, DataProtectionScope.CurrentUser));

        public static string? Decode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            if (encoded.StartsWith(Prefix, StringComparison.Ordinal))
            {
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encoded[Prefix.Length..]), null, DataProtectionScope.CurrentUser)); }
                catch (CryptographicException) { return null; }
                catch (FormatException) { return null; }
            }
            // Existing task stores remain readable; initialization migrates these values for this user.
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
            catch (FormatException) { return encoded; }
        }
    }
}
