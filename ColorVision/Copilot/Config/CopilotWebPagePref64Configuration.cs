using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ColorVision.Copilot
{
    internal readonly record struct WebPageNat64Prefix(byte[] Bytes, int Length);

    internal static class CopilotWebPagePref64Configuration
    {
        public const int MaximumTextCharacters = 4096;
        public const int MaximumPrefixCount = 16;
        private const int MaximumPrefixTextCharacters = 64;
        private static readonly int[] SupportedPrefixLengths = [32, 40, 48, 56, 64, 96];

        public static bool TryParse(
            string? text,
            out IReadOnlyList<WebPageNat64Prefix> prefixes,
            out string error)
        {
            prefixes = Array.Empty<WebPageNat64Prefix>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            if (text.Length > MaximumTextCharacters)
            {
                error = $"Pref64 configuration cannot exceed {MaximumTextCharacters:N0} characters.";
                return false;
            }

            var parsed = new List<WebPageNat64Prefix>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            using var reader = new StringReader(text);
            string? line;
            var lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                var commentIndex = line.IndexOf('#');
                var value = (commentIndex >= 0 ? line[..commentIndex] : line).Trim();
                if (value.Length == 0)
                    continue;
                if (value.Length > MaximumPrefixTextCharacters)
                {
                    error = $"The Pref64 CIDR on line {lineNumber:N0} is too long.";
                    return false;
                }
                if (parsed.Count >= MaximumPrefixCount)
                {
                    error = $"At most {MaximumPrefixCount:N0} Pref64 prefixes can be configured.";
                    return false;
                }

                var separatorIndex = value.IndexOf('/');
                if (separatorIndex <= 0 || separatorIndex != value.LastIndexOf('/'))
                {
                    error = $"Line {lineNumber:N0} must contain an IPv6 CIDR.";
                    return false;
                }
                if (!int.TryParse(
                        value.AsSpan(separatorIndex + 1),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var prefixLength)
                    || !SupportedPrefixLengths.Contains(prefixLength))
                {
                    error = $"The prefix length on line {lineNumber:N0} must be /32, /40, /48, /56, /64, or /96.";
                    return false;
                }
                var addressText = value[..separatorIndex];
                if (addressText.Contains('%') || addressText.Contains('[') || addressText.Contains(']')
                    || !IPAddress.TryParse(addressText, out var address)
                    || address.AddressFamily != AddressFamily.InterNetworkV6
                    || address.IsIPv4MappedToIPv6
                    || address.ScopeId != 0)
                {
                    error = $"Line {lineNumber:N0} must contain an unscoped IPv6 CIDR.";
                    return false;
                }

                var bytes = address.GetAddressBytes();
                var prefixByteCount = prefixLength / 8;
                if (bytes.AsSpan(prefixByteCount).IndexOfAnyExcept((byte)0) >= 0)
                {
                    error = $"Line {lineNumber:N0} must use an aligned network address with zero bits outside the prefix.";
                    return false;
                }
                if (bytes[8] != 0)
                {
                    error = $"Line {lineNumber:N0} does not meet RFC 6052 because the u octet is not zero.";
                    return false;
                }

                var prefixBytes = bytes.AsSpan(0, prefixByteCount).ToArray();
                var identity = $"{prefixLength.ToString(CultureInfo.InvariantCulture)}:{Convert.ToHexString(prefixBytes)}";
                if (identities.Add(identity))
                    parsed.Add(new WebPageNat64Prefix(prefixBytes, prefixLength));
            }

            prefixes = parsed.ToArray();
            return true;
        }

        public static string Format(IEnumerable<WebPageNat64Prefix> prefixes)
        {
            ArgumentNullException.ThrowIfNull(prefixes);
            return string.Join(
                Environment.NewLine,
                prefixes.Select(static prefix =>
                {
                    var bytes = new byte[16];
                    prefix.Bytes.CopyTo(bytes, 0);
                    return $"{new IPAddress(bytes)}/{prefix.Length.ToString(CultureInfo.InvariantCulture)}";
                }));
        }
    }
}
