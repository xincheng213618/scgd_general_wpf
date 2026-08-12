using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsRelayClientService : IDisposable
    {
        public const string DefaultEndpoint = "http://xc213618.ddns.me:9998";
        public const string SafeHostDisplayName = "ColorVision 工作站";
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly string _hostId;
        private readonly OperationsServerIdentity _identity;
        private readonly OperationsDeviceRegistry _registry;
        private readonly OperationsWorkStore _workStore;
        private readonly HttpClient _httpClient;
        private readonly bool _signedDeviceRelay;
        private readonly HashSet<string> _processedTasks = new(StringComparer.Ordinal);
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private Func<object>? _snapshotProvider;
        private Func<OperationsLiveMonitorSnapshot?>? _monitorProvider;

        public OperationsRelayClientService(
            OperationsServerIdentity identity,
            OperationsDeviceRegistry registry,
            OperationsWorkStore workStore)
        {
            _identity = identity;
            _hostId = identity.HostId;
            _registry = registry;
            _workStore = workStore;
            string endpoint = (Environment.GetEnvironmentVariable("COLORVISION_OPERATIONS_RELAY_URL") ?? string.Empty).Trim().TrimEnd('/');
            string apiKey = (Environment.GetEnvironmentVariable("COLORVISION_OPERATIONS_RELAY_KEY") ?? string.Empty).Trim();
            bool legacyConfigured = Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
                && apiKey.StartsWith("cvmp_", StringComparison.Ordinal);
            _signedDeviceRelay = !legacyConfigured;
            if (_signedDeviceRelay)
                uri = new Uri(DefaultEndpoint, UriKind.Absolute);
            IsConfigured = true;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.BaseAddress = uri;
            if (legacyConfigured)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-OperationsRelay/2.0");
        }

        public bool IsConfigured { get; }

        public bool IsRunning => _loop != null && !_loop.IsCompleted;

        public DateTimeOffset? LastHeartbeatAt { get; private set; }

        public string LastStatusMessage { get; private set; } = "Web 运维中继未配置。";

        public void Start(
            Func<object> snapshotProvider,
            Func<OperationsLiveMonitorSnapshot?> monitorProvider)
        {
            if (!IsConfigured || IsRunning)
                return;
            ArgumentNullException.ThrowIfNull(snapshotProvider);
            ArgumentNullException.ThrowIfNull(monitorProvider);
            _snapshotProvider = snapshotProvider;
            _monitorProvider = monitorProvider;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _loop = null;
            _snapshotProvider = null;
            _monitorProvider = null;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_signedDeviceRelay)
                    {
                        await SyncSignedHostAsync(cancellationToken).ConfigureAwait(false);
                        await PollSignedTasksAsync(cancellationToken).ConfigureAwait(false);
                        LastStatusMessage = "设备签名远程中继已连接。";
                    }
                    else
                    {
                        await SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
                        await PollTasksAsync(cancellationToken).ConfigureAwait(false);
                        await RelaySupportEventsAsync(cancellationToken).ConfigureAwait(false);
                        LastStatusMessage = "Web 运维中继已连接。";
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LastStatusMessage = $"Web 运维中继暂不可用：{ex.GetType().Name}";
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            OperationsSafeSnapshot snapshot = OperationsSafeSnapshotFactory.Create(
                _snapshotProvider?.Invoke() ?? new { });
            object body = new
            {
                displayName = SafeHostDisplayName,
                appVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty,
                status = "online",
                capabilities = OperationsCapabilityCatalog.GetAll().Where(item => item.Available).Select(item => item.Id).ToArray(),
                snapshot,
            };
            using HttpResponseMessage response = await PostJsonAsync(
                $"/api/ops/v1/hosts/{Uri.EscapeDataString(_hostId)}/heartbeat", body, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            LastHeartbeatAt = DateTimeOffset.UtcNow;
        }

        private async Task SyncSignedHostAsync(CancellationToken cancellationToken)
        {
            OperationsRelaySnapshot snapshot = OperationsRelaySnapshotFactory.Create(
                _snapshotProvider?.Invoke() ?? new { },
                _monitorProvider?.Invoke());
            string appVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty;
            string[] capabilities = ["ops.window.show", "ops.diagnostics.request"];
            long signedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string snapshotBody = JsonSerializer.Serialize(new
            {
                hostId = _hostId,
                appVersion,
                status = "online",
                capabilities,
                snapshot,
                signedAt,
            }, JsonOptions);
            object body = new
            {
                hostId = _hostId,
                displayName = SafeHostDisplayName,
                appVersion,
                status = "online",
                capabilities,
                snapshot,
                snapshotEnvelope = new
                {
                    body = snapshotBody,
                    signature = _identity.Sign(OperationsRelayProtocol.BuildHostEnvelopeCanonical(
                        OperationsRelayProtocol.HostSnapshotEnvelopePrefix, snapshotBody)),
                },
                devices = _registry.GetAll().Select(item => new
                {
                    item.DeviceId,
                    DisplayName = "已配对设备",
                    item.PublicKeySpki,
                    item.Scopes,
                    item.ApprovedAt,
                    item.RevokedAt,
                }).ToArray(),
            };
            string path = $"/api/ops/v1/device-relay/hosts/{Uri.EscapeDataString(_hostId)}/sync";
            using HttpResponseMessage response = await SendSignedHostRequestAsync(
                path, body, includeCertificate: true, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            LastHeartbeatAt = DateTimeOffset.UtcNow;
        }

        private async Task PollSignedTasksAsync(CancellationToken cancellationToken)
        {
            string path = $"/api/ops/v1/device-relay/hosts/{Uri.EscapeDataString(_hostId)}/tasks";
            using HttpResponseMessage response = await SendSignedHostRequestAsync(
                path, new { }, includeCertificate: false, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("tasks", out JsonElement tasks)
                || tasks.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement taskElement in tasks.EnumerateArray())
            {
                OperationsRelayDeviceTask? task = taskElement.Deserialize<OperationsRelayDeviceTask>(JsonOptions);
                if (task == null || string.IsNullOrWhiteSpace(task.TaskId))
                    continue;

                string status;
                object evidence;
                if (!OperationsRelayProtocol.TryVerifyDeviceTask(
                    task, _hostId, _registry, DateTimeOffset.UtcNow,
                    out OperationsRelayVerifiedTask? verified, out string error))
                {
                    status = "rejected";
                    evidence = new { error };
                }
                else if (verified!.CapabilityId == "ops.window.show")
                {
                    string intentKey = $"relay-intent:{verified.Device.DeviceId}:{verified.IdempotencyKey}";
                    if (_processedTasks.Contains(intentKey)
                        || _workStore.HasProcessedRelayIntent(verified.Device.DeviceId, verified.IdempotencyKey))
                    {
                        status = "completed";
                        evidence = new { actionId = OperationsDesktopActionService.ShowWindowAction, deduplicated = true };
                    }
                    else
                    {
                        OperationsActionResult result = OperationsDesktopActionService.Execute(
                            OperationsDesktopActionService.ShowWindowAction);
                        status = result.Success ? "completed" : "failed";
                        evidence = new { result.ActionId, result.Message };
                        _workStore.RecordAudit(verified.Device.DeviceId, "device", "relay.intent.execute",
                            result.ActionId, status, verified.IdempotencyKey);
                        _processedTasks.Add(intentKey);
                    }
                }
                else
                {
                    string reason = verified.Payload.TryGetProperty("reason", out JsonElement reasonElement)
                        ? reasonElement.GetString() ?? "远程诊断请求" : "远程诊断请求";
                    OperationsJob job = _workStore.CreateJob(
                        "ops.diagnostics.bundle.create", verified.Device.DeviceId, reason,
                        JsonSerializer.SerializeToElement(new { }), verified.TaskId, verified.IdempotencyKey);
                    status = "awaiting_local_consent";
                    evidence = new { jobId = job.JobId };
                }

                await SendSignedTaskReceiptAsync(
                    task.TaskId, status, evidence, verified?.IdempotencyKey ?? task.IdempotencyKey,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task SendSignedTaskReceiptAsync(
            string taskId,
            string status,
            object evidence,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            string path = $"/api/ops/v1/device-relay/hosts/{Uri.EscapeDataString(_hostId)}"
                + $"/tasks/{Uri.EscapeDataString(taskId)}/receipts";
            long signedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string receiptBody = JsonSerializer.Serialize(new
            {
                hostId = _hostId,
                taskId,
                idempotencyKey,
                status,
                evidence,
                signedAt,
            }, JsonOptions);
            object body = new
            {
                status,
                evidence,
                receiptEnvelope = new
                {
                    body = receiptBody,
                    signature = _identity.Sign(OperationsRelayProtocol.BuildHostEnvelopeCanonical(
                        OperationsRelayProtocol.HostReceiptEnvelopePrefix, receiptBody)),
                },
            };
            using HttpResponseMessage response = await SendSignedHostRequestAsync(
                path, body, includeCertificate: false, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private async Task<HttpResponseMessage> SendSignedHostRequestAsync(
            string path,
            object body,
            bool includeCertificate,
            CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, JsonOptions));
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string canonical = OperationsRelayProtocol.BuildCanonical("POST", path, timestamp, nonce, bytes);
            using HttpRequestMessage request = new(HttpMethod.Post, path)
            {
                Content = new ByteArrayContent(bytes),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };
            request.Headers.Add("X-CV-Host-Id", _hostId);
            request.Headers.Add("X-CV-Timestamp", timestamp);
            request.Headers.Add("X-CV-Nonce", nonce);
            request.Headers.Add("X-CV-Signature", _identity.Sign(canonical));
            if (includeCertificate)
                request.Headers.Add("X-CV-Host-Certificate", _identity.CertificateDer);
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task PollTasksAsync(CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/ops/v1/hosts/{Uri.EscapeDataString(_hostId)}/tasks", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("tasks", out JsonElement tasks) || tasks.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement task in tasks.EnumerateArray())
            {
                string taskId = task.GetProperty("taskId").GetString() ?? string.Empty;
                string capabilityId = task.GetProperty("capabilityId").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(taskId) || !_processedTasks.Add(taskId))
                    continue;

                string status = "received";
                object evidence = new { };
                if (capabilityId == "ops.diagnostics.request")
                {
                    JsonElement payload = task.TryGetProperty("payload", out JsonElement value)
                        ? value : JsonSerializer.SerializeToElement(new { });
                    string reason = payload.TryGetProperty("reason", out JsonElement reasonElement)
                        ? reasonElement.GetString() ?? "Web relay diagnostic request" : "Web relay diagnostic request";
                    OperationsJob job = _workStore.CreateJob("ops.diagnostics.bundle.create", "web-relay", reason, payload, taskId);
                    status = "awaiting_local_consent";
                    evidence = new { jobId = job.JobId };
                }
                else if (capabilityId is not ("ops.support.message" or "ops.deployment.verify"))
                {
                    status = "rejected";
                    evidence = new { error = "capability_not_supported_by_desktop_relay" };
                }
                else if (capabilityId == "ops.support.message")
                {
                    JsonElement payload = task.TryGetProperty("payload", out JsonElement value)
                        ? value : JsonSerializer.SerializeToElement(new { });
                    string sessionId = payload.TryGetProperty("sessionId", out JsonElement sessionElement)
                        ? sessionElement.GetString() ?? string.Empty : string.Empty;
                    string text = payload.TryGetProperty("text", out JsonElement textElement)
                        ? textElement.GetString() ?? string.Empty : string.Empty;
                    if (sessionId.Length is < 1 or > 64 || text.Length is < 1 or > 2000)
                    {
                        status = "rejected";
                        evidence = new { error = "invalid_support_message" };
                    }
                    else
                    {
                        try
                        {
                            OperationsSupportMessage message = _workStore.AddSupportMessage(sessionId, "web-relay", text, taskId);
                            status = "completed";
                            evidence = new { messageId = message.MessageId };
                        }
                        catch (InvalidOperationException ex) when (ex.Message is
                            "support_session_not_found" or "support_session_not_active" or "invalid_support_message")
                        {
                            status = "rejected";
                            evidence = new { error = ex.Message };
                        }
                    }
                }

                await SendTaskReceiptAsync(taskId, status, evidence, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SendTaskReceiptAsync(string taskId, string status, object evidence, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await PostJsonAsync(
                $"/api/ops/v1/hosts/{Uri.EscapeDataString(_hostId)}/tasks/{Uri.EscapeDataString(taskId)}/receipts",
                new { status, evidence }, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private async Task RelaySupportEventsAsync(CancellationToken cancellationToken)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (OperationsSupportSession session in _workStore.GetSupportSessions().Take(100))
            {
                string effectiveStatus = session.ExpiresAt <= now
                    && session.Status is "awaiting_local_consent" or "active"
                    ? "expired"
                    : session.Status;
                string? eventType = effectiveStatus switch
                {
                    "awaiting_local_consent" => "session.requested",
                    "active" => "session.active",
                    "rejected_local" or "expired" => "session.closed",
                    _ => null,
                };
                if (eventType == null)
                    continue;
                string eventKey = $"support:{session.SessionId}:{eventType}";
                if (_processedTasks.Contains(eventKey))
                    continue;
                using HttpResponseMessage response = await PostJsonAsync(
                    $"/api/ops/v1/hosts/{Uri.EscapeDataString(_hostId)}/support-events",
                    new
                    {
                        sessionId = session.SessionId,
                        eventType,
                        payload = new { session.Mode, session.Reason, status = effectiveStatus, session.ExpiresAt },
                    }, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                _processedTasks.Add(eventKey);

                if (effectiveStatus != "active")
                    continue;
                foreach (OperationsSupportMessage message in _workStore.GetSupportMessagesForSession(session.SessionId)
                    .Where(item => item.Source == "device"))
                {
                    string messageKey = $"support-message:{message.MessageId}";
                    if (_processedTasks.Contains(messageKey))
                        continue;
                    using HttpResponseMessage messageResponse = await PostJsonAsync(
                        $"/api/ops/v1/hosts/{Uri.EscapeDataString(_hostId)}/support-events",
                        new
                        {
                            sessionId = session.SessionId,
                            eventType = "message",
                            payload = new { direction = "from-device", message.Text, message.CreatedAt },
                        }, cancellationToken).ConfigureAwait(false);
                    messageResponse.EnsureSuccessStatusCode();
                    _processedTasks.Add(messageKey);
                }
            }
        }

        private Task<HttpResponseMessage> PostJsonAsync(string path, object body, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(body, JsonOptions);
            return _httpClient.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
        }

        public void Dispose()
        {
            Stop();
            _httpClient.Dispose();
        }
    }
}
