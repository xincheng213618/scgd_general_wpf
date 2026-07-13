using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests
{
    public class OperationsPairingSecurityTests
    {
        [Fact]
        public void PairingRequiresValidDeviceProofAndExplicitApproval()
        {
            string path = CreateStorePath();
            try
            {
                OperationsDeviceRegistry registry = new(path);
                OperationsPairingService pairing = new(registry);
                OperationsPairingChallenge challenge = pairing.CreateChallenge(
                    "host-1", "https://192.168.1.2:8788", new string('a', 64));
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
                string canonical = OperationsPairingService.BuildClaimCanonical(challenge, "device-1", "Field phone");
                string signature = Convert.ToBase64String(key.SignData(
                    Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

                (bool success, _) = pairing.SubmitClaim(
                    challenge.PairingId, "device-1", "Field phone", publicKey, signature);

                Assert.True(success);
                Assert.Null(registry.FindActive("device-1"));
                Assert.True(pairing.Approve(challenge.PairingId));
                OperationsPairedDevice approved = Assert.IsType<OperationsPairedDevice>(registry.FindActive("device-1"));
                Assert.Contains("ops.status.read", approved.Scopes);
            }
            finally
            {
                DeleteStore(path);
            }
        }

        [Fact]
        public void PairingChallengeIsSingleUse()
        {
            string path = CreateStorePath();
            try
            {
                OperationsPairingService pairing = new(new OperationsDeviceRegistry(path));
                OperationsPairingChallenge challenge = pairing.CreateChallenge("host", "https://host:8788", "pin");
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
                string canonical = OperationsPairingService.BuildClaimCanonical(challenge, "device", "Phone");
                string signature = Convert.ToBase64String(key.SignData(
                    Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

                Assert.True(pairing.SubmitClaim(challenge.PairingId, "device", "Phone", publicKey, signature).Success);
                Assert.False(pairing.SubmitClaim(challenge.PairingId, "device", "Phone", publicKey, signature).Success);
            }
            finally
            {
                DeleteStore(path);
            }
        }

        [Fact]
        public void SignedRequestRejectsReplayAndTampering()
        {
            string path = CreateStorePath();
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(path);
                registry.Approve("device-2", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), ["ops.status.read"]);
                OperationsRequestAuthenticator authenticator = new(registry);
                byte[] body = Encoding.UTF8.GetBytes("{}");
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
                string nonce = "0123456789abcdef";
                string digest = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
                string canonical = OperationsRequestAuthenticator.BuildCanonical("POST", "/ops/v1/test", timestamp, nonce, digest);
                string signature = Convert.ToBase64String(key.SignData(
                    Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
                Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["X-CV-Device-Id"] = "device-2",
                    ["X-CV-Timestamp"] = timestamp,
                    ["X-CV-Nonce"] = nonce,
                    ["X-CV-Signature"] = signature,
                };

                Assert.True(authenticator.Authenticate("POST", "/ops/v1/test", headers, body).Success);
                Assert.Equal("replayed_request", authenticator.Authenticate("POST", "/ops/v1/test", headers, body).ErrorCode);

                headers["X-CV-Nonce"] = "fedcba9876543210";
                Assert.Equal("invalid_request_signature", authenticator.Authenticate("POST", "/ops/v1/test", headers, body).ErrorCode);

                string freshCanonical = OperationsRequestAuthenticator.BuildCanonical(
                    "POST", "/ops/v1/test", timestamp, headers["X-CV-Nonce"], digest);
                headers["X-CV-Signature"] = Convert.ToBase64String(key.SignData(
                    Encoding.UTF8.GetBytes(freshCanonical), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
                Assert.True(authenticator.Authenticate("POST", "/ops/v1/test", headers, body).Success);
            }
            finally
            {
                DeleteStore(path);
            }
        }

        [Fact]
        public void RevokedDeviceCannotAuthenticate()
        {
            string path = CreateStorePath();
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(path);
                registry.Approve("device-3", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), ["ops.status.read"]);
                Assert.True(registry.Revoke("device-3"));

                OperationsRequestAuthenticator authenticator = new(registry);
                Dictionary<string, string> headers = Sign(key, "device-3", "GET", "/ops/v1/snapshot", []);
                Assert.Equal("unknown_or_revoked_device", authenticator.Authenticate(
                    "GET", "/ops/v1/snapshot", headers, []).ErrorCode);
            }
            finally
            {
                DeleteStore(path);
            }
        }

        [Fact]
        public void SecureRouterRejectsBearerOnlyAndQueryCredentials()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-4", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsPairingService pairing = new(registry);
                OperationsSecureApiRouter router = new(pairing, new OperationsRequestAuthenticator(registry),
                    new OperationsWorkStore(workPath), () => new { healthy = true });

                OperationsApiResponse bearerOnly = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = "/ops/v1/capabilities",
                    Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer legacy" },
                });
                Assert.Equal(401, bearerOnly.StatusCode);

                Dictionary<string, string> signedHeaders = Sign(key, "device-4", "GET", "/ops/v1/capabilities", []);
                OperationsApiResponse queryCredential = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = "/ops/v1/capabilities",
                    Headers = signedHeaders,
                    Query = new Dictionary<string, string> { ["token"] = "legacy" },
                });
                Assert.Equal(400, queryCredential.StatusCode);

                Dictionary<string, string> freshHeaders = Sign(key, "device-4", "GET", "/ops/v1/capabilities", []);
                OperationsApiResponse accepted = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = "/ops/v1/capabilities",
                    Headers = freshHeaders,
                });
                Assert.Equal(200, accepted.StatusCode);
                Assert.Equal("no-store", accepted.Headers["Cache-Control"]);
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        private static Dictionary<string, string> Sign(ECDsa key, string deviceId, string method, string path, byte[] body)
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            string digest = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
            string canonical = OperationsRequestAuthenticator.BuildCanonical(method, path, timestamp, nonce, digest);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-CV-Device-Id"] = deviceId,
                ["X-CV-Timestamp"] = timestamp,
                ["X-CV-Nonce"] = nonce,
                ["X-CV-Signature"] = Convert.ToBase64String(key.SignData(
                    Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)),
            };
        }

        private static string CreateStorePath() => Path.Combine(Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"), "devices.json");

        private static void DeleteStore(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
