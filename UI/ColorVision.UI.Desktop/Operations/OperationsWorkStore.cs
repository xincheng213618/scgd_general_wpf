using System.IO;
using System.Text.Json;

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
        public string? ResultEvidenceId { get; set; }
        public string? SourceTaskId { get; set; }
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

        public OperationsJob CreateJob(string capabilityId, string deviceId, string reason, JsonElement input, string correlationId)
        {
            if (capabilityId is not ("ops.diagnostics.bundle.create" or "ops.service.restart"))
                throw new InvalidOperationException("capability_not_allowed_for_remote_job");
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
                SourceTaskId = deviceId == "web-relay" ? correlationId : null,
            };
            lock (_syncRoot)
            {
                if (deviceId == "web-relay")
                {
                    OperationsJob? existing = _state.Jobs.FirstOrDefault(item => item.SourceTaskId == correlationId);
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
                if (job == null || job.Status != "awaiting_mobile_approval")
                    return null;
                job.DecisionByDeviceId = deviceId;
                job.DecisionReason = reason;
                job.DecisionAt = DateTimeOffset.UtcNow;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Status = approved ? "awaiting_local_cosign" : "rejected";
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
                if (job == null || job.Status != "approved_local")
                    return null;
                job.Status = success ? "completed" : "failed";
                job.ResultEvidenceId = evidenceId;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                AuditNoLock("operations-broker", "system", "job.complete", jobId, job.Status, Guid.NewGuid().ToString("N"));
                SaveNoLock();
                result = Clone(job);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
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
                return _state.SupportSessions.OrderByDescending(item => item.CreatedAt).ToList();
        }

        public IReadOnlyList<OperationsSupportMessage> GetSupportMessages(int count = 100)
        {
            lock (_syncRoot)
                return _state.SupportMessages.OrderByDescending(item => item.CreatedAt).Take(Math.Clamp(count, 1, 200)).ToList();
        }

        public OperationsSupportMessage AddSupportMessage(string sessionId, string source, string text, string correlationId)
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
                        return existing;
                }
                _state.SupportMessages.Add(message);
                if (_state.SupportMessages.Count > 1000)
                    _state.SupportMessages.RemoveRange(0, _state.SupportMessages.Count - 1000);
                AuditNoLock(source, "support-relay", "support.message.receive", message.MessageId, "received", correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return message;
        }

        public OperationsSupportSession RequestSupport(string deviceId, string mode, string reason, int durationMinutes, string correlationId)
        {
            if (mode is not ("diagnostics" or "guided"))
                throw new InvalidOperationException("unsupported_support_mode");
            int boundedDuration = Math.Clamp(durationMinutes, 5, 30);
            OperationsSupportSession session = new()
            {
                SessionId = Guid.NewGuid().ToString("N"),
                RequestedByDeviceId = deviceId,
                Mode = mode,
                Reason = reason,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(boundedDuration),
            };
            lock (_syncRoot)
            {
                _state.SupportSessions.Add(session);
                AuditNoLock(deviceId, "device", "support.request", session.SessionId, "awaiting_local_consent", correlationId);
                SaveNoLock();
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return session;
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
                result = session;
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return result;
        }

        public IReadOnlyList<OperationsAuditEntry> GetAudit(int count = 100)
        {
            lock (_syncRoot)
                return _state.Audit.OrderByDescending(item => item.Timestamp).Take(Math.Clamp(count, 1, 500)).ToList();
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
            ResultEvidenceId = value.ResultEvidenceId,
            SourceTaskId = value.SourceTaskId,
        };
    }
}
