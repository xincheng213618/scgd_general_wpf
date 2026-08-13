using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsRelayWindowSnapshotContract
    {
        public const string CapabilityId = "ops.window.snapshot.capture";
        public const string Scheme = "p256-hkdf-sha256-aes256gcm-v1";
        public const string EvidenceKind = "window-snapshot-encrypted-v1";
        public const string ErrorKind = "window-snapshot-error-v1";
        public const string ErrorCode = "window_snapshot_unavailable";
        public const int TtlSeconds = 300;
        public const int SaltBytes = 32;
        public const int NonceBytes = 12;
        public const int TagBytes = 16;
        public const int MaximumSealedBytes = OperationsWindowSnapshotService.MaximumDownloadBytes
            + SaltBytes + NonceBytes + TagBytes;
        public const string KeyInfoPrefix = "colorvision-window-snapshot-key-v1";
        public const string AadPrefix = "colorvision-window-snapshot-aad-v1";

        public static bool IsCanonicalP256PublicKey(string? encodedSpki)
        {
            if (string.IsNullOrEmpty(encodedSpki) || encodedSpki.Length > 512)
                return false;

            try
            {
                byte[] spki = Convert.FromBase64String(encodedSpki);
                if (!string.Equals(Convert.ToBase64String(spki), encodedSpki, StringComparison.Ordinal))
                    return false;
                using ECDiffieHellman key = ECDiffieHellman.Create();
                key.ImportSubjectPublicKeyInfo(spki, out int bytesRead);
                ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
                return bytesRead == spki.Length
                    && string.Equals(parameters.Curve.Oid.Value,
                        ECCurve.NamedCurves.nistP256.Oid.Value, StringComparison.Ordinal)
                    && string.Equals(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                        encodedSpki, StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                return false;
            }
        }

        internal static ECDiffieHellman ImportP256PublicKey(string encodedSpki)
        {
            if (!IsCanonicalP256PublicKey(encodedSpki))
                throw new CryptographicException("window_snapshot_recipient_key_rejected");
            ECDiffieHellman key = ECDiffieHellman.Create();
            try
            {
                byte[] spki = Convert.FromBase64String(encodedSpki);
                key.ImportSubjectPublicKeyInfo(spki, out int bytesRead);
                if (bytesRead != spki.Length)
                    throw new CryptographicException("window_snapshot_recipient_key_rejected");
                return key;
            }
            catch
            {
                key.Dispose();
                throw;
            }
        }

        internal static string FormatUtc(DateTimeOffset value) =>
            value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    public sealed class OperationsRelayWindowSnapshotEvidence
    {
        public string Kind { get; init; } = OperationsRelayWindowSnapshotContract.EvidenceKind;

        public string Scheme { get; init; } = OperationsRelayWindowSnapshotContract.Scheme;

        public string JobId { get; init; } = string.Empty;

        public string HostEphemeralPublicKeySpki { get; init; } = string.Empty;

        public string SealedSha256 { get; init; } = string.Empty;

        public int SealedBytes { get; init; }

        public string CapturedAt { get; init; } = string.Empty;

        public string ExpiresAt { get; init; } = string.Empty;
    }

    public sealed class OperationsRelayWindowSnapshotError
    {
        public string Kind { get; init; } = OperationsRelayWindowSnapshotContract.ErrorKind;

        public string Code { get; init; } = OperationsRelayWindowSnapshotContract.ErrorCode;
    }

    public sealed class OperationsRelayEncryptedWindowSnapshot
    {
        public OperationsRelayWindowSnapshotEvidence Evidence { get; init; } = new();

        public byte[] SealedData { get; init; } = [];
    }

    public sealed class OperationsRelayWindowSnapshotHandleResult
    {
        public bool CompletedReceiptUploaded { get; init; }

        public string Status { get; init; } = "failed";

        public object Evidence { get; init; } = new OperationsRelayWindowSnapshotError();
    }

    public sealed class OperationsRelayWindowSnapshotCrypto
    {
        private readonly Func<ECDiffieHellman> _ephemeralKeyFactory;
        private readonly Func<int, byte[]> _randomBytes;

        public OperationsRelayWindowSnapshotCrypto()
            : this(
                () => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256),
                RandomNumberGenerator.GetBytes)
        {
        }

        public OperationsRelayWindowSnapshotCrypto(
            Func<ECDiffieHellman> ephemeralKeyFactory,
            Func<int, byte[]> randomBytes)
        {
            ArgumentNullException.ThrowIfNull(ephemeralKeyFactory);
            ArgumentNullException.ThrowIfNull(randomBytes);
            _ephemeralKeyFactory = ephemeralKeyFactory;
            _randomBytes = randomBytes;
        }

        public OperationsRelayEncryptedWindowSnapshot Seal(
            string hostId,
            OperationsRelayVerifiedTask task,
            string jobId,
            byte[] jpeg,
            DateTimeOffset capturedAt,
            DateTimeOffset expiresAt)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(jpeg);
            if (task.CapabilityId != OperationsRelayWindowSnapshotContract.CapabilityId
                || task.Payload.ValueKind != JsonValueKind.Object
                || !task.Payload.TryGetProperty("recipientPublicKeySpki", out JsonElement recipientElement)
                || recipientElement.ValueKind != JsonValueKind.String
                || !OperationsRelayWindowSnapshotContract.IsCanonicalP256PublicKey(
                    recipientElement.GetString()))
                throw new CryptographicException("window_snapshot_recipient_key_rejected");
            if (jpeg.Length is <= 0 or > OperationsWindowSnapshotService.MaximumDownloadBytes)
                throw new CryptographicException("window_snapshot_plaintext_size_rejected");
            if (expiresAt <= capturedAt)
                throw new CryptographicException("window_snapshot_expiry_rejected");

            string recipientSpki = recipientElement.GetString()!;
            string capturedText = OperationsRelayWindowSnapshotContract.FormatUtc(capturedAt);
            string expiresText = OperationsRelayWindowSnapshotContract.FormatUtc(expiresAt);
            byte[]? sharedSecret = null;
            byte[]? keyBytes = null;
            byte[]? salt = null;
            byte[]? nonce = null;
            byte[]? ciphertext = null;
            byte[]? tag = null;
            try
            {
                using ECDiffieHellman recipient =
                    OperationsRelayWindowSnapshotContract.ImportP256PublicKey(recipientSpki);
                using ECDiffieHellman hostEphemeral = _ephemeralKeyFactory();
                ECParameters hostParameters = hostEphemeral.ExportParameters(includePrivateParameters: false);
                if (!string.Equals(hostParameters.Curve.Oid.Value,
                    ECCurve.NamedCurves.nistP256.Oid.Value, StringComparison.Ordinal))
                    throw new CryptographicException("window_snapshot_host_key_rejected");
                string hostSpki = Convert.ToBase64String(hostEphemeral.ExportSubjectPublicKeyInfo());

                sharedSecret = hostEphemeral.DeriveRawSecretAgreement(recipient.PublicKey);
                salt = RequireRandomBytes(OperationsRelayWindowSnapshotContract.SaltBytes);
                nonce = RequireRandomBytes(OperationsRelayWindowSnapshotContract.NonceBytes);
                byte[] info = Encoding.UTF8.GetBytes(string.Join('\n',
                    OperationsRelayWindowSnapshotContract.KeyInfoPrefix,
                    hostId,
                    task.Device.DeviceId,
                    task.TaskId,
                    task.IdempotencyKey,
                    recipientSpki,
                    hostSpki));
                keyBytes = HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    sharedSecret,
                    32,
                    salt,
                    info);
                byte[] aad = Encoding.UTF8.GetBytes(string.Join('\n',
                    OperationsRelayWindowSnapshotContract.AadPrefix,
                    hostId,
                    task.Device.DeviceId,
                    task.TaskId,
                    task.IdempotencyKey,
                    jobId,
                    capturedText,
                    expiresText,
                    "image/jpeg"));
                ciphertext = new byte[jpeg.Length];
                tag = new byte[OperationsRelayWindowSnapshotContract.TagBytes];
                using (AesGcm aes = new(keyBytes, OperationsRelayWindowSnapshotContract.TagBytes))
                    aes.Encrypt(nonce, jpeg, ciphertext, tag, aad);

                byte[] sealedData = new byte[salt.Length + nonce.Length + ciphertext.Length + tag.Length];
                int offset = 0;
                Buffer.BlockCopy(salt, 0, sealedData, offset, salt.Length);
                offset += salt.Length;
                Buffer.BlockCopy(nonce, 0, sealedData, offset, nonce.Length);
                offset += nonce.Length;
                Buffer.BlockCopy(ciphertext, 0, sealedData, offset, ciphertext.Length);
                offset += ciphertext.Length;
                Buffer.BlockCopy(tag, 0, sealedData, offset, tag.Length);
                if (sealedData.Length > OperationsRelayWindowSnapshotContract.MaximumSealedBytes)
                    throw new CryptographicException("window_snapshot_ciphertext_size_rejected");

                return new OperationsRelayEncryptedWindowSnapshot
                {
                    Evidence = new OperationsRelayWindowSnapshotEvidence
                    {
                        JobId = jobId,
                        HostEphemeralPublicKeySpki = hostSpki,
                        SealedSha256 = Convert.ToHexString(SHA256.HashData(sealedData)).ToLowerInvariant(),
                        SealedBytes = sealedData.Length,
                        CapturedAt = capturedText,
                        ExpiresAt = expiresText,
                    },
                    SealedData = sealedData,
                };
            }
            finally
            {
                Zero(sharedSecret);
                Zero(keyBytes);
                Zero(ciphertext);
                Zero(tag);
            }
        }

        private byte[] RequireRandomBytes(int count)
        {
            byte[] value = _randomBytes(count);
            if (value.Length != count)
                throw new CryptographicException("window_snapshot_random_length_rejected");
            return value;
        }

        private static void Zero(byte[]? value)
        {
            if (value is { Length: > 0 })
                CryptographicOperations.ZeroMemory(value);
        }
    }

    public sealed class OperationsRelayWindowSnapshotHandler
    {
        private const string FixedReason = "已配对手机明确确认采集单次 ColorVision 主窗口快照";
        private const string ResultEvidencePrefix = "relay-window-snapshot:";
        private readonly string _hostId;
        private readonly OperationsWorkStore _workStore;
        private readonly OperationsWindowSnapshotService _windowSnapshots;
        private readonly OperationsRelayWindowSnapshotCrypto _crypto;
        private readonly Func<OperationsActionResult> _showWindow;
        private readonly Func<DateTimeOffset> _clock;

        public OperationsRelayWindowSnapshotHandler(
            string hostId,
            OperationsWorkStore workStore,
            OperationsWindowSnapshotService windowSnapshots,
            OperationsRelayWindowSnapshotCrypto? crypto = null,
            Func<OperationsActionResult>? showWindow = null,
            Func<DateTimeOffset>? clock = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
            ArgumentNullException.ThrowIfNull(workStore);
            ArgumentNullException.ThrowIfNull(windowSnapshots);
            _hostId = hostId;
            _workStore = workStore;
            _windowSnapshots = windowSnapshots;
            _crypto = crypto ?? new OperationsRelayWindowSnapshotCrypto();
            _showWindow = showWindow ?? (() => OperationsDesktopActionService.Execute(
                OperationsDesktopActionService.ShowWindowAction));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public async Task<OperationsRelayWindowSnapshotHandleResult> HandleAsync(
            OperationsRelayVerifiedTask task,
            Func<OperationsRelayWindowSnapshotEvidence, byte[], CancellationToken, Task> upload,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(upload);
            OperationsJob? job = null;
            OperationsWindowSnapshotResult? snapshot = null;
            OperationsRelayEncryptedWindowSnapshot? encrypted = null;
            bool uploaded = false;
            try
            {
                if (task.CapabilityId != OperationsRelayWindowSnapshotContract.CapabilityId)
                    throw new InvalidOperationException("window_snapshot_capability_mismatch");
                job = _workStore.CreateJob(
                    OperationsRelayWindowSnapshotContract.CapabilityId,
                    task.Device.DeviceId,
                    FixedReason,
                    JsonSerializer.SerializeToElement(new { }),
                    task.IdempotencyKey,
                    task.TaskId,
                    task.IdempotencyKey);
                if (job.CapabilityId != OperationsRelayWindowSnapshotContract.CapabilityId
                    || !string.Equals(job.RequestedByDeviceId, task.Device.DeviceId, StringComparison.Ordinal)
                    || !string.Equals(job.SourceTaskId, task.TaskId, StringComparison.Ordinal)
                    || !string.Equals(job.SourceIdempotencyKey, task.IdempotencyKey, StringComparison.Ordinal))
                    throw new InvalidOperationException("window_snapshot_source_task_conflict");

                if (job.Status == "executing")
                {
                    OperationsJob? interrupted = _workStore.CompleteJob(
                        job.JobId, false, "window_snapshot:execution_interrupted_ambiguous");
                    _workStore.RecordAudit(task.Device.DeviceId, "device",
                        "window.snapshot.relay.capture", job.JobId, "ambiguous", task.IdempotencyKey);
                    return Failed(interrupted ?? job);
                }
                if (job.Status is "completed" or "failed" or "rejected" or "rejected_local")
                    return Failed(job);
                if (job.Status == "awaiting_mobile_approval")
                    job = _workStore.DecideJob(job.JobId, task.Device.DeviceId, approved: true,
                        "已配对手机已明确确认", task.IdempotencyKey);
                if (job?.Status == "approved_mobile")
                    job = _workStore.BeginExecution(job.JobId);
                if (job?.Status != "executing")
                    throw new InvalidOperationException("window_snapshot_job_transition_failed");

                OperationsActionResult shown = _showWindow();
                if (!shown.Success || shown.ActionId != OperationsDesktopActionService.ShowWindowAction)
                    throw new InvalidOperationException("window_snapshot_window_unavailable");
                snapshot = _windowSnapshots.CaptureInMemory();
                DateTimeOffset capturedAt = snapshot.CreatedAt.ToUniversalTime();
                DateTimeOffset expiresAt = task.EnvelopeExpiresAt < capturedAt.Add(
                    OperationsWindowSnapshotService.DownloadLifetime)
                    ? task.EnvelopeExpiresAt
                    : capturedAt.Add(OperationsWindowSnapshotService.DownloadLifetime);
                if (expiresAt <= _clock().ToUniversalTime())
                    throw new InvalidOperationException("window_snapshot_envelope_expired");

                encrypted = _crypto.Seal(
                    _hostId, task, job.JobId, snapshot.Data, capturedAt, expiresAt);
                await upload(encrypted.Evidence, encrypted.SealedData, cancellationToken)
                    .ConfigureAwait(false);
                uploaded = true;

                OperationsJob? completed = _workStore.CompleteJob(
                    job.JobId, true, ResultEvidencePrefix + task.TaskId);
                if (completed == null)
                    throw new InvalidOperationException("window_snapshot_job_completion_failed");
                _workStore.RecordAudit(task.Device.DeviceId, "device",
                    "window.snapshot.relay.capture", job.JobId, "completed", task.IdempotencyKey);
                return new OperationsRelayWindowSnapshotHandleResult
                {
                    CompletedReceiptUploaded = true,
                    Status = "completed",
                    Evidence = encrypted.Evidence,
                };
            }
            catch
            {
                if (!uploaded && job != null)
                {
                    OperationsJob? current = _workStore.GetJobForDevice(
                        job.JobId, task.Device.DeviceId, allowWebRelay: false);
                    if (current?.Status == "executing")
                        _workStore.CompleteJob(current.JobId, false, "window_snapshot:relay_failed");
                    _workStore.RecordAudit(task.Device.DeviceId, "device",
                        "window.snapshot.relay.capture", job.JobId, "failed", task.IdempotencyKey);
                }
                return new OperationsRelayWindowSnapshotHandleResult
                {
                    CompletedReceiptUploaded = uploaded,
                    Status = uploaded ? "completed" : "failed",
                    Evidence = uploaded && encrypted != null
                        ? encrypted.Evidence
                        : new OperationsRelayWindowSnapshotError(),
                };
            }
            finally
            {
                if (snapshot?.Data is { Length: > 0 })
                    CryptographicOperations.ZeroMemory(snapshot.Data);
                if (encrypted?.SealedData is { Length: > 0 })
                    CryptographicOperations.ZeroMemory(encrypted.SealedData);
            }
        }

        private static OperationsRelayWindowSnapshotHandleResult Failed(OperationsJob job) => new()
        {
            Status = "failed",
            Evidence = new OperationsRelayWindowSnapshotError(),
        };
    }
}
