using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsJob
    {
        public string JobId { get; set; } = string.Empty;
        public string CapabilityId { get; set; } = string.Empty;
        public string RequestedByDeviceId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public JsonElement Input { get; set; }
        public string RiskLevel { get; set; } = OperationsRiskLevels.ApprovalRequired;
        public string Status { get; set; } = "awaiting_mobile_approval";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? DecisionByDeviceId { get; set; }
        public string? DecisionReason { get; set; }
        public DateTimeOffset? DecisionAt { get; set; }
        public DateTimeOffset? LocalCoSignedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? ResultEvidenceId { get; set; }
        public string? SourceTaskId { get; set; }

        [JsonIgnore]
        public string DisplayTitle => CapabilityId switch
        {
            "ops.window.snapshot.capture" => "采集 ColorVision 主窗口安全快照",
            "ops.diagnostics.bundle.create" => "生成脱敏安全诊断包",
            "ops.service.restart" => "重启白名单 MQTT 服务",
            "ops.application.restart" => "重启 ColorVision 应用",
            "ops.messaging.reconnect" => "恢复 ColorVision 消息通道",
            "ops.flow.cancel" => "取消当前主检测流程",
            _ => "现场运维作业",
        };

        [JsonIgnore]
        public string LocalCoSignNotice => CapabilityId switch
        {
            "ops.window.snapshot.capture" => "只捕获 ColorVision 主窗口；可能包含当前可见的检测数据。手机明确确认后立即采集，JPEG 仅保留 5 分钟，由申请设备读取一次后删除。",
            "ops.diagnostics.bundle.create" => "手机明确确认后生成不含图像、凭据、用户名、机器名、网络地址或原始日志的脱敏 ZIP。",
            "ops.service.restart" => "仅允许通过 ServiceHost 重启固定的 Mosquitto 服务；手机明确确认后立即执行，无需电脑端再次共签。",
            "ops.application.restart" => "只干净重启当前 ColorVision 应用，不接受路径、参数或其他进程；检测运行中会拒绝。",
            "ops.messaging.reconnect" => "只在 ColorVision 消息客户端断开或订阅未就绪时重建固定现有连接并恢复已登记订阅，不接受地址、Topic、凭据或参数。",
            "ops.flow.cancel" => "只向当前主工作区正在执行的检测发送取消请求，不接受流程、节点或参数。无需电脑端再次共签。",
            _ => "请确认作业来源和固定能力范围。",
        };
    }

    public sealed class OperationsDeploymentReceipt
    {
        public string ReceiptId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string ReleaseId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string EvidenceSha256 { get; set; } = string.Empty;
        public DateTimeOffset ConfirmedAt { get; set; }
    }

    public sealed class OperationsSupportSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestedByDeviceId { get; set; } = string.Empty;
        public string Mode { get; set; } = "diagnostics";
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "awaiting_local_consent";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? LocalConsentAt { get; set; }
    }

    public sealed class OperationsAuditEntry
    {
        public string AuditId { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string ActorType { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class OperationsSupportMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? SourceTaskId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class OperationsWorkStore
    {
        private sealed class State
        {
            public List<OperationsJob> Jobs { get; set; } = [];
            public List<OperationsDeploymentReceipt> DeploymentReceipts { get; set; } = [];
            public List<OperationsSupportSession> SupportSessions { get; set; } = [];
            public List<OperationsSupportMessage> SupportMessages { get; set; } = [];
            public List<OperationsAuditEntry> Audit { get; set; } = [];
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private readonly object _syncRoot = new();
        private readonly string _path;
        private State _state;

        public OperationsWorkStore(string? path = null)
        {
            _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision", "Operations", "work-state.json");
            _state = Load();
        }

        public event EventHandler? Changed;

        public IReadOnlyList<OperationsJob> GetJobs()
        {
            lock (_syncRoot)
                return _state.Jobs.OrderByDescending(item => item.CreatedAt).Select(Clone).ToList();
        }

        public IReadOnlyList<OperationsJob> GetJobsForDevice(string deviceId)
        {
            lock (_syncRoot)
                return _state.Jobs.Where(item => CanAccessJob(item, deviceId, allowWebRelay: true))
                    .OrderByDescending(item => item.CreatedAt).Select(Clone).ToList();
        }

        public OperationsJob? GetJobForDevice(string jobId, string deviceId, bool allowWebRelay = true)
        {
            lock (_syncRoot)
            {
                OperationsJob? job = _state.Jobs.FirstOrDefault(item =>
                    item.JobId == jobId && CanAccessJob(item, deviceId, allowWebRelay));
                return job == null ? null : Clone(job);
            }
        }

        public OperationsJob CreateJob(
            string capabilityId,
            string deviceId,
            string reason,
            JsonElement input,
            string correlationId,
            string? sourceTaskId = null)
        {
            if (capabilityId is not ("ops.diagnostics.bundle.create" or "ops.window.snapshot.capture" or "ops.service.restart" or "ops.application.restart" or "ops.messaging.reconnect" or "ops.flow.cancel"))
                throw new InvalidOperationException("capability_not_allowed_for_remote_job");
            if (capabilityId == "ops.service.restart" && !IsAllowedMqttRestartInput(input))
                throw new InvalidOperationException("mqtt_restart_input_not_allowed");
            if ((capabilityId is "ops.application.restart" or "ops.messaging.reconnect") && !IsEmptyInput(input))
                throw new InvalidOperationException("job_input_not_allowed");
            if (deviceId != "web-relay"
                && capabilityId is "ops.diagnostics.bundle.create" or "ops.window.snapshot.capture" or "ops.flow.cancel"
                && !IsEmptyInput(input))
                throw new InvalidOperationException("job_input_not_allowed");
            OperationsJob job = new()
            {
                JobId = Guid.NewGuid().ToString("N"),
                CapabilityId = capabilityId,
                RequestedByDeviceId = deviceId,
                Reason = reason,
                Input = input.Clone(),
                RiskLevel = capabilityId == "ops.service.restart" ? OperationsRiskLevels.Privileged : OperationsRiskLevels.ApprovalRequired,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                SourceTaskId = sourceTaskId ?? (deviceId == "web-relay" ? correlationId : null),
            };
            lock (_syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(job.SourceTaskId))
                {
                    OperationsJob? existing = _state.Jobs.FirstOrDefault(item => item.SourceTaskId == job.SourceTaskId);
                    if (existing != null)
                        return Clone(existing);
                }
                _state.Jobs.Add(job);
                AuditNoLock(deviceId, "device", "job.create", job.JobId, "accepted", correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return Clone(job);
        }

        public OperationsJob? DecideJob(string jobId, string deviceId, bool approved, string reason, string correlationId)
        {
            OperationsJob result;
            lock (_syncRoot)
            {
                OperationsJob? job = _state.Jobs.FirstOrDefault(item => item.JobId == jobId);
                if (job == null || !CanAccessJob(job, deviceId, allowWebRelay: true)
                    || job.Status != "awaiting_mobile_approval")
                    return null;
                job.DecisionByDeviceId = deviceId;
                job.DecisionReason = reason;
                job.DecisionAt = DateTimeOffset.UtcNow;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Status = approved
                    ? RequiresLocalCoSign(job) ? "awaiting_local_cosign" : "approved_mobile"
                    : "rejected";
                AuditNoLock(deviceId, "device", approved ? "job.approve" : "job.reject", jobId, job.Status, correlationId);
                SaveNoLock();
                result = Clone(job);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public OperationsJob? LocalCoSign(string jobId, bool approved, string evidenceId = "")
        {
            OperationsJob result;
            lock (_syncRoot)
            {
                OperationsJob? job = _state.Jobs.FirstOrDefault(item => item.JobId == jobId);
                if (job == null || job.Status != "awaiting_local_cosign")
                    return null;
                job.LocalCoSignedAt = DateTimeOffset.UtcNow;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Status = approved ? "approved_local" : "rejected_local";
                job.ResultEvidenceId = string.IsNullOrWhiteSpace(evidenceId) ? null : evidenceId;
                AuditNoLock(Environment.UserName, "local-user", approved ? "job.local_cosign" : "job.local_reject",
                    jobId, job.Status, Guid.NewGuid().ToString("N"));
                SaveNoLock();
                result = Clone(job);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public OperationsJob? CompleteJob(string jobId, bool success, string evidenceId)
        {
            OperationsJob result;
            lock (_syncRoot)
            {
                OperationsJob? job = _state.Jobs.FirstOrDefault(item => item.JobId == jobId);
                if (job == null || job.Status is not ("executing" or "approved_local"))
                    return null;
                job.Status = success ? "completed" : "failed";
                job.ResultEvidenceId = evidenceId;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.CompletedAt = job.UpdatedAt;
                AuditNoLock("operations-broker", "system", "job.complete", jobId, job.Status, Guid.NewGuid().ToString("N"));
                SaveNoLock();
                result = Clone(job);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public OperationsJob? BeginExecution(string jobId)
        {
            OperationsJob result;
            lock (_syncRoot)
            {
                OperationsJob? job = _state.Jobs.FirstOrDefault(item => item.JobId == jobId);
                if (job == null || (job.Status != "approved_local"
                    && (job.Status != "approved_mobile" || RequiresLocalCoSign(job))))
                    return null;
                job.Status = "executing";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                AuditNoLock("operations-api", "system", "job.execution.start", jobId,
                    "executing", Guid.NewGuid().ToString("N"));
                SaveNoLock();
                result = Clone(job);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        internal static bool RequiresLocalCoSign(OperationsJob job) =>
            job.RequestedByDeviceId == "web-relay" || RequiresLocalCoSign(job.CapabilityId);

        internal static bool RequiresLocalCoSign(string capabilityId) =>
            capabilityId is not ("ops.flow.cancel" or "ops.service.restart" or "ops.application.restart"
                or "ops.messaging.reconnect" or "ops.diagnostics.bundle.create" or "ops.window.snapshot.capture");

        internal static bool IsEmptyInput(JsonElement input) =>
            input.ValueKind == JsonValueKind.Object && !input.EnumerateObject().Any();

        internal static bool IsAllowedMqttRestartInput(JsonElement input)
        {
            if (input.ValueKind != JsonValueKind.Object)
                return false;
            JsonProperty[] properties = input.EnumerateObject().ToArray();
            return properties.Length == 1
                && properties[0].NameEquals("serviceId")
                && properties[0].Value.ValueKind == JsonValueKind.String
                && string.Equals(properties[0].Value.GetString(), "mosquitto", StringComparison.Ordinal);
        }

        public bool ClearJobEvidence(string jobId, string expectedEvidenceId)
        {
            lock (_syncRoot)
            {
                OperationsJob? job = _state.Jobs.FirstOrDefault(item => item.JobId == jobId);
                if (job == null || job.Status != "completed"
                    || !string.Equals(job.ResultEvidenceId, expectedEvidenceId, StringComparison.Ordinal))
                    return false;
                job.ResultEvidenceId = null;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                AuditNoLock("operations-api", "system", "job.evidence.consume", jobId,
                    "completed", Guid.NewGuid().ToString("N"));
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public IReadOnlyList<OperationsDeploymentReceipt> GetDeploymentReceipts()
        {
            lock (_syncRoot)
                return _state.DeploymentReceipts.OrderByDescending(item => item.ConfirmedAt).ToList();
        }

        public OperationsDeploymentReceipt AddDeploymentReceipt(string deviceId, string releaseId, string version,
            string status, string evidenceSha256, string correlationId)
        {
            if (status is not ("installed" or "verified" or "failed"))
                throw new InvalidOperationException("invalid_deployment_status");
            if (!string.IsNullOrEmpty(evidenceSha256) && !System.Text.RegularExpressions.Regex.IsMatch(evidenceSha256, "^[0-9a-fA-F]{64}$"))
                throw new InvalidOperationException("invalid_evidence_hash");
            OperationsDeploymentReceipt receipt = new()
            {
                ReceiptId = Guid.NewGuid().ToString("N"),
                DeviceId = deviceId,
                ReleaseId = releaseId,
                Version = version,
                Status = status,
                EvidenceSha256 = evidenceSha256.ToLowerInvariant(),
                ConfirmedAt = DateTimeOffset.UtcNow,
            };
            lock (_syncRoot)
            {
                _state.DeploymentReceipts.Add(receipt);
                AuditNoLock(deviceId, "device", "deployment.receipt.create", receipt.ReceiptId, status, correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return receipt;
        }

        public IReadOnlyList<OperationsSupportSession> GetSupportSessions()
        {
            lock (_syncRoot)
                return _state.SupportSessions.OrderByDescending(item => item.CreatedAt).Select(Clone).ToList();
        }

        public IReadOnlyList<OperationsSupportMessage> GetSupportMessages(int count = 100)
        {
            lock (_syncRoot)
                return _state.SupportMessages.OrderByDescending(item => item.CreatedAt).Take(Math.Clamp(count, 1, 200))
                    .Select(Clone).ToList();
        }

        public IReadOnlyList<OperationsSupportSession> GetSupportSessionsForDevice(string deviceId)
        {
            lock (_syncRoot)
                return _state.SupportSessions.Where(item => item.RequestedByDeviceId == deviceId)
                    .OrderByDescending(item => item.CreatedAt).Select(Clone).ToList();
        }

        public OperationsSupportSession? GetSupportSessionForDevice(string sessionId, string deviceId)
        {
            lock (_syncRoot)
            {
                OperationsSupportSession? session = _state.SupportSessions.FirstOrDefault(item =>
                    item.SessionId == sessionId && item.RequestedByDeviceId == deviceId);
                return session == null ? null : Clone(session);
            }
        }

        public IReadOnlyList<OperationsSupportMessage> GetSupportMessagesForSession(string sessionId, int count = 100)
        {
            lock (_syncRoot)
                return _state.SupportMessages.Where(item => item.SessionId == sessionId)
                    .OrderByDescending(item => item.CreatedAt).Take(Math.Clamp(count, 1, 200))
                    .OrderBy(item => item.CreatedAt).Select(Clone).ToList();
        }

        public IReadOnlyList<OperationsSupportMessage> GetSupportMessagesForDevice(string deviceId, int count = 100)
        {
            lock (_syncRoot)
            {
                HashSet<string> ownedSessions = _state.SupportSessions
                    .Where(item => item.RequestedByDeviceId == deviceId)
                    .Select(item => item.SessionId).ToHashSet(StringComparer.Ordinal);
                return _state.SupportMessages.Where(item => ownedSessions.Contains(item.SessionId))
                    .OrderByDescending(item => item.CreatedAt).Take(Math.Clamp(count, 1, 200))
                    .OrderBy(item => item.CreatedAt).Select(Clone).ToList();
            }
        }

        public OperationsSupportMessage AddSupportMessage(string sessionId, string source, string text, string correlationId)
        {
            if (source != "web-relay")
                throw new InvalidOperationException("invalid_support_message_source");
            return AddSupportMessageCore(sessionId, source, text, correlationId, deviceId: null);
        }

        public OperationsSupportMessage AddDeviceSupportMessage(
            string sessionId,
            string deviceId,
            string text,
            string correlationId) => AddSupportMessageCore(sessionId, "device", text, correlationId, deviceId);

        private OperationsSupportMessage AddSupportMessageCore(
            string sessionId,
            string source,
            string text,
            string correlationId,
            string? deviceId)
        {
            string boundedText = (text ?? string.Empty).Trim();
            if (boundedText.Length is < 1 or > 2000)
                throw new InvalidOperationException("invalid_support_message");
            OperationsSupportMessage message = new()
            {
                MessageId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                Source = source,
                Text = boundedText,
                SourceTaskId = source == "web-relay" ? correlationId : null,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            lock (_syncRoot)
            {
                if (source == "web-relay" && !string.IsNullOrWhiteSpace(correlationId))
                {
                    OperationsSupportMessage? existing = _state.SupportMessages.FirstOrDefault(item => item.SourceTaskId == correlationId);
                    if (existing != null)
                        return Clone(existing);
                }
                OperationsSupportSession? session = _state.SupportSessions.FirstOrDefault(item => item.SessionId == sessionId);
                if (session == null || deviceId != null && session.RequestedByDeviceId != deviceId)
                    throw new InvalidOperationException("support_session_not_found");
                if (session.Status != "active" || session.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new InvalidOperationException("support_session_not_active");
                _state.SupportMessages.Add(message);
                if (_state.SupportMessages.Count > 1000)
                    _state.SupportMessages.RemoveRange(0, _state.SupportMessages.Count - 1000);
                AuditNoLock(deviceId ?? source, deviceId == null ? "support-relay" : "device",
                    deviceId == null ? "support.message.receive" : "support.message.send",
                    sessionId, "accepted", correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return Clone(message);
        }

        public OperationsSupportSession RequestSupport(string deviceId, string mode, string reason, int durationMinutes, string correlationId)
        {
            if (mode is not ("diagnostics" or "guided"))
                throw new InvalidOperationException("unsupported_support_mode");
            int boundedDuration = Math.Clamp(durationMinutes, 5, 30);
            OperationsSupportSession session;
            lock (_syncRoot)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                OperationsSupportSession? existing = _state.SupportSessions.FirstOrDefault(item =>
                    item.RequestedByDeviceId == deviceId
                    && item.ExpiresAt > now
                    && item.Status is "awaiting_local_consent" or "active");
                if (existing != null)
                    return Clone(existing);

                session = new OperationsSupportSession
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    RequestedByDeviceId = deviceId,
                    Mode = mode,
                    Reason = reason,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(boundedDuration),
                };
                _state.SupportSessions.Add(session);
                AuditNoLock(deviceId, "device", "support.request", session.SessionId, "awaiting_local_consent", correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return Clone(session);
        }

        public OperationsSupportSession? LocalConsentSupport(string sessionId, bool approved)
        {
            OperationsSupportSession result;
            lock (_syncRoot)
            {
                OperationsSupportSession? session = _state.SupportSessions.FirstOrDefault(item => item.SessionId == sessionId);
                if (session == null || session.Status != "awaiting_local_consent" || session.ExpiresAt <= DateTimeOffset.UtcNow)
                    return null;
                session.Status = approved ? "active" : "rejected_local";
                session.LocalConsentAt = DateTimeOffset.UtcNow;
                AuditNoLock(Environment.UserName, "local-user", approved ? "support.local_consent" : "support.local_reject",
                    sessionId, session.Status, Guid.NewGuid().ToString("N"));
                SaveNoLock();
                result = Clone(session);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public IReadOnlyList<OperationsAuditEntry> GetAudit(int count = 100)
        {
            lock (_syncRoot)
                return _state.Audit.OrderByDescending(item => item.Timestamp).Take(Math.Clamp(count, 1, 500)).ToList();
        }

        public void RecordAudit(string actorId, string actorType, string action, string targetId, string outcome, string correlationId)
        {
            lock (_syncRoot)
            {
                AuditNoLock(actorId, actorType, action, targetId, outcome, correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool HasProcessedRelayIntent(string deviceId, string idempotencyKey)
        {
            lock (_syncRoot)
                return _state.Audit.Any(item =>
                    string.Equals(item.ActorId, deviceId, StringComparison.Ordinal)
                    && string.Equals(item.ActorType, "device", StringComparison.Ordinal)
                    && string.Equals(item.Action, "relay.intent.execute", StringComparison.Ordinal)
                    && string.Equals(item.CorrelationId, idempotencyKey, StringComparison.Ordinal));
        }

        public bool RecordAuditThrottled(
            string actorId,
            string actorType,
            string action,
            string targetId,
            string outcome,
            string correlationId,
            TimeSpan minimumInterval)
        {
            if (minimumInterval < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(minimumInterval));

            bool recorded;
            lock (_syncRoot)
            {
                DateTimeOffset cutoff = DateTimeOffset.UtcNow - minimumInterval;
                recorded = !_state.Audit.Any(item =>
                    item.Timestamp >= cutoff
                    && string.Equals(item.ActorId, actorId, StringComparison.Ordinal)
                    && string.Equals(item.ActorType, actorType, StringComparison.Ordinal)
                    && string.Equals(item.Action, action, StringComparison.Ordinal)
                    && string.Equals(item.TargetId, targetId, StringComparison.Ordinal)
                    && string.Equals(item.Outcome, outcome, StringComparison.Ordinal));
                if (recorded)
                {
                    AuditNoLock(actorId, actorType, action, targetId, outcome, correlationId);
                    SaveNoLock();
                }
            }
            if (recorded)
                Changed?.Invoke(this, EventArgs.Empty);
            return recorded;
        }

        private void AuditNoLock(string actorId, string actorType, string action, string targetId, string outcome, string correlationId)
        {
            _state.Audit.Add(new OperationsAuditEntry
            {
                AuditId = Guid.NewGuid().ToString("N"), Timestamp = DateTimeOffset.UtcNow,
                ActorId = actorId, ActorType = actorType, Action = action, TargetId = targetId,
                Outcome = outcome, CorrelationId = correlationId,
            });
            if (_state.Audit.Count > 5000)
                _state.Audit.RemoveRange(0, _state.Audit.Count - 5000);
        }

        private State Load()
        {
            try
            {
                State state = File.Exists(_path)
                    ? JsonSerializer.Deserialize<State>(File.ReadAllText(_path), JsonOptions) ?? new State()
                    : new State();
                state.Jobs ??= [];
                state.DeploymentReceipts ??= [];
                state.SupportSessions ??= [];
                state.SupportMessages ??= [];
                state.Audit ??= [];
                return state;
            }
            catch
            {
                return new State();
            }
        }

        private void SaveNoLock()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_state, JsonOptions));
            File.Move(temp, _path, true);
        }

        private static OperationsJob Clone(OperationsJob value) => new()
        {
            JobId = value.JobId, CapabilityId = value.CapabilityId, RequestedByDeviceId = value.RequestedByDeviceId,
            Reason = value.Reason, Input = value.Input.Clone(), RiskLevel = value.RiskLevel, Status = value.Status,
            CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt, DecisionByDeviceId = value.DecisionByDeviceId,
            DecisionReason = value.DecisionReason, DecisionAt = value.DecisionAt, LocalCoSignedAt = value.LocalCoSignedAt,
            CompletedAt = value.CompletedAt,
            ResultEvidenceId = value.ResultEvidenceId,
            SourceTaskId = value.SourceTaskId,
        };

        private static bool CanAccessJob(OperationsJob job, string deviceId, bool allowWebRelay) =>
            job.RequestedByDeviceId == deviceId
            || (allowWebRelay && job.RequestedByDeviceId == "web-relay");

        private static OperationsSupportSession Clone(OperationsSupportSession value) => new()
        {
            SessionId = value.SessionId,
            RequestedByDeviceId = value.RequestedByDeviceId,
            Mode = value.Mode,
            Reason = value.Reason,
            Status = value.Status,
            CreatedAt = value.CreatedAt,
            ExpiresAt = value.ExpiresAt,
            LocalConsentAt = value.LocalConsentAt,
        };

        private static OperationsSupportMessage Clone(OperationsSupportMessage value) => new()
        {
            MessageId = value.MessageId,
            SessionId = value.SessionId,
            Source = value.Source,
            Text = value.Text,
            SourceTaskId = value.SourceTaskId,
            CreatedAt = value.CreatedAt,
        };
    }
}
