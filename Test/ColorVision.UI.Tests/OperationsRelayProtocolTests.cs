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
