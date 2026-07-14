using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotQueryDatabaseSqlTool : ICopilotTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["sql"] = new { type = "string", minLength = 1, maxLength = CopilotDatabaseSqlPolicy.MaximumSqlLength, description = "One read-only MySQL statement: SELECT, SHOW, DESCRIBE, EXPLAIN, TABLE, or WITH ending in SELECT." },
                    ["maxRows"] = new { type = "integer", minimum = 1, maximum = 500, description = "Maximum returned rows. Defaults to 100." },
                    ["timeoutSeconds"] = new { type = "integer", minimum = 1, maximum = 30, description = "Query timeout in seconds. Defaults to 15." },
                },
                ["required"] = new[] { "sql" },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotDatabaseSqlService _service;

        public CopilotQueryDatabaseSqlTool()
            : this(new CopilotDatabaseSqlService())
        {
        }

        public CopilotQueryDatabaseSqlTool(CopilotDatabaseSqlService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "QueryDatabaseSql";

        public string Description => "Run one bounded read-only SQL statement against the configured ColorVision MySQL database. Supports SELECT, SHOW, DESCRIBE, EXPLAIN, TABLE, and read-only CTEs; result rows are capped and sensitive columns are redacted.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            executionTimeout: TimeSpan.FromSeconds(35),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsDatabaseSqlQuery(request);

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return _service.QueryAsync(toolInput, cancellationToken);
        }
    }

    public sealed class CopilotExecuteDatabaseSqlTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["sql"] = new { type = "string", minLength = 1, maxLength = CopilotDatabaseSqlPolicy.MaximumSqlLength, description = "One MySQL data-change or schema-change statement. DELETE, TRUNCATE, and DROP are supported after native approval." },
                    ["timeoutSeconds"] = new { type = "integer", minimum = 1, maximum = 60, description = "Execution timeout in seconds. Defaults to 30." },
                },
                ["required"] = new[] { "sql" },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotDatabaseSqlService _service;

        public CopilotExecuteDatabaseSqlTool()
            : this(new CopilotDatabaseSqlService())
        {
        }

        public CopilotExecuteDatabaseSqlTool(CopilotDatabaseSqlService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "ExecuteDatabaseSql";

        public string Description => "Execute one approved MySQL data-change or schema-change statement against the configured ColorVision database. Supports INSERT, UPDATE, DELETE, REPLACE, CREATE, ALTER, DROP, TRUNCATE, and RENAME; every invocation requires native approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            executionTimeout: TimeSpan.FromSeconds(65),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsDatabaseSqlMutation(request);

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                FailureKind = CopilotToolFailureKind.Authorization,
                Summary = "Database changes require Microsoft Agent Framework approval.",
                ErrorMessage = "The SQL statement was requested without a granted native approval.",
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return _service.ExecuteApprovedAsync(toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            return CopilotDatabaseSqlService.CreateApprovalPresentation(toolInput);
        }

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => "database:configured-mysql";
    }
}
