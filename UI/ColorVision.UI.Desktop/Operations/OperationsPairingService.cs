using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsPairingChallenge
    {
        public string PairingId { get; init; } = string.Empty;

        public string Nonce { get; init; } = string.Empty;

        public string HostId { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;

        public string CertificateSha256 { get; init; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; init; }

        public bool IsClaimed { get; set; }
    }

    public sealed class OperationsPairingClaim
    {
        public string PairingId { get; init; } = string.Empty;

        public string DeviceId { get; init; } = string.Empty;

        public string DeviceName { get; init; } = string.Empty;

        public string PublicKeySpki { get; init; } = string.Empty;

        public DateTimeOffset SubmittedAt { get; init; }

        public string Status { get; set; } = "pending";
    }

    public sealed class OperationsPairingService
    {
        public static readonly string[] InitialScopes =
        [
            "ops.capabilities.read",
            "ops.status.read",
            "ops.alerts.read",
            "ops.diagnostics.read",
            "ops.jobs.read",
            "ops.jobs.create",
            "ops.approvals.decide",
            "ops.deployments.read",
            "ops.deployments.receipt.create",
            "ops.support.read",
            "ops.support.request",
            "ops.audit.read",
        ];

        private readonly object _syncRoot = new();
        private readonly OperationsDeviceRegistry _registry;
        private readonly Dictionary<string, OperationsPairingChallenge> _challenges = new(StringComparer.Ordinal);
        private readonly Dictionary<string, OperationsPairingClaim> _claims = new(StringComparer.Ordinal);

        public OperationsPairingService(OperationsDeviceRegistry registry)
        {
            _registry = registry;
        }

        public event EventHandler? ClaimsChanged;

        public OperationsPairingChallenge CreateChallenge(string hostId, string endpoint, string certificateSha256, TimeSpan? lifetime = null)
        {
            CleanupExpired();
            OperationsPairingChallenge challenge = new()
            {
                PairingId = Guid.NewGuid().ToString("N"),
                Nonce = Base64Url(RandomNumberGenerator.GetBytes(32)),
                HostId = hostId,
                Endpoint = endpoint,
                CertificateSha256 = certificateSha256,
                ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(2)),
            };

            lock (_syncRoot)
            {
                _challenges[challenge.PairingId] = challenge;
            }
            return challenge;
        }

        public string BuildQrPayload(OperationsPairingChallenge challenge)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(new
            {
                version = 1,
                challenge.PairingId,
                challenge.Nonce,
                challenge.HostId,
                challenge.Endpoint,
                challenge.CertificateSha256,
                expiresAt = challenge.ExpiresAt,
            }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            return $"colorvision://pair?v=1&payload={Uri.EscapeDataString(Base64Url(Encoding.UTF8.GetBytes(json)))}";
        }

        public (bool Success, string Error) SubmitClaim(string pairingId, string deviceId, string deviceName, string publicKeySpki, string signature)
        {
            if (!IsSafeIdentifier(deviceId) || string.IsNullOrWhiteSpace(deviceName) || deviceName.Trim().Length > 80)
                return (false, "invalid_device_identity");

            OperationsPairingChallenge? challenge;
            lock (_syncRoot)
            {
                _challenges.TryGetValue(pairingId, out challenge);
                if (challenge == null || challenge.ExpiresAt <= DateTimeOffset.UtcNow || challenge.IsClaimed)
                    return (false, "pairing_challenge_invalid_or_expired");
            }

            try
            {
                OperationsDeviceRegistry.ValidatePublicKey(publicKeySpki);
                byte[] keyBytes = Convert.FromBase64String(publicKeySpki);
                byte[] signatureBytes = Convert.FromBase64String(signature);
                using ECDsa key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(keyBytes, out _);
                byte[] canonical = Encoding.UTF8.GetBytes(BuildClaimCanonical(challenge, deviceId, deviceName.Trim()));
                if (!key.VerifyData(canonical, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                    return (false, "invalid_pairing_signature");
            }
            catch (FormatException)
            {
                return (false, "invalid_pairing_encoding");
            }
            catch (CryptographicException)
            {
                return (false, "invalid_pairing_key");
            }

            lock (_syncRoot)
            {
                if (!_challenges.TryGetValue(pairingId, out OperationsPairingChallenge? current)
                    || !ReferenceEquals(current, challenge) || current.IsClaimed
                    || current.ExpiresAt <= DateTimeOffset.UtcNow)
                    return (false, "pairing_challenge_invalid_or_expired");
                challenge.IsClaimed = true;
                _claims[pairingId] = new OperationsPairingClaim
                {
                    PairingId = pairingId,
                    DeviceId = deviceId,
                    DeviceName = deviceName.Trim(),
                    PublicKeySpki = publicKeySpki,
                    SubmittedAt = DateTimeOffset.UtcNow,
                };
            }
            ClaimsChanged?.Invoke(this, EventArgs.Empty);
            return (true, string.Empty);
        }

        public IReadOnlyList<OperationsPairingClaim> GetPendingClaims()
        {
            lock (_syncRoot)
            {
                return _claims.Values.Where(item => item.Status == "pending").Select(Clone).ToList();
            }
        }

        public OperationsPairingClaim? GetClaim(string pairingId, string deviceId)
        {
            lock (_syncRoot)
            {
                if (!_claims.TryGetValue(pairingId, out OperationsPairingClaim? claim)
                    || !string.Equals(claim.DeviceId, deviceId, StringComparison.Ordinal))
                    return null;
                return Clone(claim);
            }
        }

        public bool Approve(string pairingId)
        {
            lock (_syncRoot)
            {
                if (!_claims.TryGetValue(pairingId, out OperationsPairingClaim? claim) || claim.Status != "pending")
                    return false;

                _registry.Approve(claim.DeviceId, claim.DeviceName, claim.PublicKeySpki, InitialScopes);
                claim.Status = "approved";
            }
            ClaimsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool Reject(string pairingId)
        {
            lock (_syncRoot)
            {
                if (!_claims.TryGetValue(pairingId, out OperationsPairingClaim? claim) || claim.Status != "pending")
                    return false;
                claim.Status = "rejected";
            }
            ClaimsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public static string BuildClaimCanonical(OperationsPairingChallenge challenge, string deviceId, string deviceName)
        {
            return string.Join('\n', "colorvision-pair-v1", challenge.PairingId, challenge.Nonce, challenge.HostId,
                challenge.Endpoint, deviceId, deviceName);
        }

        private void CleanupExpired()
        {
            lock (_syncRoot)
            {
                foreach (string id in _challenges.Where(item => item.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(item => item.Key).ToList())
                    _challenges.Remove(id);
            }
        }

        private static bool IsSafeIdentifier(string value) => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 64
            && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static OperationsPairingClaim Clone(OperationsPairingClaim value) => new()
        {
            PairingId = value.PairingId,
            DeviceId = value.DeviceId,
            DeviceName = value.DeviceName,
            PublicKeySpki = value.PublicKeySpki,
            SubmittedAt = value.SubmittedAt,
            Status = value.Status,
        };
    }
}
