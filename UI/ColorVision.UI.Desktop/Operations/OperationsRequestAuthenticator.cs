using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsAuthenticationResult
    {
        public bool Success { get; init; }

        public string ErrorCode { get; init; } = string.Empty;

        public OperationsPairedDevice? Device { get; init; }
    }

    public sealed class OperationsRequestAuthenticator
    {
        private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(2);
        private readonly OperationsDeviceRegistry _registry;
        private readonly ConcurrentDictionary<string, long> _usedNonces = new(StringComparer.Ordinal);

        public OperationsRequestAuthenticator(OperationsDeviceRegistry registry)
        {
            _registry = registry;
        }

        public OperationsAuthenticationResult Authenticate(string method, string path, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            if (!TryHeader(headers, "X-CV-Device-Id", out string deviceId)
                || !TryHeader(headers, "X-CV-Timestamp", out string timestampText)
                || !TryHeader(headers, "X-CV-Nonce", out string nonce)
                || !TryHeader(headers, "X-CV-Signature", out string signatureText))
                return Failure("signed_headers_required");

            if (!long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out long timestamp))
                return Failure("invalid_timestamp");

            DateTimeOffset requestTime;
            try
            {
                requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Failure("invalid_timestamp");
            }

            if ((DateTimeOffset.UtcNow - requestTime).Duration() > AllowedClockSkew)
                return Failure("request_time_out_of_range");

            if (nonce.Length is < 16 or > 128 || nonce.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_')))
                return Failure("invalid_nonce");

            OperationsPairedDevice? device = _registry.FindActive(deviceId);
            if (device == null)
                return Failure("unknown_or_revoked_device");

            try
            {
                byte[] signature = Convert.FromBase64String(signatureText);
                byte[] publicKey = Convert.FromBase64String(device.PublicKeySpki);
                byte[] digest = SHA256.HashData(body);
                string canonical = BuildCanonical(method, path, timestampText, nonce, Convert.ToHexString(digest).ToLowerInvariant());
                using ECDsa key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(publicKey, out _);
                if (!key.VerifyData(Encoding.UTF8.GetBytes(canonical), signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
                    return Failure("invalid_request_signature");
            }
            catch (FormatException)
            {
                return Failure("invalid_signature_encoding");
            }
            catch (CryptographicException)
            {
                return Failure("invalid_request_signature");
            }

            CleanupNonces(timestamp);
            string nonceKey = $"{deviceId}:{nonce}";
            if (!_usedNonces.TryAdd(nonceKey, timestamp))
                return Failure("replayed_request");

            _registry.MarkSeen(deviceId);
            return new OperationsAuthenticationResult { Success = true, Device = device };
        }

        public static string BuildCanonical(string method, string path, string timestamp, string nonce, string bodySha256)
        {
            return string.Join('\n', method.ToUpperInvariant(), path, timestamp, nonce, bodySha256);
        }

        private void CleanupNonces(long currentTimestamp)
        {
            long cutoff = currentTimestamp - (long)AllowedClockSkew.TotalSeconds * 2;
            foreach (KeyValuePair<string, long> entry in _usedNonces)
            {
                if (entry.Value < cutoff)
                    _usedNonces.TryRemove(entry.Key, out _);
            }
        }

        private static bool TryHeader(IReadOnlyDictionary<string, string> headers, string name, out string value)
        {
            if (headers.TryGetValue(name, out string? found) && !string.IsNullOrWhiteSpace(found))
            {
                value = found.Trim();
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static OperationsAuthenticationResult Failure(string code) => new() { ErrorCode = code };
    }
}
