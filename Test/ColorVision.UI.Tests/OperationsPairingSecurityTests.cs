using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        [Fact]
        public void SecureRouterExecutesAndAuditsAllowedWindowAction()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-window", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore workStore = new(workPath);
                string executed = string.Empty;
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), workStore, () => new { healthy = true },
                    actionExecutor: actionId =>
                    {
                        executed = actionId;
                        return new OperationsActionResult(true, actionId, "done");
                    });
                const string path = "/ops/v1/actions/window/show";

                OperationsApiResponse response = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = path,
                    Headers = Sign(key, "device-window", "POST", path, []),
                });

                Assert.Equal(200, response.StatusCode);
                Assert.Equal(OperationsDesktopActionService.ShowWindowAction, executed);
                OperationsAuditEntry audit = Assert.Single(workStore.GetAudit());
                Assert.Equal("desktop.action.execute", audit.Action);
                Assert.Equal("completed", audit.Outcome);
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void ConnectionDiagnosticIsSignedScopedAndRedacted()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-diagnostics", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), new OperationsWorkStore(workPath), () => new { healthy = true },
                    runtimePerformance: new FixedRuntimePerformanceProvider(),
                    flowRuntimeStatus: new FixedFlowRuntimeStatusProvider(),
                    deviceHealthProvider: new FixedDeviceHealthProvider(),
                    messageChannelHealthProvider: new FixedMessageChannelHealthProvider());
                const string path = "/ops/v1/diagnostics/connection";

                OperationsApiResponse response = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(key, "device-diagnostics", "GET", path, []),
                });

                Assert.Equal(200, response.StatusCode);
                using JsonDocument document = JsonDocument.Parse(response.Body);
                JsonElement data = document.RootElement.GetProperty("data");
                Assert.Equal("ready", data.GetProperty("channel").GetString());
                Assert.True(data.GetProperty("serverUnixTimeMilliseconds").GetInt64() > 0);
                Assert.True(data.GetProperty("availableCapabilityCount").GetInt32() > 0);
                Assert.False(data.TryGetProperty("host", out _));
                Assert.False(data.TryGetProperty("user", out _));
                Assert.False(data.TryGetProperty("deviceId", out _));
                Assert.False(data.TryGetProperty("certificate", out _));
                Assert.True(data.GetProperty("deviceHealthAvailable").GetBoolean());
                Assert.Equal(3, data.GetProperty("configuredDeviceCount").GetInt32());
                Assert.Equal(1, data.GetProperty("readyDeviceCount").GetInt32());
                Assert.Equal(1, data.GetProperty("busyDeviceCount").GetInt32());
                Assert.Equal(1, data.GetProperty("deviceAttentionCount").GetInt32());
                Assert.Equal(1, data.GetProperty("offlineDeviceCount").GetInt32());
                Assert.Equal(0, data.GetProperty("uninitializedDeviceCount").GetInt32());
                Assert.Equal(0, data.GetProperty("unauthorizedDeviceCount").GetInt32());
                Assert.Equal(0, data.GetProperty("unclassifiedUnavailableDeviceCount").GetInt32());
                Assert.True(data.GetProperty("messageChannelAvailable").GetBoolean());
                Assert.Equal("connected", data.GetProperty("messageChannelState").GetString());
                Assert.True(data.GetProperty("messageChannelConnected").GetBoolean());
                Assert.True(data.GetProperty("messageChannelSubscriptionReady").GetBoolean());

                const string summaryPath = "/ops/v1/diagnostics/summary";
                OperationsApiResponse summary = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = summaryPath,
                    Headers = Sign(key, "device-diagnostics", "GET", summaryPath, []),
                });
                using JsonDocument summaryDocument = JsonDocument.Parse(summary.Body);
                Assert.False(summaryDocument.RootElement.GetProperty("data").TryGetProperty("host", out _));

                const string performancePath = "/ops/v1/diagnostics/performance";
                OperationsApiResponse performance = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = performancePath,
                    Headers = Sign(key, "device-diagnostics", "GET", performancePath, []),
                });
                Assert.Equal(200, performance.StatusCode);
                using JsonDocument performanceDocument = JsonDocument.Parse(performance.Body);
                JsonElement performanceData = performanceDocument.RootElement.GetProperty("data");
                Assert.InRange(performanceData.GetProperty("cpuPercent").GetDouble(), 0, 100);
                Assert.True(performanceData.GetProperty("workingSetMb").GetDouble() > 0);
                Assert.True(performanceData.GetProperty("threadCount").GetInt32() > 0);
                Assert.False(performanceData.TryGetProperty("processId", out _));
                Assert.DoesNotContain(Environment.MachineName, performance.Body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(Environment.UserName, performance.Body, StringComparison.OrdinalIgnoreCase);

                const string flowPath = "/ops/v1/flow/runtime";
                OperationsApiResponse flow = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = flowPath,
                    Headers = Sign(key, "device-diagnostics", "GET", flowPath, []),
                });
                Assert.Equal(200, flow.StatusCode);
                using JsonDocument flowDocument = JsonDocument.Parse(flow.Body);
                JsonElement flowData = flowDocument.RootElement.GetProperty("data");
                Assert.Equal("running", flowData.GetProperty("phase").GetString());
                Assert.True(flowData.GetProperty("isActive").GetBoolean());
                Assert.True(flowData.GetProperty("cancelAvailable").GetBoolean());
                Assert.Equal(37.5, flowData.GetProperty("progressPercent").GetDouble());
                Assert.False(flowData.TryGetProperty("flowName", out _));
                Assert.False(flowData.TryGetProperty("templateId", out _));
                Assert.False(flowData.TryGetProperty("batchSerialNumber", out _));
                Assert.False(flowData.TryGetProperty("nodeName", out _));
                Assert.False(flowData.TryGetProperty("resultText", out _));

                const string deviceHealthPath = "/ops/v1/devices/health";
                OperationsApiResponse deviceHealth = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = deviceHealthPath,
                    Headers = Sign(key, "device-diagnostics", "GET", deviceHealthPath, []),
                });
                Assert.Equal(200, deviceHealth.StatusCode);
                using JsonDocument deviceHealthDocument = JsonDocument.Parse(deviceHealth.Body);
                JsonElement deviceHealthData = deviceHealthDocument.RootElement.GetProperty("data");
                Assert.Equal(3, deviceHealthData.GetProperty("totalCount").GetInt32());
                Assert.Equal(1, deviceHealthData.GetProperty("readyCount").GetInt32());
                Assert.Equal(1, deviceHealthData.GetProperty("busyCount").GetInt32());
                Assert.Equal(1, deviceHealthData.GetProperty("unavailableCount").GetInt32());
                Assert.Equal(1, deviceHealthData.GetProperty("attentionCount").GetInt32());
                Assert.Equal(1, deviceHealthData.GetProperty("offlineCount").GetInt32());
                Assert.Equal(0, deviceHealthData.GetProperty("uninitializedCount").GetInt32());
                Assert.Equal(0, deviceHealthData.GetProperty("unauthorizedCount").GetInt32());
                Assert.Equal(0, deviceHealthData.GetProperty("unclassifiedUnavailableCount").GetInt32());
                JsonElement deviceCategory = Assert.Single(deviceHealthData.GetProperty("categories").EnumerateArray());
                Assert.Equal("camera", deviceCategory.GetProperty("category").GetString());
                Assert.False(deviceCategory.TryGetProperty("deviceName", out _));
                Assert.False(deviceCategory.TryGetProperty("code", out _));
                Assert.False(deviceCategory.TryGetProperty("topic", out _));
                Assert.False(deviceCategory.TryGetProperty("address", out _));
                Assert.False(deviceCategory.TryGetProperty("offlineCount", out _));
                Assert.False(deviceCategory.TryGetProperty("unauthorizedCount", out _));

                const string messageChannelPath = "/ops/v1/messaging/health";
                OperationsApiResponse messageChannel = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = messageChannelPath,
                    Headers = Sign(key, "device-diagnostics", "GET", messageChannelPath, []),
                });
                Assert.Equal(200, messageChannel.StatusCode);
                using JsonDocument messageChannelDocument = JsonDocument.Parse(messageChannel.Body);
                JsonElement messageChannelData = messageChannelDocument.RootElement.GetProperty("data");
                Assert.Equal("connected", messageChannelData.GetProperty("state").GetString());
                Assert.Equal(6, messageChannelData.GetProperty("registeredSubscriptionCount").GetInt32());
                Assert.Equal(6, messageChannelData.GetProperty("activeSubscriptionCount").GetInt32());
                Assert.False(messageChannelData.TryGetProperty("host", out _));
                Assert.False(messageChannelData.TryGetProperty("port", out _));
                Assert.False(messageChannelData.TryGetProperty("topic", out _));
                Assert.False(messageChannelData.TryGetProperty("payload", out _));
                Assert.False(messageChannelData.TryGetProperty("username", out _));
                Assert.False(messageChannelData.TryGetProperty("password", out _));

                const string monitorPath = "/ops/v1/monitor";
                OperationsApiResponse monitor = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = monitorPath,
                    Headers = Sign(key, "device-diagnostics", "GET", monitorPath, []),
                });
                Assert.Equal(200, monitor.StatusCode);
                using JsonDocument monitorDocument = JsonDocument.Parse(monitor.Body);
                JsonElement monitorData = monitorDocument.RootElement.GetProperty("data");
                Assert.Equal("running", monitorData.GetProperty("flow").GetProperty("phase").GetString());
                Assert.Equal(9.5, monitorData.GetProperty("performance").GetProperty("cpuPercent").GetDouble());
                Assert.Equal(1, monitorData.GetProperty("devices").GetProperty("attentionCount").GetInt32());
                Assert.Equal(1, monitorData.GetProperty("devices").GetProperty("offlineCount").GetInt32());
                Assert.Equal("connected", monitorData.GetProperty("messageChannel").GetProperty("state").GetString());
                Assert.Equal(10, monitorData.GetProperty("suggestedRefreshSeconds").GetInt32());
                Assert.False(monitorData.GetProperty("alerts").TryGetProperty("items", out _));
                Assert.DoesNotContain(Environment.MachineName, monitor.Body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(Environment.UserName, monitor.Body, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void SecureSnapshotAndAuditEndpointsReturnOnlyAllowlistedSummaries()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-safe-snapshot", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore store = new(workPath);
                for (int index = 0; index < 35; index++)
                {
                    store.RecordAudit($"private-device-id-{index}", "device", "test.action",
                        $"private-target-id-{index}", "completed", $"private-correlation-id-{index}");
                }
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), store, () => new
                    {
                        app = "ColorVision",
                        version = "1.2.3",
                        machine = "private-machine",
                        user = "private-user",
                        endpoint = "https://10.0.0.8:8788",
                        addresses = new[] { "10.0.0.8" },
                        process = new { id = 9123, name = "private-process", memoryMb = 10.5 },
                        mainWindow = new { exists = true, title = "private-title", state = "Normal", isVisible = true },
                    });

                const string snapshotPath = "/ops/v1/snapshot";
                OperationsApiResponse snapshot = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = snapshotPath,
                    Headers = Sign(key, "device-safe-snapshot", "GET", snapshotPath, []),
                });
                Assert.Equal(200, snapshot.StatusCode);
                Assert.DoesNotContain("private-machine", snapshot.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-user", snapshot.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("10.0.0.8", snapshot.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-process", snapshot.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-title", snapshot.Body, StringComparison.Ordinal);

                const string auditPath = "/ops/v1/audit";
                OperationsApiResponse audit = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = auditPath,
                    Headers = Sign(key, "device-safe-snapshot", "GET", auditPath, []),
                });
                Assert.Equal(200, audit.StatusCode);
                Assert.Contains("test.action", audit.Body, StringComparison.Ordinal);
                using JsonDocument auditDocument = JsonDocument.Parse(audit.Body);
                JsonElement auditData = auditDocument.RootElement.GetProperty("data");
                Assert.Equal(30, auditData.GetProperty("count").GetInt32());
                Assert.Equal(30, auditData.GetProperty("entries").GetArrayLength());
                Assert.True(auditData.TryGetProperty("generatedAt", out _));
                Assert.DoesNotContain("private-device-id", audit.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-target-id", audit.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-correlation-id", audit.Body, StringComparison.Ordinal);
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void RecentEventDigestIsSignedBoundedAndRedacted()
        {
            string devicePath = CreateStorePath();
            string root = Path.GetDirectoryName(devicePath)!;
            string workPath = Path.Combine(root, "work.json");
            string logDirectory = Path.Combine(root, "log");
            Directory.CreateDirectory(logDirectory);
            try
            {
                File.WriteAllLines(Path.Combine(logDirectory, "20260713.txt"),
                [
                    "2026-07-13 10:00:00,000 [1] INFO  Host - ready",
                    "2026-07-13 10:01:00,000 [1] ERROR OperationsSecureHostService - token=visible 10.0.0.8 user@example.com",
                ]);
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-events", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), new OperationsWorkStore(workPath), () => new { healthy = true },
                    alerts: new OperationsAlertService(logDirectory));
                const string path = "/ops/v1/diagnostics/recent-events";

                OperationsApiResponse response = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(key, "device-events", "GET", path, []),
                });

                Assert.Equal(200, response.StatusCode);
                Assert.DoesNotContain("visible", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("10.0.0.8", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("user@example.com", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("20260713.txt", response.Body, StringComparison.Ordinal);
                using JsonDocument document = JsonDocument.Parse(response.Body);
                JsonElement data = document.RootElement.GetProperty("data");
                Assert.True(data.GetProperty("available").GetBoolean());
                Assert.Equal(1, data.GetProperty("errorCount").GetInt32());
                Assert.Equal(1, data.GetProperty("recentEvents").GetArrayLength());
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void TriageReportUsesExistingDiagnosticScopeAndReturnsOnlyFixedActions()
        {
            string devicePath = CreateStorePath();
            string root = Path.GetDirectoryName(devicePath)!;
            string workPath = Path.Combine(root, "work.json");
            string logDirectory = Path.Combine(root, "log");
            Directory.CreateDirectory(logDirectory);
            try
            {
                File.WriteAllLines(Path.Combine(logDirectory, "20260713.txt"),
                [
                    "2026-07-13 10:00:00,000 [1] ERROR Broker - endpoint 10.0.0.8 token=visible",
                ]);
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-triage", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), new OperationsWorkStore(workPath), () => new { healthy = true },
                    alerts: new OperationsAlertService(logDirectory),
                    serviceHealthProvider: new FixedServiceHealthProvider("stopped", healthy: false, maintenanceSupported: true));
                const string path = "/ops/v1/triage";

                OperationsApiResponse response = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(key, "device-triage", "GET", path, []),
                });

                Assert.Equal(200, response.StatusCode);
                Assert.DoesNotContain("visible", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("10.0.0.8", response.Body, StringComparison.Ordinal);
                using JsonDocument document = JsonDocument.Parse(response.Body);
                JsonElement findings = document.RootElement.GetProperty("data").GetProperty("findings");
                JsonElement restart = findings.EnumerateArray()
                    .SelectMany(item => item.GetProperty("actions").EnumerateArray())
                    .Single(item => item.GetProperty("actionId").GetString() == OperationsTriageActionIds.RequestMqttRestart);
                Assert.False(restart.GetProperty("requiresLocalCoSign").GetBoolean());
                Assert.Equal(OperationsRiskLevels.Privileged, restart.GetProperty("riskLevel").GetString());
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void ServiceHealthIsSignedScopedAndContainsOnlyAllowlistedNormalizedState()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-health", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), new OperationsWorkStore(workPath), () => new { healthy = true },
                    serviceHealthProvider: new FixedServiceHealthProvider("running", healthy: true, maintenanceSupported: true));
                const string path = "/ops/v1/services/health";

                OperationsApiResponse response = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(key, "device-health", "GET", path, []),
                });

                Assert.Equal(200, response.StatusCode);
                using JsonDocument document = JsonDocument.Parse(response.Body);
                JsonElement data = document.RootElement.GetProperty("data");
                Assert.True(data.GetProperty("available").GetBoolean());
                JsonElement service = Assert.Single(data.GetProperty("services").EnumerateArray());
                Assert.Equal(OperationsServiceIds.MqttBroker, service.GetProperty("serviceId").GetString());
                Assert.Equal("running", service.GetProperty("status").GetString());
                Assert.False(service.TryGetProperty("serviceName", out _));
                Assert.False(service.TryGetProperty("path", out _));
                Assert.False(service.TryGetProperty("account", out _));
                Assert.False(service.TryGetProperty("commandLine", out _));
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void JobApiReturnsSafeTimelineInsteadOfInternalActorsInputOrEvidenceIds()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-job-safe", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore workStore = new(workPath);
                workStore.CreateJob("ops.diagnostics.bundle.create", "device-job-safe", "private reason",
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto", secret = "private-input" }), "private-correlation");
                workStore.CreateJob("ops.service.restart", "foreign-device-id", "foreign reason",
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto" }), "foreign-correlation");
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), workStore, () => new { healthy = true });
                const string path = "/ops/v1/jobs";

                OperationsApiResponse response = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = path,
                    Headers = Sign(key, "device-job-safe", "GET", path, []),
                });

                Assert.Equal(200, response.StatusCode);
                Assert.DoesNotContain("device-job-safe", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("foreign-device-id", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private reason", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-input", response.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private-correlation", response.Body, StringComparison.Ordinal);
                using JsonDocument document = JsonDocument.Parse(response.Body);
                JsonElement job = Assert.Single(document.RootElement.GetProperty("data").GetProperty("jobs").EnumerateArray());
                Assert.True(job.TryGetProperty("timeline", out JsonElement timeline));
                Assert.Equal(4, timeline.GetArrayLength());
                Assert.True(job.TryGetProperty("evidence", out _));
                Assert.False(job.TryGetProperty("requestedByDeviceId", out _));
                Assert.False(job.TryGetProperty("input", out _));
                Assert.False(job.TryGetProperty("resultEvidenceId", out _));
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void SupportApiIsDeviceIsolatedAndRequiresLocalConsentBeforeMessages()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa keyA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using ECDsa keyB = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-support-a", "Phone A", Convert.ToBase64String(keyA.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                registry.Approve("device-support-b", "Phone B", Convert.ToBase64String(keyB.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore workStore = new(workPath);
                OperationsSupportSession session = workStore.RequestSupport(
                    "device-support-a", "guided", "private support reason", 15, "private-request-correlation");
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), workStore, () => new { healthy = true });

                const string sessionsPath = "/ops/v1/support-sessions";
                OperationsApiResponse ownSessions = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = sessionsPath,
                    Headers = Sign(keyA, "device-support-a", "GET", sessionsPath, []),
                });
                Assert.Equal(200, ownSessions.StatusCode);
                Assert.DoesNotContain("device-support-a", ownSessions.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("private support reason", ownSessions.Body, StringComparison.Ordinal);
                using JsonDocument ownDocument = JsonDocument.Parse(ownSessions.Body);
                Assert.Equal(1, ownDocument.RootElement.GetProperty("data").GetProperty("count").GetInt32());

                OperationsApiResponse foreignSessions = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = sessionsPath,
                    Headers = Sign(keyB, "device-support-b", "GET", sessionsPath, []),
                });
                using JsonDocument foreignDocument = JsonDocument.Parse(foreignSessions.Body);
                Assert.Equal(0, foreignDocument.RootElement.GetProperty("data").GetProperty("count").GetInt32());

                string messagesPath = $"/ops/v1/support-sessions/{session.SessionId}/messages";
                byte[] messageBody = JsonSerializer.SerializeToUtf8Bytes(new { text = "bounded field note" });
                OperationsApiResponse beforeConsent = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = messagesPath,
                    Body = messageBody,
                    Headers = Sign(keyA, "device-support-a", "POST", messagesPath, messageBody),
                });
                Assert.Equal(409, beforeConsent.StatusCode);

                Assert.NotNull(workStore.LocalConsentSupport(session.SessionId, true));
                OperationsApiResponse accepted = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = messagesPath,
                    Body = messageBody,
                    Headers = Sign(keyA, "device-support-a", "POST", messagesPath, messageBody),
                });
                Assert.Equal(201, accepted.StatusCode);
                Assert.DoesNotContain("device-support-a", accepted.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("sourceTaskId", accepted.Body, StringComparison.OrdinalIgnoreCase);

                OperationsApiResponse foreignRead = router.Handle(new OperationsSecureRequest
                {
                    Method = "GET",
                    Path = messagesPath,
                    Headers = Sign(keyB, "device-support-b", "GET", messagesPath, []),
                });
                Assert.Equal(404, foreignRead.StatusCode);
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void FlowCancellationRejectsRemoteParametersAndExecutesOnceAfterMobileApproval()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-flow-cancel", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore workStore = new(workPath);
                RecordingFlowRuntimeController controller = new();
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), workStore, () => new { healthy = true },
                    flowRuntimeController: controller);
                const string jobsPath = "/ops/v1/jobs";

                byte[] rejectedBody = Encoding.UTF8.GetBytes(
                    "{\"capabilityId\":\"ops.flow.cancel\",\"input\":{\"flowId\":\"remote-value\"}}");
                OperationsApiResponse rejected = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = jobsPath,
                    Body = rejectedBody,
                    Headers = Sign(key, "device-flow-cancel", "POST", jobsPath, rejectedBody),
                });
                Assert.Equal(400, rejected.StatusCode);
                Assert.Empty(workStore.GetJobs());

                byte[] createBody = Encoding.UTF8.GetBytes(
                    "{\"capabilityId\":\"ops.flow.cancel\",\"reason\":\"confirmed\",\"input\":{}}");
                OperationsApiResponse created = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = jobsPath,
                    Body = createBody,
                    Headers = Sign(key, "device-flow-cancel", "POST", jobsPath, createBody),
                });
                Assert.Equal(202, created.StatusCode);
                using JsonDocument createdDocument = JsonDocument.Parse(created.Body);
                JsonElement createdJob = createdDocument.RootElement.GetProperty("data").GetProperty("job");
                string jobId = Assert.IsType<string>(createdJob.GetProperty("jobId").GetString());
                Assert.False(createdJob.GetProperty("requiresLocalCoSign").GetBoolean());

                string decisionPath = $"/ops/v1/jobs/{jobId}/decision";
                byte[] decisionBody = Encoding.UTF8.GetBytes("{\"approved\":true,\"reason\":\"confirmed\"}");
                OperationsApiResponse completed = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = decisionPath,
                    Body = decisionBody,
                    Headers = Sign(key, "device-flow-cancel", "POST", decisionPath, decisionBody),
                });
                Assert.Equal(200, completed.StatusCode);
                using JsonDocument completedDocument = JsonDocument.Parse(completed.Body);
                Assert.Equal("completed", completedDocument.RootElement.GetProperty("data")
                    .GetProperty("job").GetProperty("status").GetString());
                Assert.Equal(1, controller.RequestCount);

                OperationsApiResponse repeated = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = decisionPath,
                    Body = decisionBody,
                    Headers = Sign(key, "device-flow-cancel", "POST", decisionPath, decisionBody),
                });
                Assert.Equal(409, repeated.StatusCode);
                Assert.Equal(1, controller.RequestCount);
            }
            finally
            {
                DeleteStore(devicePath);
            }
        }

        [Fact]
        public void MqttRestartRejectsRemoteTargetsAndExecutesOnceAfterMobileApproval()
        {
            string devicePath = CreateStorePath();
            string workPath = Path.Combine(Path.GetDirectoryName(devicePath)!, "work.json");
            try
            {
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                OperationsDeviceRegistry registry = new(devicePath);
                registry.Approve("device-mqtt-restart", "Phone", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                    OperationsPairingService.InitialScopes);
                OperationsWorkStore workStore = new(workPath);
                RecordingMqttRestartController controller = new();
                OperationsSecureApiRouter router = new(new OperationsPairingService(registry),
                    new OperationsRequestAuthenticator(registry), workStore, () => new { healthy = true },
                    mqttRestartController: controller);
                const string jobsPath = "/ops/v1/jobs";

                byte[] rejectedBody = Encoding.UTF8.GetBytes(
                    "{\"capabilityId\":\"ops.service.restart\",\"input\":{\"serviceId\":\"mosquitto\",\"command\":\"restart\"}}");
                OperationsApiResponse rejected = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = jobsPath,
                    Body = rejectedBody,
                    Headers = Sign(key, "device-mqtt-restart", "POST", jobsPath, rejectedBody),
                });
                Assert.Equal(400, rejected.StatusCode);
                Assert.Empty(workStore.GetJobs());

                byte[] createBody = Encoding.UTF8.GetBytes(
                    "{\"capabilityId\":\"ops.service.restart\",\"reason\":\"confirmed\",\"input\":{\"serviceId\":\"mosquitto\"}}");
                OperationsApiResponse created = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = jobsPath,
                    Body = createBody,
                    Headers = Sign(key, "device-mqtt-restart", "POST", jobsPath, createBody),
                });
                Assert.Equal(202, created.StatusCode);
                using JsonDocument createdDocument = JsonDocument.Parse(created.Body);
                JsonElement createdJob = createdDocument.RootElement.GetProperty("data").GetProperty("job");
                string jobId = Assert.IsType<string>(createdJob.GetProperty("jobId").GetString());
                Assert.False(createdJob.GetProperty("requiresLocalCoSign").GetBoolean());

                string decisionPath = $"/ops/v1/jobs/{jobId}/decision";
                byte[] decisionBody = Encoding.UTF8.GetBytes("{\"approved\":true,\"reason\":\"confirmed\"}");
                OperationsApiResponse completed = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = decisionPath,
                    Body = decisionBody,
                    Headers = Sign(key, "device-mqtt-restart", "POST", decisionPath, decisionBody),
                });
                Assert.Equal(200, completed.StatusCode);
                using JsonDocument completedDocument = JsonDocument.Parse(completed.Body);
                JsonElement completedJob = completedDocument.RootElement.GetProperty("data").GetProperty("job");
                Assert.Equal("completed", completedJob.GetProperty("status").GetString());
                Assert.Equal("service-host-receipt", completedJob.GetProperty("evidence").GetProperty("kind").GetString());
                Assert.Equal(1, controller.RestartCount);

                OperationsApiResponse repeated = router.Handle(new OperationsSecureRequest
                {
                    Method = "POST",
                    Path = decisionPath,
                    Body = decisionBody,
                    Headers = Sign(key, "device-mqtt-restart", "POST", decisionPath, decisionBody),
                });
                Assert.Equal(409, repeated.StatusCode);
                Assert.Equal(1, controller.RestartCount);
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

        private sealed class FixedServiceHealthProvider(
            string status,
            bool healthy,
            bool maintenanceSupported) : IOperationsServiceHealthProvider
        {
            public OperationsServiceHealthReport Capture() => new()
            {
                Available = true,
                AllHealthy = healthy,
                Services =
                [
                    new OperationsServiceHealthItem
                    {
                        ServiceId = OperationsServiceIds.MqttBroker,
                        Title = "MQTT 消息服务",
                        Status = status,
                        Installed = true,
                        Healthy = healthy,
                        MaintenanceSupported = maintenanceSupported,
                        StatusSource = "test-provider",
                        ObservedAt = DateTimeOffset.UtcNow,
                    },
                ],
            };
        }

        private sealed class FixedFlowRuntimeStatusProvider : IOperationsFlowRuntimeStatusProvider
        {
            public OperationsFlowRuntimeStatus Capture() => new()
            {
                Available = true,
                HasConfiguredFlow = true,
                Phase = "running",
                IsActive = true,
                EngineRunning = true,
                ProgressAvailable = true,
                CancelAvailable = true,
                ProgressPercent = 37.5,
                ProgressIsHistoricalEstimate = true,
                ElapsedMilliseconds = 12000,
                LastRunStatus = "none",
                ObservedAt = DateTimeOffset.UtcNow,
            };
        }

        private sealed class RecordingFlowRuntimeController : IOperationsFlowRuntimeController
        {
            public int RequestCount { get; private set; }

            public OperationsFlowCancelResult RequestCancelCurrentFlow()
            {
                RequestCount++;
                return new OperationsFlowCancelResult(true, "flow_cancel_requested", "accepted");
            }
        }

        private sealed class RecordingMqttRestartController : IOperationsMqttRestartController
        {
            public int RestartCount { get; private set; }

            public OperationsMqttRestartResult Restart()
            {
                RestartCount++;
                return new OperationsMqttRestartResult(true, $"servicehost:request-{RestartCount}");
            }
        }

        private sealed class FixedDeviceHealthProvider : IOperationsDeviceHealthProvider
        {
            public OperationsDeviceHealthSnapshot Capture() => OperationsDeviceHealthSnapshotFactory.Create(
            [
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, OperationsDeviceStates.Ready),
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, OperationsDeviceStates.Busy),
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Unavailable,
                    OperationsDeviceUnavailableReasons.Offline),
            ]);
        }

        private sealed class FixedMessageChannelHealthProvider : IOperationsMessageChannelHealthProvider
        {
            public OperationsMessageChannelHealthSnapshot Capture() =>
                OperationsMessageChannelHealthSnapshotFactory.Create(
                    new OperationsMessageChannelObservation(true, true, 6, 6, DateTimeOffset.UtcNow));
        }

        private sealed class FixedRuntimePerformanceProvider : IOperationsRuntimePerformanceProvider
        {
            public OperationsRuntimePerformanceSnapshot Capture() => new()
            {
                CapturedAt = DateTimeOffset.UtcNow,
                SampleMilliseconds = 300,
                CpuPercent = 9.5,
                WorkingSetMb = 256,
                PrivateMemoryMb = 300,
                ManagedHeapMb = 24,
                ThreadCount = 18,
                HandleCount = 400,
                MainUi = new OperationsUiResponsivenessSnapshot
                {
                    Available = true,
                    State = "responsive",
                    LatencyMilliseconds = 12,
                },
            };
        }
    }
}
