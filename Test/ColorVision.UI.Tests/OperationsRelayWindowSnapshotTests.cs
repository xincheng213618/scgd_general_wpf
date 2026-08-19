using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests;

public sealed class OperationsRelayWindowSnapshotTests
{
    private const string RecipientPkcs8 = "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgckEdAEDTwuGBYepGZjxrwDo5dAN7LDiodc2zctnF1bChRANCAASeIlVBl3AEwo2yA3tqf38ym42n0BGdz+jfcdBo/oAUyxijpyoxmu/QK/SZmfVUV68yXvjLdKSWtWCWNgaoiNjd";
    private const string RecipientSpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEniJVQZdwBMKNsgN7an9/MpuNp9ARnc/o33HQaP6AFMsYo6cqMZrv0Cv0mZn1VFevMl74y3SklrVgljYGqIjY3Q==";
    private const string HostPkcs8 = "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgPGLgGbb45SM2iz6wULoLs16pg+v2+9U3RBewMhRP5TWhRANCAASFVbUo8/oB2qHeWcHWW/U8ADVv8qLtMw8a/3LxPwyo5O7BVd+cDa8fPOaONXYzTRNHlqLYBZG3kOC4p6+mVaXz";
    private const string HostSpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEhVW1KPP6Adqh3lnB1lv1PAA1b/Ki7TMPGv9y8T8MqOTuwVXfnA2vHzzmjjV2M00TR5ai2AWRt5DguKevplWl8w==";
    private static readonly byte[] VectorJpeg = [0xff, 0xd8, 0x10, 0x20, 0x30, 0x40, 0xff, 0xd9];
    private static readonly DateTimeOffset CapturedAt = new(2026, 8, 13, 1, 2, 3, 456, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = CapturedAt.AddMinutes(5);

    [Fact]
    public void ProtocolAcceptsOnlyExactP256PayloadAndFiveMinuteTtl()
    {
        string root = NewRoot();
        Directory.CreateDirectory(root);
        try
        {
            using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            OperationsDeviceRegistry registry = new(Path.Combine(root, "devices.json"));
            registry.Approve("device-vector", "phone",
                Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo()), ["ops.jobs.create"]);
            DateTimeOffset now = new(2026, 8, 13, 1, 0, 0, TimeSpan.Zero);

            OperationsRelayDeviceTask valid = SignedTask(signer,
                SnapshotBody(RecipientSpki, OperationsRelayWindowSnapshotContract.Scheme, 300), now);
            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                valid, "host-vector", registry, now,
                out OperationsRelayVerifiedTask? verified, out string error), error);
            Assert.Equal(now.AddSeconds(300), verified!.EnvelopeExpiresAt);
            Assert.Equal(RecipientSpki,
                verified.Payload.GetProperty("recipientPublicKeySpki").GetString());

            DateTimeOffset deviceClockAhead = now.AddMinutes(2);
            OperationsRelayDeviceTask skewed = SignedTask(
                signer,
                SnapshotBody(RecipientSpki,
                    OperationsRelayWindowSnapshotContract.Scheme, 300),
                deviceClockAhead,
                now.AddMinutes(5));
            Assert.True(OperationsRelayProtocol.TryVerifyDeviceTask(
                skewed, "host-vector", registry, now,
                out OperationsRelayVerifiedTask? skewedVerified, out error), error);
            Assert.Equal(now.AddMinutes(5), skewedVerified!.EnvelopeExpiresAt);

            AssertProtocolError(signer, registry, now,
                SnapshotBody(RecipientSpki, OperationsRelayWindowSnapshotContract.Scheme, null),
                "window_snapshot_ttl_not_allowed");
            AssertProtocolError(signer, registry, now,
                SnapshotBody(RecipientSpki, OperationsRelayWindowSnapshotContract.Scheme, 299),
                "window_snapshot_ttl_not_allowed");
            AssertProtocolError(signer, registry, now,
                SnapshotBody(RecipientSpki, "wrong", 300),
                "window_snapshot_payload_not_allowed");
            AssertProtocolError(signer, registry, now,
                SnapshotBody(RecipientSpki, OperationsRelayWindowSnapshotContract.Scheme, 300,
                    includeExtra: true),
                "window_snapshot_payload_not_allowed");
            AssertProtocolError(signer, registry, now,
                SnapshotBody(RecipientSpki.TrimEnd('='),
                    OperationsRelayWindowSnapshotContract.Scheme, 300),
                "window_snapshot_payload_not_allowed");
            using ECDiffieHellman p384 = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
            AssertProtocolError(signer, registry, now,
                SnapshotBody(Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo()),
                    OperationsRelayWindowSnapshotContract.Scheme, 300),
                "window_snapshot_payload_not_allowed");

            registry.Approve("device-vector", "phone",
                Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo()), ["ops.window.control"]);
            AssertProtocolError(signer, registry, now,
                SnapshotBody(RecipientSpki, OperationsRelayWindowSnapshotContract.Scheme, 300),
                "device_scope_required");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void DeterministicVectorMatchesFixedCrossLanguageContractAndDecrypts()
    {
        OperationsRelayVerifiedTask task = VectorTask();
        OperationsRelayWindowSnapshotCrypto crypto = new(
            () => ImportPrivate(HostPkcs8),
            count => count == 32
                ? Enumerable.Range(0, 32).Select(index => (byte)index).ToArray()
                : Enumerable.Range(0xa0, 12).Select(index => (byte)index).ToArray());

        OperationsRelayEncryptedWindowSnapshot encrypted = crypto.Seal(
            "host-vector", task, "job-vector", [.. VectorJpeg], CapturedAt, ExpiresAt);

        Assert.Equal(HostSpki, encrypted.Evidence.HostEphemeralPublicKeySpki);
        Assert.Equal(
            "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh+goaKjpKWmp6ipqqvod6qcWKJ0++eMbNccUQD+RwF3ctV2AUM=",
            Convert.ToBase64String(encrypted.SealedData));
        Assert.Equal("0ec61440fd6f4435107a94a05963f80e12dfdfa806e24fb72088fffca11cb999",
            encrypted.Evidence.SealedSha256);
        Assert.Equal(encrypted.SealedData.Length, encrypted.Evidence.SealedBytes);
        Assert.Equal(VectorJpeg, Decrypt(encrypted, task, "job-vector"));

        string evidenceJson = JsonSerializer.Serialize(encrypted.Evidence,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using JsonDocument document = JsonDocument.Parse(evidenceJson);
        Assert.Equal(8, document.RootElement.GetPropertyCount());
        Assert.DoesNotContain("width", evidenceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("height", evidenceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plaintext", evidenceJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CiphertextAndAadTamperingAreRejected()
    {
        OperationsRelayVerifiedTask task = VectorTask();
        OperationsRelayEncryptedWindowSnapshot encrypted = new OperationsRelayWindowSnapshotCrypto(
            () => ImportPrivate(HostPkcs8),
            count => Enumerable.Repeat(count == 32 ? (byte)0x11 : (byte)0x22, count).ToArray())
            .Seal("host-vector", task, "job-vector", [.. VectorJpeg], CapturedAt, ExpiresAt);

        byte[] tampered = [.. encrypted.SealedData];
        tampered[OperationsRelayWindowSnapshotContract.SaltBytes
            + OperationsRelayWindowSnapshotContract.NonceBytes] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => Decrypt(
            new OperationsRelayEncryptedWindowSnapshot
            {
                Evidence = encrypted.Evidence,
                SealedData = tampered,
            }, task, "job-vector"));
        Assert.ThrowsAny<CryptographicException>(() => Decrypt(encrypted, task, "other-job"));
    }

    [Fact]
    public async Task HandlerCapturesOnceInMemoryAndUsesWorkStoreTransitions()
    {
        string root = NewRoot();
        DateTimeOffset now = CapturedAt;
        int captureCount = 0;
        try
        {
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));
            string snapshotDirectory = Path.Combine(root, "snapshots");
            OperationsWindowSnapshotService snapshots = new(
                snapshotDirectory, () => now, () =>
                {
                    captureCount++;
                    return [.. VectorJpeg];
                });
            OperationsRelayWindowSnapshotHandler handler = new(
                "host-vector", store, snapshots,
                showWindow: () => new OperationsActionResult(
                    true, OperationsDesktopActionService.ShowWindowAction, "shown"),
                clock: () => now);
            OperationsRelayVerifiedTask task = VectorTask(CapturedAt.AddMinutes(3));
            byte[]? uploaded = null;
            OperationsRelayWindowSnapshotEvidence? evidence = null;

            OperationsRelayWindowSnapshotHandleResult result = await handler.HandleAsync(
                task,
                (value, bytes, _) =>
                {
                    evidence = value;
                    uploaded = [.. bytes];
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.True(result.CompletedReceiptUploaded);
            Assert.Equal("completed", result.Status);
            Assert.NotNull(uploaded);
            Assert.NotNull(evidence);
            Assert.Equal(CapturedAt.AddMinutes(3).ToString("O",
                System.Globalization.CultureInfo.InvariantCulture), evidence.ExpiresAt);
            OperationsJob job = Assert.Single(store.GetJobs());
            Assert.Equal("completed", job.Status);
            Assert.Equal(task.TaskId, job.SourceTaskId);
            Assert.Equal(task.IdempotencyKey, job.SourceIdempotencyKey);
            Assert.Empty(job.Input.EnumerateObject());
            Assert.Equal(1, captureCount);
            Assert.False(Directory.Exists(snapshotDirectory));
            Assert.Contains(store.GetAudit(), item => item.Action == "job.approve");
            Assert.Contains(store.GetAudit(), item => item.Action == "job.execution.start");

            OperationsRelayWindowSnapshotHandleResult duplicate = await handler.HandleAsync(
                task, (_, _, _) => throw new InvalidOperationException("must not upload"),
                CancellationToken.None);
            Assert.False(duplicate.CompletedReceiptUploaded);
            Assert.Equal("failed", duplicate.Status);
            Assert.Equal(1, captureCount);
            Assert.Equal("completed", Assert.Single(store.GetJobs()).Status);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExecutingRecoveryFailsAmbiguouslyWithoutRecapture()
    {
        string root = NewRoot();
        int captureCount = 0;
        try
        {
            OperationsRelayVerifiedTask task = VectorTask();
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));
            OperationsJob job = store.CreateJob(
                OperationsRelayWindowSnapshotContract.CapabilityId,
                task.Device.DeviceId,
                "reason",
                JsonSerializer.SerializeToElement(new { }),
                task.IdempotencyKey,
                task.TaskId,
                task.IdempotencyKey);
            job = Assert.IsType<OperationsJob>(store.DecideJob(
                job.JobId, task.Device.DeviceId, true, "approved", task.IdempotencyKey));
            job = Assert.IsType<OperationsJob>(store.BeginExecution(job.JobId));
            OperationsWindowSnapshotService snapshots = new(
                Path.Combine(root, "snapshots"), captureProvider: () =>
                {
                    captureCount++;
                    return [.. VectorJpeg];
                });
            OperationsRelayWindowSnapshotHandler handler = new(
                "host-vector", store, snapshots,
                showWindow: () => new OperationsActionResult(
                    true, OperationsDesktopActionService.ShowWindowAction, "shown"));

            OperationsRelayWindowSnapshotHandleResult result = await handler.HandleAsync(
                task, (_, _, _) => throw new InvalidOperationException("must not upload"),
                CancellationToken.None);

            Assert.Equal("failed", result.Status);
            Assert.Equal(0, captureCount);
            OperationsJob failed = Assert.Single(store.GetJobs());
            Assert.Equal("failed", failed.Status);
            Assert.Equal("window_snapshot:execution_interrupted_ambiguous", failed.ResultEvidenceId);
            Assert.Single(store.GetAudit(), item =>
                item.Action == "window.snapshot.relay.capture" && item.Outcome == "ambiguous");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void RelayStartupReconcilesExecutingSnapshotWithoutWaitingForRedelivery()
    {
        string root = NewRoot();
        Directory.CreateDirectory(root);
        try
        {
            OperationsRelayVerifiedTask task = VectorTask();
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));
            OperationsJob job = store.CreateJob(
                OperationsRelayWindowSnapshotContract.CapabilityId,
                task.Device.DeviceId,
                "reason",
                JsonSerializer.SerializeToElement(new { }),
                task.IdempotencyKey,
                task.TaskId,
                task.IdempotencyKey);
            job = Assert.IsType<OperationsJob>(store.DecideJob(
                job.JobId, task.Device.DeviceId, true, "approved", task.IdempotencyKey));
            job = Assert.IsType<OperationsJob>(store.BeginExecution(job.JobId));
            using OperationsRelayClientService relay = new(
                new OperationsServerIdentity(Path.Combine(root, "identity")),
                new OperationsDeviceRegistry(Path.Combine(root, "devices.json")),
                store,
                new CaptureHandler());
            MethodInfo? reconcile = typeof(OperationsRelayClientService).GetMethod(
                "ReconcileInterruptedSignedWindowSnapshots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(reconcile);

            reconcile.Invoke(relay, null);
            reconcile.Invoke(relay, null);

            OperationsJob failed = Assert.Single(store.GetJobs());
            Assert.Equal("failed", failed.Status);
            Assert.Equal("window_snapshot:execution_interrupted_ambiguous",
                failed.ResultEvidenceId);
            Assert.Single(store.GetAudit(), item =>
                item.Action == "window.snapshot.relay.capture"
                && item.Outcome == "ambiguous");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task UploadFailureProducesExactErrorAndNeverRecapturesOnRedelivery()
    {
        string root = NewRoot();
        int captureCount = 0;
        try
        {
            OperationsRelayVerifiedTask task = VectorTask();
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));
            OperationsWindowSnapshotService snapshots = new(
                Path.Combine(root, "snapshots"), () => CapturedAt, () =>
                {
                    captureCount++;
                    return [.. VectorJpeg];
                });
            OperationsRelayWindowSnapshotHandler handler = new(
                "host-vector", store, snapshots,
                showWindow: () => new OperationsActionResult(
                    true, OperationsDesktopActionService.ShowWindowAction, "shown"),
                clock: () => CapturedAt);

            OperationsRelayWindowSnapshotHandleResult first = await handler.HandleAsync(
                task, (_, _, _) => throw new HttpRequestException("lost response"),
                CancellationToken.None);
            OperationsRelayWindowSnapshotHandleResult second = await handler.HandleAsync(
                task, (_, _, _) => throw new InvalidOperationException("must not retry"),
                CancellationToken.None);

            Assert.Equal(1, captureCount);
            Assert.Equal("failed", Assert.Single(store.GetJobs()).Status);
            AssertExactError(first.Evidence);
            AssertExactError(second.Evidence);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AtomicUploadSignsRawBodyAndCarriesBase64ReceiptMetadata()
    {
        string root = NewRoot();
        Directory.CreateDirectory(root);
        try
        {
            CaptureHandler capture = new();
            OperationsServerIdentity identity = new(Path.Combine(root, "identity"));
            using OperationsRelayClientService relay = new(
                identity,
                new OperationsDeviceRegistry(Path.Combine(root, "devices.json")),
                new OperationsWorkStore(Path.Combine(root, "work.json")),
                capture);
            OperationsRelayVerifiedTask task = VectorTask();
            OperationsRelayWindowSnapshotEvidence evidence = new()
            {
                JobId = "job-vector",
                HostEphemeralPublicKeySpki = HostSpki,
                SealedSha256 = Convert.ToHexString(SHA256.HashData(VectorJpeg)).ToLowerInvariant(),
                SealedBytes = VectorJpeg.Length,
                CapturedAt = CapturedAt.ToUniversalTime().ToString("O",
                    System.Globalization.CultureInfo.InvariantCulture),
                ExpiresAt = ExpiresAt.ToUniversalTime().ToString("O",
                    System.Globalization.CultureInfo.InvariantCulture),
            };

            MethodInfo? method = typeof(OperationsRelayClientService).GetMethod(
                "SendSignedWindowSnapshotAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Task operation = Assert.IsAssignableFrom<Task>(method.Invoke(
                relay, [task, evidence, VectorJpeg, CancellationToken.None]));
            await operation;

            Assert.Equal(HttpMethod.Post, capture.Method);
            Assert.Equal($"/api/ops/v1/device-relay/hosts/{identity.HostId}"
                + "/tasks/task-vector/window-snapshot", capture.Path);
            Assert.Equal("application/octet-stream", capture.ContentType);
            Assert.Equal(VectorJpeg, capture.Body);
            string metadataJson = Encoding.UTF8.GetString(Convert.FromBase64String(
                Assert.IsType<string>(capture.Headers["X-CV-Receipt-Metadata"])));
            using JsonDocument metadata = JsonDocument.Parse(metadataJson);
            Assert.Equal(3, metadata.RootElement.GetPropertyCount());
            Assert.Equal("completed", metadata.RootElement.GetProperty("status").GetString());
            Assert.Equal(8, metadata.RootElement.GetProperty("evidence").GetPropertyCount());
            JsonElement envelope = metadata.RootElement.GetProperty("receiptEnvelope");
            using JsonDocument receipt = JsonDocument.Parse(envelope.GetProperty("body").GetString()!);
            Assert.Equal(identity.HostId, receipt.RootElement.GetProperty("hostId").GetString());
            Assert.Equal("task-vector", receipt.RootElement.GetProperty("taskId").GetString());
            Assert.Equal("idempotency-vector", receipt.RootElement.GetProperty("idempotencyKey").GetString());
            Assert.Equal("completed", receipt.RootElement.GetProperty("status").GetString());

            string timestamp = capture.Headers["X-CV-Timestamp"];
            string nonce = capture.Headers["X-CV-Nonce"];
            string canonical = OperationsRelayProtocol.BuildCanonical(
                "POST", capture.Path, timestamp, nonce, capture.Body);
            using RSA? publicKey = identity.Certificate.GetRSAPublicKey();
            Assert.NotNull(publicKey);
            Assert.True(publicKey.VerifyData(
                Encoding.UTF8.GetBytes(canonical),
                Convert.FromBase64String(capture.Headers["X-CV-Signature"]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SignedHostAdvertisesWindowSnapshotCaptureOnce()
    {
        MethodInfo? method = typeof(OperationsRelayClientService).GetMethod(
            "GetSignedHostCapabilities", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        string[] capabilities = Assert.IsType<string[]>(method.Invoke(null, null));

        Assert.Single(capabilities, item =>
            item == OperationsRelayWindowSnapshotContract.CapabilityId);
    }

    private static void AssertProtocolError(
        ECDsa signer,
        OperationsDeviceRegistry registry,
        DateTimeOffset now,
        string body,
        string expected)
    {
        OperationsRelayDeviceTask task = SignedTask(signer, body, now);
        Assert.False(OperationsRelayProtocol.TryVerifyDeviceTask(
            task, "host-vector", registry, now, out _, out string error));
        Assert.Equal(expected, error);
    }

    private static string SnapshotBody(
        string recipientSpki,
        string scheme,
        int? ttl,
        bool includeExtra = false)
    {
        Dictionary<string, object> payload = new(StringComparer.Ordinal)
        {
            ["scheme"] = scheme,
            ["recipientPublicKeySpki"] = recipientSpki,
        };
        if (includeExtra)
            payload["extra"] = true;
        Dictionary<string, object> body = new(StringComparer.Ordinal)
        {
            ["hostId"] = "host-vector",
            ["capabilityId"] = OperationsRelayWindowSnapshotContract.CapabilityId,
            ["payload"] = payload,
            ["idempotencyKey"] = "idempotency-vector",
        };
        if (ttl.HasValue)
            body["ttlSeconds"] = ttl.Value;
        return JsonSerializer.Serialize(body);
    }

    private static OperationsRelayDeviceTask SignedTask(
        ECDsa signer,
        string body,
        DateTimeOffset createdAt,
        DateTimeOffset? relayExpiresAt = null)
    {
        string timestamp = createdAt.ToUnixTimeSeconds().ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        const string nonce = "snapshot-vector-nonce";
        string canonical = OperationsRelayProtocol.BuildCanonical(
            "POST", OperationsRelayProtocol.DeviceTaskPath, timestamp, nonce,
            Encoding.UTF8.GetBytes(body));
        return new OperationsRelayDeviceTask
        {
            TaskId = "task-vector",
            CapabilityId = OperationsRelayWindowSnapshotContract.CapabilityId,
            IdempotencyKey = "idempotency-vector",
            RequestBody = body,
            DeviceId = "device-vector",
            Timestamp = timestamp,
            Nonce = nonce,
            Signature = Convert.ToBase64String(signer.SignData(
                Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence)),
            CreatedAt = createdAt,
            ExpiresAt = relayExpiresAt ?? createdAt.AddMinutes(5),
        };
    }

    private static OperationsRelayVerifiedTask VectorTask(
        DateTimeOffset? envelopeExpiresAt = null) => new()
    {
        TaskId = "task-vector",
        CapabilityId = OperationsRelayWindowSnapshotContract.CapabilityId,
        IdempotencyKey = "idempotency-vector",
        Device = new OperationsPairedDevice { DeviceId = "device-vector" },
        Payload = JsonSerializer.SerializeToElement(new
        {
            scheme = OperationsRelayWindowSnapshotContract.Scheme,
            recipientPublicKeySpki = RecipientSpki,
        }),
        EnvelopeExpiresAt = envelopeExpiresAt ?? ExpiresAt,
    };

    private static ECDiffieHellman ImportPrivate(string encodedPkcs8)
    {
        ECDiffieHellman key = ECDiffieHellman.Create();
        byte[] pkcs8 = Convert.FromBase64String(encodedPkcs8);
        key.ImportPkcs8PrivateKey(pkcs8, out int bytesRead);
        Assert.Equal(pkcs8.Length, bytesRead);
        return key;
    }

    private static byte[] Decrypt(
        OperationsRelayEncryptedWindowSnapshot encrypted,
        OperationsRelayVerifiedTask task,
        string jobId)
    {
        using ECDiffieHellman recipient = ImportPrivate(RecipientPkcs8);
        using ECDiffieHellman host = ECDiffieHellman.Create();
        byte[] hostSpki = Convert.FromBase64String(
            encrypted.Evidence.HostEphemeralPublicKeySpki);
        host.ImportSubjectPublicKeyInfo(hostSpki, out int read);
        Assert.Equal(hostSpki.Length, read);
        byte[] shared = recipient.DeriveRawSecretAgreement(host.PublicKey);
        byte[] salt = encrypted.SealedData[..OperationsRelayWindowSnapshotContract.SaltBytes];
        byte[] nonce = encrypted.SealedData[
            OperationsRelayWindowSnapshotContract.SaltBytes..
            (OperationsRelayWindowSnapshotContract.SaltBytes
                + OperationsRelayWindowSnapshotContract.NonceBytes)];
        int cipherOffset = OperationsRelayWindowSnapshotContract.SaltBytes
            + OperationsRelayWindowSnapshotContract.NonceBytes;
        int cipherLength = encrypted.SealedData.Length
            - cipherOffset - OperationsRelayWindowSnapshotContract.TagBytes;
        byte[] ciphertext = encrypted.SealedData.AsSpan(cipherOffset, cipherLength).ToArray();
        byte[] tag = encrypted.SealedData[^OperationsRelayWindowSnapshotContract.TagBytes..];
        byte[] info = Encoding.UTF8.GetBytes(string.Join('\n',
            OperationsRelayWindowSnapshotContract.KeyInfoPrefix,
            "host-vector",
            task.Device.DeviceId,
            task.TaskId,
            task.IdempotencyKey,
            RecipientSpki,
            encrypted.Evidence.HostEphemeralPublicKeySpki));
        byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, salt, info);
        byte[] aad = Encoding.UTF8.GetBytes(string.Join('\n',
            OperationsRelayWindowSnapshotContract.AadPrefix,
            "host-vector",
            task.Device.DeviceId,
            task.TaskId,
            task.IdempotencyKey,
            jobId,
            encrypted.Evidence.CapturedAt,
            encrypted.Evidence.ExpiresAt,
            "image/jpeg"));
        byte[] plaintext = new byte[cipherLength];
        try
        {
            using AesGcm aes = new(key, OperationsRelayWindowSnapshotContract.TagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void AssertExactError(object evidence)
    {
        string json = JsonSerializer.Serialize(evidence,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetPropertyCount());
        Assert.Equal(OperationsRelayWindowSnapshotContract.ErrorKind,
            document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(OperationsRelayWindowSnapshotContract.ErrorCode,
            document.RootElement.GetProperty("code").GetString());
    }

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string Path { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;
        public byte[] Body { get; private set; } = [];
        public Dictionary<string, string> Headers { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.PathAndQuery;
            ContentType = request.Content!.Headers.ContentType!.MediaType!;
            Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                Headers[header.Key] = Assert.Single(header.Value);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
