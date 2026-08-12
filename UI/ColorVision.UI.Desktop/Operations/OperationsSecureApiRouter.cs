using System.Text.Json;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsSecureRequest
    {
        public string Method { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>();

        public byte[] Body { get; init; } = [];

        public string BodyText => System.Text.Encoding.UTF8.GetString(Body);
    }

    public sealed class OperationsSecureApiRouter
    {
        private const string ApiPrefix = "/ops/v1";
        private static readonly TimeSpan LiveMonitorAuditInterval = TimeSpan.FromMinutes(5);
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly OperationsPairingService _pairing;
        private readonly OperationsRequestAuthenticator _authenticator;
        private readonly OperationsWorkStore _workStore;
        private readonly OperationsAlertService _alerts;
        private readonly Func<object> _snapshotProvider;
        private readonly Func<string, OperationsActionResult>? _actionExecutor;
        private readonly IOperationsServiceHealthProvider _serviceHealthProvider;
        private readonly OperationsDiagnosticBundleService? _diagnosticBundles;
        private readonly OperationsWindowSnapshotService? _windowSnapshots;
        private readonly IOperationsRuntimePerformanceProvider _runtimePerformance;
        private readonly IOperationsFlowRuntimeStatusProvider _flowRuntimeStatus;
        private readonly IOperationsFlowRuntimeController _flowRuntimeController;
        private readonly IOperationsMqttRestartController _mqttRestartController;
        private readonly IOperationsApplicationRestartController _applicationRestartController;
        private readonly IOperationsDeviceHealthProvider _deviceHealthProvider;
        private readonly IOperationsMessageChannelHealthProvider _messageChannelHealthProvider;

        public OperationsSecureApiRouter(
            OperationsPairingService pairing,
            OperationsRequestAuthenticator authenticator,
            OperationsWorkStore workStore,
            Func<object> snapshotProvider,
            OperationsAlertService? alerts = null,
            Func<string, OperationsActionResult>? actionExecutor = null,
            IOperationsServiceHealthProvider? serviceHealthProvider = null,
            OperationsDiagnosticBundleService? diagnosticBundles = null,
            OperationsWindowSnapshotService? windowSnapshots = null,
            IOperationsRuntimePerformanceProvider? runtimePerformance = null,
            IOperationsFlowRuntimeStatusProvider? flowRuntimeStatus = null,
            IOperationsFlowRuntimeController? flowRuntimeController = null,
            IOperationsMqttRestartController? mqttRestartController = null,
            IOperationsApplicationRestartController? applicationRestartController = null,
            IOperationsDeviceHealthProvider? deviceHealthProvider = null,
            IOperationsMessageChannelHealthProvider? messageChannelHealthProvider = null)
        {
            _pairing = pairing;
            _authenticator = authenticator;
            _workStore = workStore;
            _snapshotProvider = snapshotProvider;
            _alerts = alerts ?? new OperationsAlertService();
            _actionExecutor = actionExecutor;
            _serviceHealthProvider = serviceHealthProvider ?? UnavailableOperationsServiceHealthProvider.Instance;
            _diagnosticBundles = diagnosticBundles;
            _windowSnapshots = windowSnapshots;
            _runtimePerformance = runtimePerformance ?? new OperationsRuntimePerformanceService();
            _flowRuntimeStatus = flowRuntimeStatus ?? UnavailableOperationsFlowRuntimeStatusProvider.Instance;
            _flowRuntimeController = flowRuntimeController ?? UnavailableOperationsFlowRuntimeController.Instance;
            _mqttRestartController = mqttRestartController ?? UnavailableOperationsMqttRestartController.Instance;
            _applicationRestartController = applicationRestartController ?? UnavailableOperationsApplicationRestartController.Instance;
            _deviceHealthProvider = deviceHealthProvider ?? UnavailableOperationsDeviceHealthProvider.Instance;
            _messageChannelHealthProvider = messageChannelHealthProvider ?? UnavailableOperationsMessageChannelHealthProvider.Instance;
        }

        public OperationsApiResponse Handle(OperationsSecureRequest request)
        {
            string correlationId = ResolveCorrelationId(request.Headers);
            if (!request.Path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
                return Error(404, correlationId, "endpoint_not_found", "The requested Operations API endpoint was not found.");
            if (request.Query.ContainsKey("token") || request.Query.ContainsKey("access_token"))
                return Error(400, correlationId, "query_credentials_not_allowed", "Credentials must not be supplied in the request URL.");

            if (request.Path.Equals($"{ApiPrefix}/pairing/claim", StringComparison.OrdinalIgnoreCase))
                return HandlePairingClaim(request, correlationId);
            if (request.Path.Equals($"{ApiPrefix}/pairing/status", StringComparison.OrdinalIgnoreCase))
                return HandlePairingStatus(request, correlationId);

            OperationsAuthenticationResult authentication = _authenticator.Authenticate(
                request.Method, request.Path, request.Headers, request.Body);
            if (!authentication.Success || authentication.Device == null)
                return Error(401, correlationId, authentication.ErrorCode, "A valid signed device request is required.");

            if (request.Path.Equals($"{ApiPrefix}/capabilities", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.capabilities.read", new
                {
                    capabilities = OperationsCapabilityCatalog.GetAll(),
                    count = OperationsCapabilityCatalog.GetAll().Count,
                });

            if (request.Path.Equals($"{ApiPrefix}/snapshot", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.status.read",
                    OperationsSafeSnapshotFactory.Create(_snapshotProvider()));

            if (request.Path.StartsWith($"{ApiPrefix}/actions/window/", StringComparison.OrdinalIgnoreCase))
                return HandleWindowAction(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/alerts", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<OperationsAlert> alerts = _alerts.GetRecent();
                return GetOnly(request, correlationId, authentication.Device, "ops.alerts.read", new
                {
                    alerts,
                    count = alerts.Count,
                    generatedAt = DateTimeOffset.UtcNow,
                });
            }

            if (request.Path.Equals($"{ApiPrefix}/diagnostics/connection", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<OperationsAlert> alerts = _alerts.GetRecent();
                OperationsServiceHealthReport serviceHealth = CaptureServiceHealth();
                OperationsDeviceHealthSnapshot deviceHealth = CaptureDeviceHealth();
                OperationsMessageChannelHealthSnapshot messageChannel = CaptureMessageChannelHealth();
                int pendingJobCount = _workStore.GetJobsForDevice(authentication.Device.DeviceId).Count(item => item.Status is
                    "awaiting_mobile_approval" or "awaiting_local_cosign" or "approved_local" or "approved_mobile" or "executing");
                return GetOnly(request, correlationId, authentication.Device, "ops.diagnostics.read", new
                {
                    channel = "ready",
                    serverUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    applicationVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    desktop = OperationsDesktopActionService.CaptureState(),
                    availableCapabilityCount = OperationsCapabilityCatalog.GetAll().Count(item => item.Available),
                    alertCount = alerts.Count,
                    pendingJobCount,
                    serviceHealthAvailable = serviceHealth.Available,
                    unhealthyServiceCount = serviceHealth.Services.Count(item => !item.Healthy),
                    deviceHealthAvailable = deviceHealth.Available,
                    configuredDeviceCount = deviceHealth.TotalCount,
                    readyDeviceCount = deviceHealth.ReadyCount,
                    busyDeviceCount = deviceHealth.BusyCount,
                    deviceAttentionCount = deviceHealth.AttentionCount,
                    offlineDeviceCount = deviceHealth.OfflineCount,
                    uninitializedDeviceCount = deviceHealth.UninitializedCount,
                    unauthorizedDeviceCount = deviceHealth.UnauthorizedCount,
                    unclassifiedUnavailableDeviceCount = deviceHealth.UnclassifiedUnavailableCount,
                    messageChannelAvailable = messageChannel.Available,
                    messageChannelState = messageChannel.State,
                    messageChannelConnected = messageChannel.Connected,
                    messageChannelSubscriptionReady = messageChannel.SubscriptionReady,
                });
            }

            if (request.Path.Equals($"{ApiPrefix}/diagnostics/recent-events", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.diagnostics.read", _alerts.GetDigest());

            if (request.Path.Equals($"{ApiPrefix}/services/health", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.diagnostics.read", CaptureServiceHealth());

            if (request.Path.Equals($"{ApiPrefix}/devices/health", StringComparison.OrdinalIgnoreCase))
                return HandleDeviceHealth(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/messaging/health", StringComparison.OrdinalIgnoreCase))
                return HandleMessageChannelHealth(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/triage", StringComparison.OrdinalIgnoreCase))
            {
                int pendingJobCount = _workStore.GetJobsForDevice(authentication.Device.DeviceId).Count(item => item.Status is
                    "awaiting_mobile_approval" or "awaiting_local_cosign" or "approved_local" or "approved_mobile" or "executing");
                OperationsTriageReport report = OperationsTriageService.Build(
                    _alerts.GetDigest(), OperationsDesktopActionService.CaptureState(), pendingJobCount,
                    CaptureServiceHealth(), CaptureDeviceHealth(), CaptureMessageChannelHealth());
                return GetOnly(request, correlationId, authentication.Device, "ops.diagnostics.read", report);
            }

            if (request.Path.Equals($"{ApiPrefix}/diagnostics/summary", StringComparison.OrdinalIgnoreCase))
            {
                return GetOnly(request, correlationId, authentication.Device, "ops.diagnostics.read", new
                {
                    application = "ColorVision",
                    applicationVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                    os = Environment.OSVersion.VersionString,
                    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processWorkingSetBytes = Environment.WorkingSet,
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    generatedAt = DateTimeOffset.UtcNow,
                });
            }

            if (request.Path.Equals($"{ApiPrefix}/diagnostics/performance", StringComparison.OrdinalIgnoreCase))
                return HandleRuntimePerformance(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/flow/runtime", StringComparison.OrdinalIgnoreCase))
                return HandleFlowRuntimeStatus(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/monitor", StringComparison.OrdinalIgnoreCase))
                return HandleLiveMonitor(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/jobs", StringComparison.OrdinalIgnoreCase))
                return HandleJobs(request, correlationId, authentication.Device);

            if (TryGetDiagnosticBundleJobId(request.Path, out string diagnosticBundleJobId))
                return HandleDiagnosticBundleDownload(
                    request, correlationId, authentication.Device, diagnosticBundleJobId);

            if (TryGetWindowSnapshotJobId(request.Path, out string windowSnapshotJobId))
                return HandleWindowSnapshotDownload(
                    request, correlationId, authentication.Device, windowSnapshotJobId);

            if (request.Path.StartsWith($"{ApiPrefix}/jobs/", StringComparison.OrdinalIgnoreCase)
                && request.Path.EndsWith("/decision", StringComparison.OrdinalIgnoreCase))
                return HandleJobDecision(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/deployment-receipts", StringComparison.OrdinalIgnoreCase))
                return HandleDeploymentReceipts(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/support-sessions", StringComparison.OrdinalIgnoreCase))
                return HandleSupportSessions(request, correlationId, authentication.Device);

            if (TryGetSupportMessageSessionId(request.Path, out string supportSessionId))
                return HandleSupportMessages(request, correlationId, authentication.Device, supportSessionId);

            if (request.Path.Equals($"{ApiPrefix}/support-messages", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.support.read",
                    new
                    {
                        messages = _workStore.GetSupportMessagesForDevice(authentication.Device.DeviceId)
                            .Select(OperationsSupportSummaryFactory.Create).ToArray(),
                        privacyNotice = OperationsSupportSummaryFactory.PrivacyNotice,
                    });

            if (request.Path.Equals($"{ApiPrefix}/audit", StringComparison.OrdinalIgnoreCase))
            {
                OperationsAuditSummary[] entries = _workStore.GetAudit(30)
                    .Select(OperationsAuditSummaryFactory.Create).ToArray();
                return GetOnly(request, correlationId, authentication.Device, "ops.audit.read", new
                {
                    entries,
                    count = entries.Length,
                    generatedAt = DateTimeOffset.UtcNow,
                    privacyNotice = "The latest 30 audit summaries exclude actor, device, target, and correlation identifiers.",
                });
            }

            return Error(404, correlationId, "endpoint_not_found", "The requested Operations API endpoint was not found.");
        }

        private OperationsServiceHealthReport CaptureServiceHealth()
        {
            try
            {
                return _serviceHealthProvider.Capture();
            }
            catch
            {
                return OperationsServiceHealthReport.CreateUnavailable();
            }
        }

        private OperationsDeviceHealthSnapshot CaptureDeviceHealth()
        {
            try
            {
                return _deviceHealthProvider.Capture();
            }
            catch
            {
                return OperationsDeviceHealthSnapshot.CreateUnavailable();
            }
        }

        private OperationsMessageChannelHealthSnapshot CaptureMessageChannelHealth()
        {
            try
            {
                return _messageChannelHealthProvider.Capture();
            }
            catch
            {
                return OperationsMessageChannelHealthSnapshot.CreateUnavailable();
            }
        }

        private OperationsApiResponse HandleMessageChannelHealth(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for this endpoint.", "GET");
            if (!HasScope(device, "ops.diagnostics.read"))
                return ScopeRequired(correlationId, "ops.diagnostics.read");

            OperationsMessageChannelHealthSnapshot snapshot = CaptureMessageChannelHealth();
            _workStore.RecordAudit(device.DeviceId, "device", "messaging.health.read",
                "message-channel", snapshot.Available ? "completed" : "failed", correlationId);
            return snapshot.Available
                ? Json(200, correlationId, snapshot)
                : Error(503, correlationId, "message_channel_health_unavailable",
                    "The redacted ColorVision message-channel health snapshot is temporarily unavailable.");
        }

        private OperationsApiResponse HandleDeviceHealth(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for this endpoint.", "GET");
            if (!HasScope(device, "ops.diagnostics.read"))
                return ScopeRequired(correlationId, "ops.diagnostics.read");

            OperationsDeviceHealthSnapshot snapshot = CaptureDeviceHealth();
            _workStore.RecordAudit(device.DeviceId, "device", "devices.health.read",
                "device-health", snapshot.Available ? "completed" : "failed", correlationId);
            return snapshot.Available
                ? Json(200, correlationId, snapshot)
                : Error(503, correlationId, "device_health_unavailable",
                    "The aggregate inspection-device health snapshot is temporarily unavailable.");
        }

        private OperationsApiResponse HandleRuntimePerformance(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for this endpoint.", "GET");
            if (!HasScope(device, "ops.diagnostics.read"))
                return ScopeRequired(correlationId, "ops.diagnostics.read");
            try
            {
                OperationsRuntimePerformanceSnapshot snapshot = _runtimePerformance.Capture();
                _workStore.RecordAudit(device.DeviceId, "device", "diagnostics.performance.read",
                    "runtime-performance", "completed", correlationId);
                return Json(200, correlationId, snapshot);
            }
            catch
            {
                _workStore.RecordAudit(device.DeviceId, "device", "diagnostics.performance.read",
                    "runtime-performance", "failed", correlationId);
                return Error(503, correlationId, "performance_snapshot_unavailable",
                    "The bounded runtime performance snapshot is currently unavailable.");
            }
        }

        private OperationsApiResponse HandleFlowRuntimeStatus(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for this endpoint.", "GET");
            if (!HasScope(device, "ops.diagnostics.read"))
                return ScopeRequired(correlationId, "ops.diagnostics.read");
            try
            {
                OperationsFlowRuntimeStatus status = _flowRuntimeStatus.Capture();
                _workStore.RecordAudit(device.DeviceId, "device", "flow.runtime.read",
                    "flow-runtime", "success", correlationId);
                return Json(200, correlationId, status);
            }
            catch
            {
                _workStore.RecordAudit(device.DeviceId, "device", "flow.runtime.read",
                    "flow-runtime", "failed", correlationId);
                return Error(503, correlationId, "flow_runtime_unavailable",
                    "The aggregate flow runtime status is temporarily unavailable.");
            }
        }

        private OperationsApiResponse HandleLiveMonitor(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for this endpoint.", "GET");
            if (!HasScope(device, "ops.diagnostics.read"))
                return ScopeRequired(correlationId, "ops.diagnostics.read");
            try
            {
                OperationsLiveMonitorSnapshot snapshot = OperationsLiveMonitorSnapshotFactory.Create(
                    _flowRuntimeStatus.Capture(),
                    _runtimePerformance.Capture(),
                    _alerts.GetRecent(),
                    CaptureDeviceHealth(),
                    messageChannel: CaptureMessageChannelHealth());
                _workStore.RecordAuditThrottled(
                    device.DeviceId,
                    "device",
                    "monitor.read",
                    "live-monitor",
                    "completed",
                    correlationId,
                    LiveMonitorAuditInterval);
                return Json(200, correlationId, snapshot);
            }
            catch
            {
                _workStore.RecordAuditThrottled(
                    device.DeviceId,
                    "device",
                    "monitor.read",
                    "live-monitor",
                    "failed",
                    correlationId,
                    LiveMonitorAuditInterval);
                return Error(503, correlationId, "live_monitor_unavailable",
                    "The bounded live monitor snapshot is temporarily unavailable.");
            }
        }

        private OperationsApiResponse HandleWindowAction(OperationsSecureRequest request, string correlationId, OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use POST for desktop window actions.", "POST");
            if (!HasScope(device, "ops.window.control"))
                return ScopeRequired(correlationId, "ops.window.control");

            string actionId = request.Path.EndsWith("/show", StringComparison.OrdinalIgnoreCase)
                ? OperationsDesktopActionService.ShowWindowAction
                : request.Path.EndsWith("/minimize", StringComparison.OrdinalIgnoreCase)
                    ? OperationsDesktopActionService.MinimizeWindowAction
                    : string.Empty;
            if (string.IsNullOrEmpty(actionId))
                return Error(404, correlationId, "unsupported_action", "The requested desktop window action is not supported.");
            if (_actionExecutor == null)
                return Error(503, correlationId, "action_provider_unavailable", "The desktop action provider is unavailable.");

            try
            {
                OperationsActionResult result = _actionExecutor(actionId);
                _workStore.RecordAudit(device.DeviceId, "device", "desktop.action.execute", actionId,
                    result.Success ? "completed" : "failed", correlationId);
                return result.Success
                    ? Json(200, correlationId, new { result.ActionId, result.Message, completedAt = DateTimeOffset.UtcNow })
                    : Error(409, correlationId, "action_not_completed", result.Message);
            }
            catch (Exception ex)
            {
                _workStore.RecordAudit(device.DeviceId, "device", "desktop.action.execute", actionId,
                    "failed", correlationId);
                return Error(500, correlationId, "action_failed", ex.Message);
            }
        }

        private OperationsApiResponse HandleJobs(OperationsSecureRequest request, string correlationId, OperationsPairedDevice device)
        {
            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasScope(device, "ops.jobs.read"))
                    return ScopeRequired(correlationId, "ops.jobs.read");
                OperationsJobSummary[] jobs = _workStore.GetJobsForDevice(device.DeviceId)
                    .Select(OperationsJobSummaryFactory.Create).ToArray();
                return Json(200, correlationId, new { jobs, count = jobs.Length });
            }
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET or POST for jobs.", "GET, POST");
            if (!HasScope(device, "ops.jobs.create"))
                return ScopeRequired(correlationId, "ops.jobs.create");
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                JsonElement root = document.RootElement;
                string capabilityId = RequiredString(root, "capabilityId");
                string reason = OptionalString(root, "reason", 200);
                JsonElement input = root.TryGetProperty("input", out JsonElement inputElement)
                    ? inputElement : JsonDocument.Parse("{}").RootElement;
                if (capabilityId == "ops.flow.cancel"
                    && (input.ValueKind != JsonValueKind.Object || input.EnumerateObject().Any()))
                    return Error(400, correlationId, "flow_cancel_input_not_allowed",
                        "Flow cancellation accepts no remote input fields.");
                OperationsJob job = _workStore.CreateJob(capabilityId, device.DeviceId, reason, input, correlationId);
                return Json(202, correlationId, new { job = OperationsJobSummaryFactory.Create(job) });
            }
            catch (JsonException)
            {
                return Error(400, correlationId, "invalid_json", "The job body is not valid JSON.");
            }
            catch (InvalidOperationException ex)
            {
                return Error(400, correlationId, ex.Message, "The job request is invalid.");
            }
        }

        private OperationsApiResponse HandleJobDecision(OperationsSecureRequest request, string correlationId, OperationsPairedDevice device)
        {
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use POST for approval decisions.", "POST");
            if (!HasScope(device, "ops.approvals.decide"))
                return ScopeRequired(correlationId, "ops.approvals.decide");
            string relative = request.Path[$"{ApiPrefix}/jobs/".Length..];
            string jobId = relative[..^"/decision".Length];
            if (jobId.Length != 32 || !jobId.All(char.IsLetterOrDigit))
                return Error(400, correlationId, "invalid_job_id", "The job id is invalid.");
            OperationsJob? currentJob = _workStore.GetJobForDevice(jobId, device.DeviceId);
            if (currentJob == null)
                return Error(404, correlationId, "job_not_found", "The job was not found for this device.");
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("approved", out JsonElement approvedElement)
                    || approvedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return Error(400, correlationId, "approval_decision_required", "approved must be a boolean.");
                string reason = OptionalString(root, "reason", 200);
                bool approved = approvedElement.GetBoolean();
                bool executesAfterMobileApproval = currentJob.CapabilityId is
                    "ops.flow.cancel" or "ops.service.restart" or "ops.application.restart"
                    or "ops.diagnostics.bundle.create" or "ops.window.snapshot.capture";
                OperationsJob? job = approved
                    && executesAfterMobileApproval
                    && currentJob.Status == "approved_mobile"
                        ? currentJob
                        : _workStore.DecideJob(jobId, device.DeviceId, approved, reason, correlationId);
                if (job is { Status: "approved_mobile" }
                    && job.CapabilityId is "ops.flow.cancel" or "ops.service.restart" or "ops.application.restart"
                        or "ops.diagnostics.bundle.create" or "ops.window.snapshot.capture")
                {
                    OperationsJob? executingJob = _workStore.BeginExecution(job.JobId);
                    if (executingJob == null)
                        return Error(409, correlationId, "job_execution_already_started",
                            "The job execution has already started or completed.");
                    job = executingJob.CapabilityId switch
                    {
                        "ops.flow.cancel" => ExecuteFlowCancellation(executingJob),
                        "ops.service.restart" => ExecuteMqttRestart(executingJob),
                        "ops.application.restart" => ExecuteApplicationRestart(executingJob),
                        "ops.diagnostics.bundle.create" => ExecuteDiagnosticBundle(executingJob),
                        "ops.window.snapshot.capture" => ExecuteWindowSnapshot(executingJob),
                        _ => executingJob,
                    };
                }
                return job == null
                    ? Error(409, correlationId, "job_not_awaiting_decision", "The job is not awaiting a mobile decision.")
                    : Json(200, correlationId, new { job = OperationsJobSummaryFactory.Create(job) });
            }
            catch (JsonException)
            {
                return Error(400, correlationId, "invalid_json", "The approval body is not valid JSON.");
            }
            catch (InvalidOperationException ex)
            {
                return Error(400, correlationId, ex.Message, "The approval decision is invalid.");
            }
        }

        private OperationsJob ExecuteFlowCancellation(OperationsJob job)
        {
            OperationsFlowCancelResult result;
            try
            {
                result = _flowRuntimeController.RequestCancelCurrentFlow();
            }
            catch
            {
                result = new OperationsFlowCancelResult(false, "flow_control_failed",
                    "The primary flow cancellation request failed.");
            }
            return _workStore.CompleteJob(job.JobId, result.Accepted, $"flow_cancel:{result.Code}") ?? job;
        }

        private OperationsJob ExecuteMqttRestart(OperationsJob job)
        {
            OperationsMqttRestartResult result;
            try
            {
                result = _mqttRestartController.Restart();
            }
            catch
            {
                result = new OperationsMqttRestartResult(false, "mqtt_restart_controller_failed");
            }
            return _workStore.CompleteJob(job.JobId, result.Success, result.EvidenceId) ?? job;
        }

        private OperationsJob ExecuteApplicationRestart(OperationsJob job)
        {
            OperationsApplicationRestartResult result;
            try
            {
                result = _applicationRestartController.RequestRestart(job.JobId);
            }
            catch
            {
                result = new OperationsApplicationRestartResult(
                    false, "application_restart:controller_failed");
            }
            return result.Accepted
                ? job
                : _workStore.CompleteJob(job.JobId, false, result.EvidenceId) ?? job;
        }

        private OperationsJob ExecuteDiagnosticBundle(OperationsJob job)
        {
            if (_diagnosticBundles == null)
                return _workStore.CompleteJob(job.JobId, false,
                    "diagnostic_bundle_provider_unavailable") ?? job;
            try
            {
                OperationsDiagnosticBundleResult bundle = _diagnosticBundles.Create(
                    _snapshotProvider, _alerts.GetDigest(), CaptureServiceHealth());
                return _workStore.CompleteJob(job.JobId, true, bundle.BundleId) ?? job;
            }
            catch (Exception ex)
            {
                return _workStore.CompleteJob(job.JobId, false,
                    $"diagnostic_bundle_error:{ex.GetType().Name}") ?? job;
            }
        }

        private OperationsJob ExecuteWindowSnapshot(OperationsJob job)
        {
            if (_windowSnapshots == null)
                return _workStore.CompleteJob(job.JobId, false,
                    "window_snapshot_provider_unavailable") ?? job;
            try
            {
                OperationsWindowSnapshotResult snapshot = _windowSnapshots.Create();
                string evidenceId = OperationsWindowSnapshotService.EvidencePrefix + snapshot.SnapshotId;
                return _workStore.CompleteJob(job.JobId, true, evidenceId) ?? job;
            }
            catch (Exception ex)
            {
                return _workStore.CompleteJob(job.JobId, false,
                    $"window_snapshot_error:{ex.GetType().Name}") ?? job;
            }
        }

        private OperationsApiResponse HandleDiagnosticBundleDownload(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device,
            string jobId)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for diagnostic bundle downloads.", "GET");
            if (!HasScope(device, "ops.diagnostics.bundle.read") && !HasScope(device, "ops.jobs.read"))
                return ScopeRequired(correlationId, "ops.diagnostics.bundle.read");

            OperationsJob? job = _workStore.GetJobForDevice(jobId, device.DeviceId, allowWebRelay: false);
            if (job == null || job.CapabilityId != "ops.diagnostics.bundle.create")
                return Error(404, correlationId, "diagnostic_bundle_not_found", "The diagnostic bundle was not found for this device.");
            if (job.Status != "completed" || string.IsNullOrWhiteSpace(job.ResultEvidenceId))
                return Error(409, correlationId, "diagnostic_bundle_not_ready", "The diagnostic bundle is not ready for download.");
            if (_diagnosticBundles == null)
                return Error(503, correlationId, "diagnostic_bundle_provider_unavailable", "Diagnostic bundle downloads are unavailable.");

            OperationsDiagnosticBundleLookupStatus lookup = _diagnosticBundles.TryRead(job.ResultEvidenceId, out OperationsDiagnosticBundleResult? bundle);
            if (lookup == OperationsDiagnosticBundleLookupStatus.Expired)
                return Error(410, correlationId, "diagnostic_bundle_expired", "The diagnostic bundle download has expired.");
            if (lookup == OperationsDiagnosticBundleLookupStatus.TooLarge)
                return Error(409, correlationId, "diagnostic_bundle_too_large", "The diagnostic bundle exceeds the mobile download limit.");
            if (lookup == OperationsDiagnosticBundleLookupStatus.UnsupportedFormat)
                return Error(409, correlationId, "diagnostic_bundle_regeneration_required", "The diagnostic bundle uses an older format and must be regenerated.");
            if (lookup == OperationsDiagnosticBundleLookupStatus.ReadFailed)
                return Error(503, correlationId, "diagnostic_bundle_read_failed", "The diagnostic bundle could not be read.");
            if (lookup != OperationsDiagnosticBundleLookupStatus.Available || bundle == null)
                return Error(404, correlationId, "diagnostic_bundle_not_found", "The diagnostic bundle file is unavailable.");

            _workStore.RecordAudit(device.DeviceId, "device", "diagnostic.bundle.download", jobId,
                "completed", correlationId);
            Dictionary<string, string> headers = SecurityHeaders();
            headers["Content-Disposition"] = "attachment; filename=\"ColorVision-diagnostics.zip\"";
            headers["X-CV-Content-SHA256"] = bundle.Sha256;
            headers["X-CV-Bundle-Expires-At"] = bundle.ExpiresAt.ToString("O");
            return new OperationsApiResponse
            {
                StatusCode = 200,
                ContentType = "application/zip",
                BodyBytes = bundle.Data,
                Headers = headers,
            };
        }

        private OperationsApiResponse HandleWindowSnapshotDownload(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device,
            string jobId)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for window snapshot downloads.", "GET");
            if (!HasScope(device, "ops.window.snapshot.read") && !HasScope(device, "ops.jobs.read"))
                return ScopeRequired(correlationId, "ops.window.snapshot.read");

            OperationsJob? job = _workStore.GetJobForDevice(jobId, device.DeviceId, allowWebRelay: false);
            if (job == null || job.CapabilityId != "ops.window.snapshot.capture")
                return Error(404, correlationId, "window_snapshot_not_found", "The window snapshot was not found for this device.");
            if (job.Status != "completed")
                return Error(409, correlationId, "window_snapshot_not_ready", "The window snapshot is not ready for download.");
            if (!OperationsWindowSnapshotService.TryGetSnapshotId(job.ResultEvidenceId, out string snapshotId))
                return Error(404, correlationId, "window_snapshot_not_found", "The one-time window snapshot is unavailable.");
            if (_windowSnapshots == null)
                return Error(503, correlationId, "window_snapshot_provider_unavailable", "Window snapshot downloads are unavailable.");

            OperationsWindowSnapshotLookupStatus lookup = _windowSnapshots.TryTake(
                snapshotId, out OperationsWindowSnapshotResult? snapshot);
            if (lookup == OperationsWindowSnapshotLookupStatus.Expired)
                return Error(410, correlationId, "window_snapshot_expired", "The window snapshot download has expired.");
            if (lookup == OperationsWindowSnapshotLookupStatus.TooLarge)
                return Error(409, correlationId, "window_snapshot_too_large", "The window snapshot exceeds the mobile download limit.");
            if (lookup == OperationsWindowSnapshotLookupStatus.UnsupportedFormat)
                return Error(409, correlationId, "window_snapshot_format_rejected", "The window snapshot format is invalid.");
            if (lookup == OperationsWindowSnapshotLookupStatus.ReadFailed)
                return Error(503, correlationId, "window_snapshot_read_failed", "The window snapshot could not be read.");
            if (lookup != OperationsWindowSnapshotLookupStatus.Available || snapshot == null)
                return Error(404, correlationId, "window_snapshot_not_found", "The one-time window snapshot is unavailable.");

            _workStore.ClearJobEvidence(jobId, job.ResultEvidenceId!);
            _workStore.RecordAudit(device.DeviceId, "device", "window.snapshot.download", jobId,
                "completed", correlationId);
            Dictionary<string, string> headers = SecurityHeaders();
            headers["Content-Disposition"] = "inline; filename=\"ColorVision-window-snapshot.jpg\"";
            headers["X-CV-Content-SHA256"] = snapshot.Sha256;
            headers["X-CV-Snapshot-Expires-At"] = snapshot.ExpiresAt.ToString("O");
            return new OperationsApiResponse
            {
                StatusCode = 200,
                ContentType = "image/jpeg",
                BodyBytes = snapshot.Data,
                Headers = headers,
            };
        }

        private OperationsApiResponse HandleDeploymentReceipts(OperationsSecureRequest request, string correlationId, OperationsPairedDevice device)
        {
            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasScope(device, "ops.deployments.read"))
                    return ScopeRequired(correlationId, "ops.deployments.read");
                var receipts = _workStore.GetDeploymentReceipts();
                return Json(200, correlationId, new { receipts, count = receipts.Count });
            }
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET or POST for deployment receipts.", "GET, POST");
            if (!HasScope(device, "ops.deployments.receipt.create"))
                return ScopeRequired(correlationId, "ops.deployments.receipt.create");
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                JsonElement root = document.RootElement;
                OperationsDeploymentReceipt receipt = _workStore.AddDeploymentReceipt(device.DeviceId,
                    RequiredString(root, "releaseId"), RequiredString(root, "version"), RequiredString(root, "status"),
                    OptionalString(root, "evidenceSha256", 64), correlationId);
                return Json(201, correlationId, new { receipt });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return Error(400, correlationId, ex is InvalidOperationException ? ex.Message : "invalid_json", "The deployment receipt is invalid.");
            }
        }

        private OperationsApiResponse HandleSupportSessions(OperationsSecureRequest request, string correlationId, OperationsPairedDevice device)
        {
            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasScope(device, "ops.support.read"))
                    return ScopeRequired(correlationId, "ops.support.read");
                OperationsSupportSessionSummary[] sessions = _workStore.GetSupportSessionsForDevice(device.DeviceId)
                    .Select(session => OperationsSupportSummaryFactory.Create(
                        session, _workStore.GetSupportMessagesForSession(session.SessionId).Count))
                    .ToArray();
                return Json(200, correlationId, new
                {
                    sessions,
                    count = sessions.Length,
                    privacyNotice = OperationsSupportSummaryFactory.PrivacyNotice,
                });
            }
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET or POST for support sessions.", "GET, POST");
            if (!HasScope(device, "ops.support.request"))
                return ScopeRequired(correlationId, "ops.support.request");
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                JsonElement root = document.RootElement;
                int duration = root.TryGetProperty("durationMinutes", out JsonElement durationElement) && durationElement.TryGetInt32(out int value) ? value : 15;
                OperationsSupportSession session = _workStore.RequestSupport(device.DeviceId,
                    RequiredString(root, "mode"), OptionalString(root, "reason", 200), duration, correlationId);
                return Json(202, correlationId, new
                {
                    session = OperationsSupportSummaryFactory.Create(
                        session, _workStore.GetSupportMessagesForSession(session.SessionId).Count),
                    privacyNotice = OperationsSupportSummaryFactory.PrivacyNotice,
                });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return Error(400, correlationId, ex is InvalidOperationException ? ex.Message : "invalid_json", "The support request is invalid.");
            }
        }

        private OperationsApiResponse HandleSupportMessages(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device,
            string sessionId)
        {
            OperationsSupportSession? session = _workStore.GetSupportSessionForDevice(sessionId, device.DeviceId);
            if (session == null)
                return Error(404, correlationId, "support_session_not_found", "The support session was not found.");

            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasScope(device, "ops.support.read"))
                    return ScopeRequired(correlationId, "ops.support.read");
                OperationsSupportMessageSummary[] messages = _workStore.GetSupportMessagesForSession(sessionId)
                    .Select(OperationsSupportSummaryFactory.Create).ToArray();
                return Json(200, correlationId, new
                {
                    session = OperationsSupportSummaryFactory.Create(session, messages.Length),
                    messages,
                    count = messages.Length,
                    privacyNotice = OperationsSupportSummaryFactory.PrivacyNotice,
                });
            }
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET or POST for support messages.", "GET, POST");
            if (!HasScope(device, "ops.support.request"))
                return ScopeRequired(correlationId, "ops.support.request");
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                string text = RequiredString(document.RootElement, "text").Trim();
                if (text.Length > 500)
                    throw new InvalidOperationException("support_message_too_long");
                OperationsSupportMessage message = _workStore.AddDeviceSupportMessage(
                    sessionId, device.DeviceId, text, correlationId);
                return Json(201, correlationId, new
                {
                    message = OperationsSupportSummaryFactory.Create(message),
                    session = OperationsSupportSummaryFactory.Create(
                        session, _workStore.GetSupportMessagesForSession(sessionId).Count),
                });
            }
            catch (JsonException)
            {
                return Error(400, correlationId, "invalid_json", "The support message body is not valid JSON.");
            }
            catch (InvalidOperationException ex)
            {
                int statusCode = ex.Message == "support_session_not_active" ? 409 : 400;
                return Error(statusCode, correlationId, ex.Message, "The support message was not accepted.");
            }
        }

        private static bool TryGetSupportMessageSessionId(string path, out string sessionId)
        {
            string prefix = $"{ApiPrefix}/support-sessions/";
            const string suffix = "/messages";
            sessionId = string.Empty;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;
            string value = path[prefix.Length..^suffix.Length];
            if (value.Length is < 1 or > 64 || value.Any(ch => !char.IsLetterOrDigit(ch) && ch is not ('-' or '_')))
                return false;
            sessionId = value;
            return true;
        }

        private static bool TryGetDiagnosticBundleJobId(string path, out string jobId)
        {
            string prefix = $"{ApiPrefix}/jobs/";
            const string suffix = "/diagnostic-bundle";
            jobId = string.Empty;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;
            string value = path[prefix.Length..^suffix.Length];
            if (value.Length != 32 || value.Any(ch => !char.IsAsciiHexDigit(ch)))
                return false;
            jobId = value;
            return true;
        }

        private static bool TryGetWindowSnapshotJobId(string path, out string jobId)
        {
            string prefix = $"{ApiPrefix}/jobs/";
            const string suffix = "/window-snapshot";
            jobId = string.Empty;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;
            string value = path[prefix.Length..^suffix.Length];
            if (value.Length != 32 || value.Any(ch => !char.IsAsciiHexDigit(ch)))
                return false;
            jobId = value;
            return true;
        }

        private OperationsApiResponse HandlePairingClaim(OperationsSecureRequest request, string correlationId)
        {
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use POST for pairing claims.", "POST");
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                JsonElement root = document.RootElement;
                string pairingId = RequiredString(root, "pairingId");
                string deviceId = RequiredString(root, "deviceId");
                string deviceName = RequiredString(root, "deviceName");
                string publicKey = RequiredString(root, "publicKeySpki");
                string signature = RequiredString(root, "signature");
                (bool success, string error) = _pairing.SubmitClaim(pairingId, deviceId, deviceName, publicKey, signature);
                return success
                    ? Json(202, correlationId, new { status = "pending", pairingId })
                    : Error(400, correlationId, error, "The pairing claim was rejected.");
            }
            catch (JsonException)
            {
                return Error(400, correlationId, "invalid_json", "The pairing claim body is not valid JSON.");
            }
            catch (InvalidOperationException)
            {
                return Error(400, correlationId, "missing_pairing_field", "A required pairing field is missing.");
            }
        }

        private OperationsApiResponse HandlePairingStatus(OperationsSecureRequest request, string correlationId)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for pairing status.", "GET");
            if (!request.Query.TryGetValue("pairingId", out string? pairingId)
                || !request.Query.TryGetValue("deviceId", out string? deviceId))
                return Error(400, correlationId, "pairing_identity_required", "pairingId and deviceId are required.");

            OperationsPairingClaim? claim = _pairing.GetClaim(pairingId, deviceId);
            if (claim == null)
                return Error(404, correlationId, "pairing_claim_not_found", "The pairing claim was not found.");
            return Json(200, correlationId, new
            {
                claim.Status,
                scopes = claim.Status == "approved" ? OperationsPairingService.InitialScopes : [],
            });
        }

        private static OperationsApiResponse GetOnly(
            OperationsSecureRequest request,
            string correlationId,
            OperationsPairedDevice device,
            string requiredScope,
            object data)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return Error(405, correlationId, "method_not_allowed", "Use GET for this endpoint.", "GET");
            if (!device.Scopes.Contains(requiredScope, StringComparer.Ordinal))
                return Error(403, correlationId, "scope_required", $"The device requires scope '{requiredScope}'.");
            return Json(200, correlationId, data);
        }

        private static string RequiredString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidOperationException();
            return value.GetString()!;
        }

        private static string OptionalString(JsonElement root, string name, int maxLength)
        {
            if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                return string.Empty;
            if (value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"invalid_{name}");
            string text = value.GetString()?.Trim() ?? string.Empty;
            if (text.Length > maxLength)
                throw new InvalidOperationException($"{name}_too_long");
            return text;
        }

        private static bool HasScope(OperationsPairedDevice device, string scope) => device.Scopes.Contains(scope, StringComparer.Ordinal);

        private static OperationsApiResponse ScopeRequired(string correlationId, string scope) =>
            Error(403, correlationId, "scope_required", $"The device requires scope '{scope}'.");

        private static OperationsApiResponse Json(int statusCode, string correlationId, object data)
        {
            return new OperationsApiResponse
            {
                StatusCode = statusCode,
                Body = JsonSerializer.Serialize(new
                {
                    schemaVersion = OperationsCapabilityCatalog.SchemaVersion,
                    requestId = Guid.NewGuid().ToString("N"),
                    correlationId,
                    serverTime = DateTimeOffset.UtcNow,
                    data,
                    error = (object?)null,
                }, JsonOptions),
                Headers = SecurityHeaders(),
            };
        }

        private static OperationsApiResponse Error(int statusCode, string correlationId, string code, string message, string? allow = null)
        {
            Dictionary<string, string> headers = SecurityHeaders();
            if (allow != null)
                headers["Allow"] = allow;
            return new OperationsApiResponse
            {
                StatusCode = statusCode,
                Body = JsonSerializer.Serialize(new
                {
                    schemaVersion = OperationsCapabilityCatalog.SchemaVersion,
                    requestId = Guid.NewGuid().ToString("N"),
                    correlationId,
                    serverTime = DateTimeOffset.UtcNow,
                    data = (object?)null,
                    error = new { code, message },
                }, JsonOptions),
                Headers = headers,
            };
        }

        private static Dictionary<string, string> SecurityHeaders() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cache-Control"] = "no-store",
            ["Pragma"] = "no-cache",
            ["X-Content-Type-Options"] = "nosniff",
            ["Referrer-Policy"] = "no-referrer",
            ["Content-Security-Policy"] = "default-src 'none'",
        };

        private static string ResolveCorrelationId(IReadOnlyDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("X-Correlation-Id", out string? value))
                return Guid.NewGuid().ToString("N");
            string normalized = new(value.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(64).ToArray());
            return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N") : normalized;
        }
    }
}
