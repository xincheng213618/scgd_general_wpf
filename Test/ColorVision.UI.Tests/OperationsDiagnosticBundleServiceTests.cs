using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsDiagnosticBundleServiceTests
    {
        [Fact]
        public void SafeSnapshotUsesAnAllowlistInsteadOfForwardingLanIdentityFields()
        {
            OperationsSafeSnapshot snapshot = OperationsSafeSnapshotFactory.Create(new
            {
                app = "ColorVision",
                version = "1.2.3",
                machine = "private-machine",
                user = "private-user",
                endpoint = "https://10.0.0.8:8788",
                selectedAddress = "10.0.0.8",
                addresses = new[] { "10.0.0.8" },
                isRunning = true,
                uptimeSeconds = 42,
                process = new { id = 1234, name = "private-process", memoryMb = 12.5 },
                mainWindow = new { exists = true, title = "private-title", state = "Normal", isVisible = true },
                secureOperations = new
                {
                    isRunning = true,
                    endpoint = "https://10.0.0.8:8788",
                    pairedDeviceCount = 2,
                    relayConfigured = true,
                    relayRunning = false,
                    relayStatus = "private-relay-status",
                },
            });

            string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            Assert.DoesNotContain("private-machine", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-user", json, StringComparison.Ordinal);
            Assert.DoesNotContain("10.0.0.8", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-process", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-title", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-relay-status", json, StringComparison.Ordinal);
            Assert.Equal("1.2.3", snapshot.Version);
            Assert.Equal(12.5, snapshot.Process.MemoryMb);
            Assert.True(snapshot.MainWindow.IsVisible);
            Assert.Equal(2, snapshot.SecureOperations.PairedDeviceCount);
        }

        [Fact]
        public void DiagnosticBundleContainsOnlyBoundedRedactedEvidenceAndExpires()
        {
            string root = NewRoot();
            DateTimeOffset now = new(2026, 8, 12, 2, 30, 0, TimeSpan.Zero);
            try
            {
                OperationsWorkStore store = new(Path.Combine(root, "work.json"));
                store.RecordAudit("private-device-id", "device", "test.action", "private-target-id",
                    "completed", "private-correlation-id");
                OperationsDiagnosticBundleService service = new(
                    store, Path.Combine(root, "bundles"), () => now);
                OperationsDiagnosticBundleResult created = service.Create(
                    () => new
                    {
                        app = "ColorVision",
                        version = "1.2.3",
                        machine = "private-machine",
                        user = "private-user",
                        endpoint = "https://10.0.0.8:8788",
                        process = new { id = 9123, name = "private-process", memoryMb = 24.5 },
                        mainWindow = new { exists = true, title = "private-title", state = "Normal", isVisible = true },
                        secureOperations = new { isRunning = true, pairedDeviceCount = 1, relayConfigured = false, relayRunning = false },
                    },
                    new OperationsLogDigest
                    {
                        Available = true,
                        ParsedEventCount = 1,
                        ErrorCount = 1,
                        RecentEvents =
                        [
                            new OperationsAlert
                            {
                                Severity = "error",
                                Source = "application",
                                Summary = "bounded redacted event",
                                OccurredAt = now,
                            },
                        ],
                    },
                    HealthyServices(now));

                {
                    using ZipArchive archive = ZipFile.OpenRead(created.FilePath);
                    string[] names = archive.Entries.Select(item => item.FullName).OrderBy(item => item).ToArray();
                    Assert.Equal(new[]
                    {
                        "host-snapshot.json",
                        "manifest.json",
                        "operations-audit.json",
                        "recent-events.json",
                        "runtime.json",
                        "service-health.json",
                    }, names);
                    string allText = string.Join("\n", archive.Entries.Select(ReadText));
                    Assert.Contains("bounded redacted event", allText, StringComparison.Ordinal);
                    Assert.Contains("test.action", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-device-id", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-target-id", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-correlation-id", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-machine", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-user", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("10.0.0.8", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-process", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("private-title", allText, StringComparison.Ordinal);
                    Assert.DoesNotContain("9123", allText, StringComparison.Ordinal);
                }

                Assert.Equal(OperationsDiagnosticBundleLookupStatus.Available,
                    service.TryRead(created.BundleId, out OperationsDiagnosticBundleResult? download));
                Assert.NotNull(download);
                Assert.InRange(download.Data.Length, 1, OperationsDiagnosticBundleService.MaximumDownloadBytes);
                Assert.Equal(created.Sha256, download.Sha256);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(download.Data)).ToLowerInvariant(), download.Sha256);

                File.SetLastWriteTimeUtc(created.FilePath, now.AddHours(-25).UtcDateTime);
                Assert.Equal(OperationsDiagnosticBundleLookupStatus.Expired,
                    service.TryRead(created.BundleId, out _));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void DiagnosticBundleRejectsLegacyFormatBeforeReturningBytes()
        {
            string root = NewRoot();
            try
            {
                string directory = Path.Combine(root, "bundles");
                Directory.CreateDirectory(directory);
                string bundleId = Guid.NewGuid().ToString("N");
                string path = Path.Combine(directory, $"colorvision-diagnostics-{bundleId}.zip");
                using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry manifest = archive.CreateEntry("manifest.json");
                    using StreamWriter writer = new(manifest.Open(), Encoding.UTF8);
                    writer.Write("{\"schemaVersion\":\"1.0\"}");
                }

                OperationsDiagnosticBundleService service = new(
                    new OperationsWorkStore(Path.Combine(root, "work.json")), directory);
                Assert.Equal(OperationsDiagnosticBundleLookupStatus.UnsupportedFormat,
                    service.TryRead(bundleId, out OperationsDiagnosticBundleResult? result));
                Assert.Null(result);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void DiagnosticBundleDownloadRequiresCompletedOwnedJobAndReturnsVerifiedZip()
        {
            string root = NewRoot();
            string devicePath = Path.Combine(root, "devices.json");
            string workPath = Path.Combine(root, "work.json");
            try
            {
                using ECDsa keyA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using ECDsa keyB = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-a", "Phone A", Convert.ToBase64String(keyA.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                registry.Approve("device-b", "Phone B", Convert.ToBase64String(keyB.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore store = new(workPath);
                OperationsDiagnosticBundleService bundles = new(store, Path.Combine(root, "bundles"));
                OperationsJob job = store.CreateJob("ops.diagnostics.bundle.create", "device-a", "private reason",
                    JsonSerializer.SerializeToElement(new { }), "private-correlation");
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), store,
                    () => new { app = "ColorVision", version = "1.2.3" },
                    diagnosticBundles: bundles);
                string path = $"/ops/v1/jobs/{job.JobId}/diagnostic-bundle";

                OperationsApiResponse notReady = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(keyA, "device-a", "GET", path, []),
                });
                Assert.Equal(409, notReady.StatusCode);

                OperationsApiResponse foreign = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(keyB, "device-b", "GET", path, []),
                });
                Assert.Equal(404, foreign.StatusCode);

                Assert.NotNull(store.DecideJob(job.JobId, "device-a", true, "approved", "decision"));
                OperationsDiagnosticBundleResult bundle = bundles.Create(
                    () => new { app = "ColorVision", version = "1.2.3" },
                    new OperationsLogDigest(), HealthyServices(DateTimeOffset.UtcNow));
                Assert.NotNull(store.LocalCoSign(job.JobId, true, bundle.BundleId));
                Assert.NotNull(store.CompleteJob(job.JobId, true, bundle.BundleId));

                OperationsApiResponse accepted = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(keyA, "device-a", "GET", path, []),
                });
                Assert.Equal(200, accepted.StatusCode);
                Assert.Equal("application/zip", accepted.ContentType);
                Assert.NotNull(accepted.BodyBytes);
                Assert.Equal(bundle.Sha256, accepted.Headers["X-CV-Content-SHA256"]);
                Assert.Equal(bundle.Sha256,
                    Convert.ToHexString(SHA256.HashData(accepted.BodyBytes)).ToLowerInvariant());
                Assert.Contains(store.GetAudit(), item => item.Action == "diagnostic.bundle.download");

                const string jobsPath = "/ops/v1/jobs";
                OperationsApiResponse foreignJobs = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = jobsPath,
                    Headers = Sign(keyB, "device-b", "GET", jobsPath, []),
                });
                using JsonDocument jobsDocument = JsonDocument.Parse(foreignJobs.Body);
                Assert.Equal(0, jobsDocument.RootElement.GetProperty("data").GetProperty("count").GetInt32());
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static OperationsServiceHealthReport HealthyServices(DateTimeOffset observedAt) => new()
        {
            Available = true,
            AllHealthy = true,
            Services =
            [
                new OperationsServiceHealthItem
                {
                    ServiceId = OperationsServiceIds.MqttBroker,
                    Title = "MQTT 消息服务",
                    Status = "running",
                    Installed = true,
                    Healthy = true,
                    MaintenanceSupported = true,
                    StatusSource = "test-provider",
                    ObservedAt = observedAt,
                },
            ],
        };

        private static string ReadText(ZipArchiveEntry entry)
        {
            using StreamReader reader = new(entry.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static Dictionary<string, string> Sign(
            ECDsa key, string deviceId, string method, string path, byte[] body)
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
