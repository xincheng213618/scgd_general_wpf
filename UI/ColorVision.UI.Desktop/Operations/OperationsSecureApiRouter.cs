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
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly OperationsPairingService _pairing;
        private readonly OperationsRequestAuthenticator _authenticator;
        private readonly OperationsWorkStore _workStore;
        private readonly OperationsAlertService _alerts;
        private readonly Func<object> _snapshotProvider;

        public OperationsSecureApiRouter(
            OperationsPairingService pairing,
            OperationsRequestAuthenticator authenticator,
            OperationsWorkStore workStore,
            Func<object> snapshotProvider,
            OperationsAlertService? alerts = null)
        {
            _pairing = pairing;
            _authenticator = authenticator;
            _workStore = workStore;
            _snapshotProvider = snapshotProvider;
            _alerts = alerts ?? new OperationsAlertService();
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
                return GetOnly(request, correlationId, authentication.Device, "ops.status.read", _snapshotProvider());

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

            if (request.Path.Equals($"{ApiPrefix}/diagnostics/summary", StringComparison.OrdinalIgnoreCase))
            {
                return GetOnly(request, correlationId, authentication.Device, "ops.diagnostics.read", new
                {
                    host = Environment.MachineName,
                    os = Environment.OSVersion.VersionString,
                    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processWorkingSetBytes = Environment.WorkingSet,
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    generatedAt = DateTimeOffset.UtcNow,
                });
            }

            if (request.Path.Equals($"{ApiPrefix}/jobs", StringComparison.OrdinalIgnoreCase))
                return HandleJobs(request, correlationId, authentication.Device);

            if (request.Path.StartsWith($"{ApiPrefix}/jobs/", StringComparison.OrdinalIgnoreCase)
                && request.Path.EndsWith("/decision", StringComparison.OrdinalIgnoreCase))
                return HandleJobDecision(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/deployment-receipts", StringComparison.OrdinalIgnoreCase))
                return HandleDeploymentReceipts(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/support-sessions", StringComparison.OrdinalIgnoreCase))
                return HandleSupportSessions(request, correlationId, authentication.Device);

            if (request.Path.Equals($"{ApiPrefix}/support-messages", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.support.read",
                    new { messages = _workStore.GetSupportMessages() });

            if (request.Path.Equals($"{ApiPrefix}/audit", StringComparison.OrdinalIgnoreCase))
                return GetOnly(request, correlationId, authentication.Device, "ops.audit.read", new { entries = _workStore.GetAudit() });

            return Error(404, correlationId, "endpoint_not_found", "The requested Operations API endpoint was not found.");
        }

        private OperationsApiResponse HandleJobs(OperationsSecureRequest request, string correlationId, OperationsPairedDevice device)
        {
            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasScope(device, "ops.jobs.read"))
                    return ScopeRequired(correlationId, "ops.jobs.read");
                IReadOnlyList<OperationsJob> jobs = _workStore.GetJobs();
                return Json(200, correlationId, new { jobs, count = jobs.Count });
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
                OperationsJob job = _workStore.CreateJob(capabilityId, device.DeviceId, reason, input, correlationId);
                return Json(202, correlationId, new { job });
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
            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("approved", out JsonElement approvedElement)
                    || approvedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return Error(400, correlationId, "approval_decision_required", "approved must be a boolean.");
                string reason = OptionalString(root, "reason", 200);
                OperationsJob? job = _workStore.DecideJob(jobId, device.DeviceId, approvedElement.GetBoolean(), reason, correlationId);
                return job == null
                    ? Error(409, correlationId, "job_not_awaiting_decision", "The job is not awaiting a mobile decision.")
                    : Json(200, correlationId, new { job });
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
                var sessions = _workStore.GetSupportSessions();
                return Json(200, correlationId, new { sessions, count = sessions.Count });
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
                return Json(202, correlationId, new { session });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return Error(400, correlationId, ex is InvalidOperationException ? ex.Message : "invalid_json", "The support request is invalid.");
            }
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
