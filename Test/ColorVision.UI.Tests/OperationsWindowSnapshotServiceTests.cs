using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsWindowSnapshotServiceTests
    {
        private static readonly byte[] TestJpeg = [0xff, 0xd8, 0xff, 0xe0, 0x01, 0x02, 0xff, 0xd9];

        [Fact]
        public void SnapshotCanBeReadOnceAndIsDeletedAfterSuccessfulRead()
        {
            string root = NewRoot();
            DateTimeOffset now = new(2026, 8, 12, 4, 0, 0, TimeSpan.Zero);
            try
            {
                OperationsWindowSnapshotService service = new(
                    Path.Combine(root, "snapshots"), () => now, () => TestJpeg);
                OperationsWindowSnapshotResult created = service.Create();

                Assert.True(File.Exists(created.FilePath));
                Assert.Empty(created.Data);
                Assert.Equal(TestJpeg.Length, created.SizeBytes);
                Assert.Equal(now.AddMinutes(5), created.ExpiresAt);

                Assert.Equal(OperationsWindowSnapshotLookupStatus.Available,
                    service.TryTake(created.SnapshotId, out OperationsWindowSnapshotResult? download));
                Assert.NotNull(download);
                Assert.Equal(TestJpeg, download.Data);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(TestJpeg)).ToLowerInvariant(), download.Sha256);
                Assert.False(File.Exists(created.FilePath));
                Assert.Equal(OperationsWindowSnapshotLookupStatus.NotFound,
                    service.TryTake(created.SnapshotId, out _));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void ExpiredSnapshotIsRejectedAndRemoved()
        {
            string root = NewRoot();
            DateTimeOffset now = new(2026, 8, 12, 4, 0, 0, TimeSpan.Zero);
            try
            {
                OperationsWindowSnapshotService service = new(
                    Path.Combine(root, "snapshots"), () => now, () => TestJpeg);
                OperationsWindowSnapshotResult created = service.Create();
                now = now.AddMinutes(6);

                Assert.Equal(OperationsWindowSnapshotLookupStatus.Expired,
                    service.TryTake(created.SnapshotId, out OperationsWindowSnapshotResult? result));
                Assert.Null(result);
                Assert.False(File.Exists(created.FilePath));
                Assert.Equal(OperationsWindowSnapshotLookupStatus.InvalidId,
                    service.TryTake("../private", out _));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void WindowSnapshotDownloadRequiresCompletedOwnedJobAndConsumesEvidence()
        {
            string root = NewRoot();
            try
            {
                using ECDsa keyA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using ECDsa keyB = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
                registry.Approve("device-a", "Phone A", Convert.ToBase64String(keyA.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                registry.Approve("device-b", "Phone B", Convert.ToBase64String(keyB.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore store = new(Path.Combine(root, "work.json"));
                OperationsWindowSnapshotService snapshots = new(
                    Path.Combine(root, "snapshots"), captureProvider: () => TestJpeg);
                OperationsJob job = store.CreateJob("ops.window.snapshot.capture", "device-a", "private reason",
                    JsonSerializer.SerializeToElement(new { }), "private-correlation");
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), store,
                    () => new { app = "ColorVision", version = "1.2.3" },
                    windowSnapshots: snapshots);
                string path = $"/ops/v1/jobs/{job.JobId}/window-snapshot";

                Assert.Equal(409, Send(router, keyA, "device-a", path).StatusCode);
                Assert.Equal(404, Send(router, keyB, "device-b", path).StatusCode);

                Assert.NotNull(store.DecideJob(job.JobId, "device-a", true, "approved", "decision"));
                OperationsWindowSnapshotResult created = snapshots.Create();
                string evidenceId = OperationsWindowSnapshotService.EvidencePrefix + created.SnapshotId;
                Assert.NotNull(store.LocalCoSign(job.JobId, true, evidenceId));
                Assert.NotNull(store.CompleteJob(job.JobId, true, evidenceId));

                OperationsApiResponse accepted = Send(router, keyA, "device-a", path);
                Assert.Equal(200, accepted.StatusCode);
                Assert.Equal("image/jpeg", accepted.ContentType);
                Assert.Equal(TestJpeg, accepted.BodyBytes);
                Assert.Equal(created.Sha256, accepted.Headers["X-CV-Content-SHA256"]);
                Assert.Null(store.GetJobForDevice(job.JobId, "device-a")?.ResultEvidenceId);
                Assert.Contains(store.GetAudit(), item => item.Action == "window.snapshot.download");
                Assert.Contains(store.GetAudit(), item => item.Action == "job.evidence.consume");

                Assert.Equal(404, Send(router, keyA, "device-a", path).StatusCode);
                Assert.False(File.Exists(created.FilePath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static OperationsApiResponse Send(
            OperationsSecureApiRouter router, ECDsa key, string deviceId, string path)
        {
            return router.Handle(new OperationsSecureRequest
            {
                Method = "GET",
                Path = path,
                Headers = Sign(key, deviceId, "GET", path, []),
            });
        }

        private static Dictionary<string, string> Sign(
            ECDsa key, string deviceId, string method, string path, byte[] body)
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            string digest = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
            string canonical = OperationsRequestAuthenticator.BuildCanonical(method, path, timestamp, nonce, digest);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-CV-Device-Id"] = deviceId,
                ["X-CV-Timestamp"] = timestamp,
                ["X-CV-Nonce"] = nonce,
                ["X-CV-Signature"] = Convert.ToBase64String(key.SignData(
                    Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence)),
            };
        }

        private static string NewRoot() => Path.Combine(
            Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"));

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
