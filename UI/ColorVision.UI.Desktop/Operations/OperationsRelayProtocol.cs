using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsRelayDeviceTask
    {
        public string TaskId { get; init; } = string.Empty;

        public string CapabilityId { get; init; } = string.Empty;

        public string IdempotencyKey { get; init; } = string.Empty;

        public string RequestBody { get; init; } = string.Empty;

        public string DeviceId { get; init; } = string.Empty;

        public string Timestamp { get; init; } = string.Empty;

        public string Nonce { get; init; } = string.Empty;

        public string Signature { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }
    }

    public sealed class OperationsRelayVerifiedTask
    {
        public string TaskId { get; init; } = string.Empty;

        public string CapabilityId { get; init; } = string.Empty;

        public string IdempotencyKey { get; init; } = string.Empty;

        public OperationsPairedDevice Device { get; init; } = new();

        public JsonElement Payload { get; init; }
    }

    public static class OperationsRelayProtocol
    {
        public const string DeviceTaskPath = "/api/ops/v1/device-relay/tasks";
        public const string HostSnapshotEnvelopePrefix = "colorvision-relay-snapshot-v1";
        public const string HostReceiptEnvelopePrefix = "colorvision-relay-receipt-v1";
        private static readonly TimeSpan AllowedCreatedAtSkew = TimeSpan.FromMinutes(2);

        public static string BuildCanonical(
            string method,
            string path,
            string timestamp,
            string nonce,
            ReadOnlySpan<byte> body)
        {
            string digest = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
            return string.Join('\n', method.ToUpperInvariant(), path, timestamp, nonce, digest);
        }

        public static string BuildHostEnvelopeCanonical(string prefix, string body) =>
            string.Join('\n', prefix, body);

        public static bool TryVerifyDeviceTask(
            OperationsRelayDeviceTask task,
            string expectedHostId,
            OperationsDeviceRegistry registry,
            DateTimeOffset now,
            out OperationsRelayVerifiedTask? verified,
            out string error)
        {
            verified = null;
            error = string.Empty;
            if (!IsSafeId(task.TaskId) || !IsSafeId(task.DeviceId)
                || !IsSafeNonce(task.Nonce) || task.RequestBody.Length is < 2 or > 16384)
                return Fail("invalid_task_envelope", out error);
            if (!long.TryParse(task.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long timestamp))
                return Fail("invalid_task_timestamp", out error);
            DateTimeOffset requestTime;
            try
            {
                requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Fail("invalid_task_timestamp", out error);
            }
            OperationsPairedDevice? device = registry.FindActive(task.DeviceId);
            if (device == null)
                return Fail("unknown_or_revoked_device", out error);
            string requiredScope = task.CapabilityId switch
            {
                "ops.window.show" => "ops.window.control",
                "ops.window.minimize" => "ops.window.control",
                "ops.messaging.reconnect" => "ops.jobs.create",
                "ops.diagnostics.request" => "ops.jobs.create",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(requiredScope))
                return Fail("capability_not_supported_by_desktop_relay", out error);
            if (!device.Scopes.Contains(requiredScope, StringComparer.Ordinal))
                return Fail("device_scope_required", out error);

            byte[] body = Encoding.UTF8.GetBytes(task.RequestBody);
            try
            {
                byte[] publicKey = Convert.FromBase64String(device.PublicKeySpki);
                byte[] signature = Convert.FromBase64String(task.Signature);
                using ECDsa key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(publicKey, out int read);
                string canonical = BuildCanonical("POST", DeviceTaskPath, task.Timestamp, task.Nonce, body);
                if (read != publicKey.Length || !key.VerifyData(
                    Encoding.UTF8.GetBytes(canonical), signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
                    return Fail("invalid_request_signature", out error);
            }
            catch (FormatException)
            {
                return Fail("invalid_signature_encoding", out error);
            }
            catch (CryptographicException)
            {
                return Fail("invalid_request_signature", out error);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || root.EnumerateObject().Any(item => item.Name is not (
                        "hostId" or "capabilityId" or "payload" or "idempotencyKey" or "ttlSeconds"))
                    || !TextEquals(root, "hostId", expectedHostId)
                    || !TextEquals(root, "capabilityId", task.CapabilityId)
                    || !root.TryGetProperty("idempotencyKey", out JsonElement idempotency)
                    || idempotency.ValueKind != JsonValueKind.String
                    || !IsSafeId(idempotency.GetString() ?? string.Empty)
                    || !root.TryGetProperty("payload", out JsonElement payload)
                    || payload.ValueKind != JsonValueKind.Object)
                    return Fail("invalid_task_body", out error);
                int ttlSeconds = 900;
                if (root.TryGetProperty("ttlSeconds", out JsonElement ttlElement))
                {
                    if (!ttlElement.TryGetInt32(out ttlSeconds))
                        return Fail("invalid_task_ttl", out error);
                    ttlSeconds = Math.Clamp(ttlSeconds, 60, 3600);
                }
                if (requestTime > now.Add(AllowedCreatedAtSkew)
                    || requestTime.AddSeconds(ttlSeconds) <= now)
                    return Fail("expired_task_envelope", out error);
                if (task.CapabilityId == "ops.window.show" && payload.EnumerateObject().Any())
                    return Fail("window_show_payload_not_allowed", out error);
                if (task.CapabilityId == "ops.window.minimize" && payload.EnumerateObject().Any())
                    return Fail("window_minimize_payload_not_allowed", out error);
                if (task.CapabilityId == "ops.messaging.reconnect" && payload.EnumerateObject().Any())
                    return Fail("message_reconnect_payload_not_allowed", out error);
                if (task.CapabilityId == "ops.diagnostics.request"
                    && payload.EnumerateObject().Any(item => item.Name != "reason"
                        || item.Value.ValueKind != JsonValueKind.String
                        || (item.Value.GetString() ?? string.Empty).Length > 200))
                    return Fail("invalid_diagnostics_payload", out error);

                verified = new OperationsRelayVerifiedTask
                {
                    TaskId = task.TaskId,
                    CapabilityId = task.CapabilityId,
                    IdempotencyKey = idempotency.GetString()!,
                    Device = device,
                    Payload = payload.Clone(),
                };
                return true;
            }
            catch (JsonException)
            {
                return Fail("invalid_task_body", out error);
            }
        }

        private static bool TextEquals(JsonElement root, string name, string expected) =>
            root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

        private static bool IsSafeId(string value) => value.Length is >= 1 and <= 64
            && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');

        private static bool IsSafeNonce(string value) => value.Length is >= 16 and <= 128
            && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');

        private static bool Fail(string code, out string error)
        {
            error = code;
            return false;
        }
    }
}
