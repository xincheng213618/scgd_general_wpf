namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsCapabilityCatalog
    {
        public const string SchemaVersion = "1.0";

        private static readonly IReadOnlyList<OperationsCapabilityDescriptor> Capabilities =
        [
            new()
            {
                Id = "ops.status.read",
                Title = "Read operational status",
                Description = "Read the current ColorVision host, process, window, and LAN endpoint status.",
                Category = "status",
                Provider = "desktop.lan-remote",
                Permission = "ops.status.read",
                DiscoverableOn = ["desktop", "android", "copilot"],
                OutputSchema = new { type = "object", additionalProperties = true },
                Evidence = ["host.snapshot"],
                Available = true,
            },
            new()
            {
                Id = "ops.logs.read",
                Title = "Read recent operational logs",
                Description = "Read a bounded and redacted window of recent ColorVision application logs.",
                Category = "diagnostics",
                Provider = "desktop.lan-remote",
                Permission = "ops.logs.read",
                DiscoverableOn = ["desktop", "android", "copilot"],
                InputSchema = new
                {
                    type = "object",
                    properties = new { count = new { type = "integer", minimum = 1, maximum = 120 } },
                    additionalProperties = false,
                },
                OutputSchema = new { type = "object", additionalProperties = true },
                Audit = new OperationsAuditPolicy { Required = true, Redact = ["$.lines"] },
                Evidence = ["application.log"],
                Available = false,
                BlockedReason = "raw_log_surface_not_exposed_to_field_clients",
            },
            new()
            {
                Id = "ops.alerts.read",
                Title = "Read recent operational alerts",
                Description = "Read bounded warning, error, and fatal summaries derived from the latest application log with credential and user-profile redaction.",
                Category = "alerts",
                Provider = "desktop.operations",
                Permission = "ops.alerts.read",
                DiscoverableOn = ["desktop", "android"],
                InputSchema = new { type = "object", additionalProperties = false },
                OutputSchema = new { type = "object", additionalProperties = true },
                Audit = new OperationsAuditPolicy { Required = true, Redact = ["$.alerts[*].summary"] },
                Evidence = ["application.log.summary"],
                Available = true,
            },
            new()
            {
                Id = "ops.window.show",
                Title = "Show the ColorVision main window",
                Description = "Bring the existing ColorVision main window to the foreground.",
                Category = "desktop-control",
                Provider = "desktop.window",
                RiskLevel = OperationsRiskLevels.LowRisk,
                Permission = "ops.window.show",
                DiscoverableOn = ["desktop", "android", "copilot"],
                Idempotency = "safe",
                Available = false,
                BlockedReason = "secure_pairing_required",
            },
            new()
            {
                Id = "ops.jobs.manage",
                Title = "Submit and review bounded operations jobs",
                Description = "Create bounded operational intents and review their approval state without directly executing privileged commands.",
                Category = "jobs",
                Provider = "desktop.operations",
                RiskLevel = OperationsRiskLevels.LowRisk,
                Permission = "ops.jobs.create",
                DiscoverableOn = ["desktop", "android", "copilot"],
                Audit = new OperationsAuditPolicy { Required = true },
                Evidence = ["job.audit"],
                Available = true,
            },
            new()
            {
                Id = "ops.approvals.decide",
                Title = "Record an operations approval decision",
                Description = "Record a mobile approval or rejection. Privileged jobs remain blocked until a separate local co-sign and broker ticket exist.",
                Category = "approvals",
                Provider = "desktop.operations",
                RiskLevel = OperationsRiskLevels.ApprovalRequired,
                Permission = "ops.approvals.decide",
                DiscoverableOn = ["desktop", "android"],
                Approval = new OperationsApprovalPolicy { Mode = "device-credential", TtlSeconds = 300, SingleUse = true },
                Audit = new OperationsAuditPolicy { Required = true },
                Evidence = ["approval.decision"],
                Available = true,
            },
            new()
            {
                Id = "ops.deployment.receipt.create",
                Title = "Confirm deployment outcome",
                Description = "Submit an installation or verification receipt. This does not initiate deployment.",
                Category = "deployment",
                Provider = "desktop.operations",
                RiskLevel = OperationsRiskLevels.LowRisk,
                Permission = "ops.deployments.receipt.create",
                DiscoverableOn = ["desktop", "android"],
                Audit = new OperationsAuditPolicy { Required = true },
                Evidence = ["deployment.receipt"],
                Available = true,
            },
            new()
            {
                Id = "ops.support.session.request",
                Title = "Request a bounded support session",
                Description = "Request diagnostics or guided support for at most 30 minutes. Local desktop consent is always required.",
                Category = "support",
                Provider = "desktop.operations",
                RiskLevel = OperationsRiskLevels.ApprovalRequired,
                Permission = "ops.support.request",
                DiscoverableOn = ["desktop", "android"],
                Approval = new OperationsApprovalPolicy { Mode = "local-co-sign", TtlSeconds = 1800, SingleUse = true, RequiresLocalCoSign = true },
                Audit = new OperationsAuditPolicy { Required = true },
                Evidence = ["support.session.audit"],
                Available = true,
            },
            new()
            {
                Id = "ops.window.minimize",
                Title = "Minimize the ColorVision main window",
                Description = "Minimize the existing ColorVision main window.",
                Category = "desktop-control",
                Provider = "desktop.window",
                RiskLevel = OperationsRiskLevels.LowRisk,
                Permission = "ops.window.minimize",
                DiscoverableOn = ["desktop", "android", "copilot"],
                Idempotency = "safe",
                Available = false,
                BlockedReason = "secure_pairing_required",
            },
            new()
            {
                Id = "ops.diagnostics.bundle.create",
                Title = "Create a diagnostic bundle",
                Description = "Create a bounded, redacted diagnostic evidence bundle for field support.",
                Category = "diagnostics",
                Provider = "desktop.diagnostics",
                RiskLevel = OperationsRiskLevels.ApprovalRequired,
                Permission = "ops.diagnostics.bundle.create",
                DiscoverableOn = ["desktop", "android", "copilot"],
                TimeoutMs = 60000,
                Idempotency = "keyed",
                SupportsCancellation = true,
                Approval = new OperationsApprovalPolicy
                {
                    Mode = "device-credential",
                    TtlSeconds = 300,
                    SingleUse = true,
                },
                Audit = new OperationsAuditPolicy { Required = true, Redact = ["$.bundle.contents"] },
                Evidence = ["diagnostic.bundle"],
                Available = true,
            },
            new()
            {
                Id = "ops.service.restart",
                Title = "Restart a known Windows service",
                Description = "Restart a catalog-approved Windows service through the privileged broker.",
                Category = "maintenance",
                Provider = "desktop.privileged-broker",
                RiskLevel = OperationsRiskLevels.Privileged,
                Permission = "ops.service.restart",
                DiscoverableOn = ["desktop", "android", "copilot"],
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        serviceId = new { type = "string", @enum = new[] { "mosquitto" } },
                        reason = new { type = "string", maxLength = 200 },
                    },
                    required = new[] { "serviceId" },
                    additionalProperties = false,
                },
                TimeoutMs = 60000,
                Idempotency = "keyed",
                SupportsCancellation = true,
                Approval = new OperationsApprovalPolicy
                {
                    Mode = "local-co-sign",
                    TtlSeconds = 300,
                    SingleUse = true,
                    RequiresLocalCoSign = true,
                },
                Execution = new OperationsExecutionPolicy
                {
                    Target = "service-host",
                    BrokerCapability = "service.restart.known",
                },
                RollbackCapability = "ops.service.start",
                Evidence = ["service.before", "service.after"],
                Available = true,
            },
        ];

        public static IReadOnlyList<OperationsCapabilityDescriptor> GetAll() => Capabilities;
    }
}
