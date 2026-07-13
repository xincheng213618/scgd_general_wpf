using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsRelayClientService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly string _hostId;
        private readonly OperationsWorkStore _workStore;
        private readonly HttpClient _httpClient;
        private readonly HashSet<string> _processedTasks = new(StringComparer.Ordinal);
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private Func<object>? _snapshotProvider;

        public OperationsRelayClientService(string hostId, OperationsWorkStore workStore)
        {
            _hostId = hostId;
            _workStore = workStore;
            string endpoint = (Environment.GetEnvironmentVariable("COLORVISION_OPERATIONS_RELAY_URL") ?? string.Empty).Trim().TrimEnd('/');
            string apiKey = (Environment.GetEnvironmentVariable("COLORVISION_OPERATIONS_RELAY_KEY") ?? string.Empty).Trim();
            IsConfigured = Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
                && apiKey.StartsWith("cvmp_", StringComparison.Ordinal);
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            if (IsConfigured)
            {
                _httpClient.BaseAddress = uri;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-OperationsRelay/1.0");
            }
        }

        public bool IsConfigured { get; }

        public bool IsRunning => _loop != null && !_loop.IsCompleted;

        public DateTimeOffset? LastHeartbeatAt { get; private set; }

        public string LastStatusMessage { get; private set; } = "Web 运维中继未配置。";

        public void Start(Func<object> snapshotProvider)
        {
            if (!IsConfigured || IsRunning)
                return;
            _snapshotProvider = snapshotProvider;
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
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
                    await PollTasksAsync(cancellationToken).ConfigureAwait(false);
                    await RelaySupportEventsAsync(cancellationToken).ConfigureAwait(false);
                    LastStatusMessage = "Web 运维中继已连接。";
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
            object snapshot = _snapshotProvider?.Invoke() ?? new { };
            object body = new
            {
                displayName = Environment.MachineName,
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
                        OperationsSupportMessage message = _workStore.AddSupportMessage(sessionId, "web-relay", text, taskId);
                        status = "completed";
                        evidence = new { messageId = message.MessageId };
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
            foreach (OperationsSupportSession session in _workStore.GetSupportSessions()
                .Where(item => item.ExpiresAt > DateTimeOffset.UtcNow))
            {
                string? eventType = session.Status switch
                {
                    "awaiting_local_consent" => "session.requested",
                    "active" => "session.active",
                    "rejected_local" => "session.closed",
                    _ => null,
                };
                if (eventType == null)
                    continue;
                string eventKey = $"support:{session.SessionId}:{eventType}";
                if (!_processedTasks.Add(eventKey))
                    continue;
                using HttpResponseMessage response = await PostJsonAsync(
                    $"/api/ops/v1/hosts/{Uri.EscapeDataString(_hostId)}/support-events",
                    new
                    {
                        sessionId = session.SessionId,
                        eventType,
                        payload = new { session.Mode, session.Reason, session.Status, session.ExpiresAt },
                    }, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
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
