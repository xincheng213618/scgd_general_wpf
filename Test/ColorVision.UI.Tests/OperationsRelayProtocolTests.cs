using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests;

public sealed class OperationsRelayProtocolTests
{
    [Fact]
    public void RelayUsesAPrivacyPreservingHostLabel()
    {
        Assert.Equal("ColorVision 工作站", OperationsRelayClientService.SafeHostDisplayName);
        Assert.DoesNotContain(Environment.MachineName,
            OperationsRelayClientService.SafeHostDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RelaySnapshotAddsOnlyTheBoundedLiveMonitorToTheSafeHostStatus()
    {
        DateTimeOffset capturedAt = new(2026, 8, 13, 8, 0, 0, TimeSpan.Zero);
        OperationsLiveMonitorSnapshot monitor = OperationsLiveMonitorSnapshotFactory.Create(
            new OperationsFlowRuntimeStatus { Available = true, Phase = "running", IsActive = true },
            new OperationsRuntimePerformanceSnapshot
            {
                CapturedAt = capturedAt,
                CpuPercent = 7.5,
                MainUi = new OperationsUiResponsivenessSnapshot
                {
                    Available = true,
                    State = "responsive",
                    LatencyMilliseconds = 12,
                },
            },
            [new OperationsAlert
            {
                AlertId = "private-alert-id",
                Severity = "warning",
                Summary = "private alert text",
                OccurredAt = capturedAt,
            }],
            OperationsDeviceHealthSnapshot.CreateUnavailable(),
            capturedAt,
            OperationsMessageChannelHealthSnapshot.CreateUnavailable(),
            new OperationsApplicationRecoveryStatus { Supported = true, Registered = true });
        OperationsRelaySnapshot snapshot = OperationsRelaySnapshotFactory.Create(new
        {
            app = "ColorVision",
            version = "1.2.3",
            isRunning = true,
            uptimeSeconds = 60,
            privateConfiguration = "must-not-leak",
            process = new { memoryMb = 128, processId = 1234 },
            mainWindow = new { exists = true, state = "Normal", isVisible = true, title = "private" },
            secureOperations = new { isRunning = true, pairedDeviceCount = 1, relayConfigured = true, relayRunning = true },
        }, monitor, capturedAt);

        string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("monitor", out JsonElement signedMonitor));
        Assert.Equal("running", signedMonitor.GetProperty("flow").GetProperty("phase").GetString());
        Assert.Equal(1, signedMonitor.GetProperty("alerts").GetProperty("count").GetInt32());
        Assert.DoesNotContain("privateConfiguration", json, StringComparison.Ordinal);
        Assert.DoesNotContain("processId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-alert-id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private alert text", json, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(json) < 65_536);
    }

    [Fact]
    public void HostEnvelopeCanonicalSeparatesKindFromExactJsonBody()
    {
        Assert.Equal("colorvision-relay-snapshot-v1\n{\"status\":\"online\"}",
            OperationsRelayProtocol.BuildHostEnvelopeCanonical(
                OperationsRelayProtocol.HostSnapshotEnvelopePrefix,
                "{\"status\":\"online\"}"));
        Assert.NotEqual(
            OperationsRelayProtocol.BuildHostEnvelopeCanonical(
                OperationsRelayProtocol.HostSnapshotEnvelopePrefix, "{}"),
            OperationsRelayProtocol.BuildHostEnvelopeCanonical(
                OperationsRelayProtocol.HostReceiptEnvelopePrefix, "{}"));
    }

    [Fact]
    public void VerifiesPairedDeviceTaskAndRejectsTampering()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve(
                "device-1", "Test phone",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["ops.window.control", "ops.jobs.create"]);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string body = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.window.show",
                payload = new { },
                idempotencyKey = "show-1",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask task = CreateTask(
                key, body, "ops.window.show", now, now.AddMinutes(5));

            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                task, "host-1", registry, now,
                out OperationsRelayVerifiedTask? verified, out string error), error);
            Assert.NotNull(verified);
            Assert.Equal("device-1", verified.Device.DeviceId);
            Assert.Equal("ops.window.show", verified.CapabilityId);

            OperationsRelayDeviceTask tampered = Copy(task, task.RequestBody.Replace("show-1", "show-2", StringComparison.Ordinal));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                tampered, "host-1", registry, now, out _, out error));
            Assert.Equal("invalid_request_signature", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsExpiredOrRevokedDeviceTask()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve(
                "device-1", "Test phone",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["ops.window.control"]);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string body = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.window.show",
                payload = new { },
                idempotencyKey = "show-1",
            });
            OperationsRelayDeviceTask expired = CreateTask(
                key, body, "ops.window.show", now.AddMinutes(-20), now.AddMinutes(30));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                expired, "host-1", registry, now, out _, out string error));
            Assert.Equal("expired_task_envelope", error);

            OperationsRelayDeviceTask active = CreateTask(
                key, body, "ops.window.show", now, now.AddMinutes(5));
            Assert.True(registry.Revoke("device-1"));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                active, "host-1", registry, now, out _, out error));
            Assert.Equal("unknown_or_revoked_device", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WindowMinimizeRequiresTheWindowScopeAndAnEmptySignedPayload()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve(
                "device-1", "Test phone",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["ops.window.control"]);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string body = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.window.minimize",
                payload = new { },
                idempotencyKey = "minimize-1",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask task = CreateTask(
                key, body, "ops.window.minimize", now, now.AddMinutes(5));

            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                task, "host-1", registry, now,
                out OperationsRelayVerifiedTask? verified, out string error), error);
            Assert.NotNull(verified);
            Assert.Equal("ops.window.minimize", verified.CapabilityId);
            Assert.Equal("minimize-1", verified.IdempotencyKey);

            string payloadBody = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.window.minimize",
                payload = new { title = "other-window" },
                idempotencyKey = "minimize-2",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask payloadTask = CreateTask(
                key, payloadBody, "ops.window.minimize", now, now.AddMinutes(5));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                payloadTask, "host-1", registry, now, out _, out error));
            Assert.Equal("window_minimize_payload_not_allowed", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MessageReconnectRequiresTheJobScopeAndAnEmptySignedPayload()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve(
                "device-1", "Test phone",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["ops.jobs.create"]);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string body = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.messaging.reconnect",
                payload = new { },
                idempotencyKey = "reconnect-1",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask task = CreateTask(
                key, body, "ops.messaging.reconnect", now, now.AddMinutes(5));

            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                task, "host-1", registry, now,
                out OperationsRelayVerifiedTask? verified, out string error), error);
            Assert.NotNull(verified);
            Assert.Equal("ops.messaging.reconnect", verified.CapabilityId);
            Assert.Equal("reconnect-1", verified.IdempotencyKey);

            string payloadBody = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.messaging.reconnect",
                payload = new { endpoint = "other-broker" },
                idempotencyKey = "reconnect-2",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask payloadTask = CreateTask(
                key, payloadBody, "ops.messaging.reconnect", now, now.AddMinutes(5));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                payloadTask, "host-1", registry, now, out _, out error));
            Assert.Equal("message_reconnect_payload_not_allowed", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FlowCancellationRequiresTheJobScopeAndAnEmptySignedPayload()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve(
                "device-1", "Test phone",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["ops.jobs.create"]);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string body = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.flow.cancel",
                payload = new { },
                idempotencyKey = "cancel-1",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask task = CreateTask(
                key, body, "ops.flow.cancel", now, now.AddMinutes(5));

            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                task, "host-1", registry, now,
                out OperationsRelayVerifiedTask? verified, out string error), error);
            Assert.NotNull(verified);
            Assert.Equal("ops.flow.cancel", verified.CapabilityId);
            Assert.Equal("cancel-1", verified.IdempotencyKey);

            string payloadBody = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.flow.cancel",
                payload = new { flowId = "remote-selection" },
                idempotencyKey = "cancel-2",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask payloadTask = CreateTask(
                key, payloadBody, "ops.flow.cancel", now, now.AddMinutes(5));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                payloadTask, "host-1", registry, now, out _, out error));
            Assert.Equal("flow_cancel_payload_not_allowed", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplicationRestartRequiresTheJobScopeAndAnEmptySignedPayload()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve(
                "device-1", "Test phone",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["ops.jobs.create"]);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string body = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.application.restart",
                payload = new { },
                idempotencyKey = "restart-1",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask task = CreateTask(
                key, body, "ops.application.restart", now, now.AddMinutes(5));

            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                task, "host-1", registry, now,
                out OperationsRelayVerifiedTask? verified, out string error), error);
            Assert.NotNull(verified);
            Assert.Equal("ops.application.restart", verified.CapabilityId);
            Assert.Equal("restart-1", verified.IdempotencyKey);

            string payloadBody = JsonSerializer.Serialize(new
            {
                hostId = "host-1",
                capabilityId = "ops.application.restart",
                payload = new { executablePath = "other.exe" },
                idempotencyKey = "restart-2",
                ttlSeconds = 300,
            });
            OperationsRelayDeviceTask payloadTask = CreateTask(
                key, payloadBody, "ops.application.restart", now, now.AddMinutes(5));
            Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
                payloadTask, "host-1", registry, now, out _, out error));
            Assert.Equal("application_restart_payload_not_allowed", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static OperationsRelayDeviceTask CreateTask(
        ECDsa key,
        string body,
        string capabilityId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        string timestamp = createdAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string canonical = OperationsRelayProtocol.BuildCanonical(
            "POST", OperationsRelayProtocol.DeviceTaskPath, timestamp, nonce, Encoding.UTF8.GetBytes(body));
        string signature = Convert.ToBase64String(key.SignData(
            Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));
        return new OperationsRelayDeviceTask
        {
            TaskId = "task-1",
            CapabilityId = capabilityId,
            IdempotencyKey = "show-1",
            RequestBody = body,
            DeviceId = "device-1",
            Timestamp = timestamp,
            Nonce = nonce,
            Signature = signature,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }

    private static OperationsRelayDeviceTask Copy(OperationsRelayDeviceTask source, string requestBody) => new()
    {
        TaskId = source.TaskId,
        CapabilityId = source.CapabilityId,
        IdempotencyKey = source.IdempotencyKey,
        RequestBody = requestBody,
        DeviceId = source.DeviceId,
        Timestamp = source.Timestamp,
        Nonce = source.Nonce,
        Signature = source.Signature,
        CreatedAt = source.CreatedAt,
        ExpiresAt = source.ExpiresAt,
    };
}
