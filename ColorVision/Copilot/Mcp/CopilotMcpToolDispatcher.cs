#pragma warning disable CA1822,CA1826,CA1859,CA1861
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher :
        ICopilotApplicationCapabilityInvoker,
        ICopilotScopedApplicationCapabilityInvoker,
        ICopilotApprovedApplicationCapabilityInvoker
    {
        private const int MaxAuditEntries = 80;
        private const int DefaultDiagnosticBundleChars = 12000;
        private const int MaxDiagnosticBundleChars = 60000;
        public const string InAppAgentCallerSource = "in-app-agent";

        internal const string InAppAgentFrameworkApprovedCallerSource = "in-app-agent-framework-approved";
        private const string LiveContextResourceUri = "colorvision://live-context/current";
        private const string WorkspaceResourceUri = "colorvision://workspace/current";
        private const string LogsResourceUri = "colorvision://logs/recent";
        private const string TemplateResourceUri = "colorvision://template/current";
        private const string FlowResourceUri = "colorvision://flow/current";
        private const string AuditSummaryResourceUri = "colorvision://mcp/audit-summary";
        private const string AuditLogResourceUri = "colorvision://mcp/audit-log";
        private const string CapabilityCatalogResourceUri = "colorvision://copilot/capabilities";
        private const string TaskEventJournalResourceUri = "colorvision://copilot/task-events";
        private static readonly JsonSerializerOptions StructuredJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        private static readonly string[] SupportedPanelAliases =
        {
            "copilot",
            "log",
            "config",
            "solution",
            "template",
            "device",
        };

        private readonly CopilotMcpToolEnvironment _environment;
        private readonly IReadOnlyList<CopilotMcpToolDefinition> _toolDefinitions;
        private readonly IReadOnlyDictionary<string, CopilotMcpToolDefinition> _toolDefinitionsByName;

        private readonly record struct CopilotPanelTarget(string Alias, string TargetId);

        private sealed class TemplatePatchComputation
        {
            public string TemplateIdentifier { get; init; } = string.Empty;

            public string SourceId { get; init; } = string.Empty;

            public string CurrentJson { get; init; } = string.Empty;

            public string ProposedChangesJson { get; init; } = string.Empty;

            public string PatchedJson { get; init; } = string.Empty;

            public IReadOnlyList<string> Changes { get; init; } = Array.Empty<string>();

            public bool IsApplyEligible => !string.IsNullOrWhiteSpace(SourceId);
        }

        public CopilotMcpToolDispatcher(CopilotMcpToolEnvironment? environment = null)
        {
            _environment = environment ?? new CopilotMcpToolEnvironment();
            _toolDefinitions = CreateToolDefinitions();
            ValidateInputSchemas(_toolDefinitions);
            _toolDefinitionsByName = _toolDefinitions.ToDictionary(
                definition => definition.Descriptor.Name,
                StringComparer.OrdinalIgnoreCase);
            CopilotSharedCapabilityCatalog.ValidateMcpSurface(ListTools());
        }

        private static void ValidateInputSchemas(IEnumerable<CopilotMcpToolDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                if (CopilotToolInputContractValidator.TryValidateSchema(
                    definition.Descriptor.InputSchema,
                    out var error))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"MCP tool '{definition.Descriptor.Name}' has an invalid input schema: {error}");
            }
        }

        private static CopilotMcpToolCallResult GetCapabilityCatalog()
        {
            var snapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(snapshot, StructuredJsonOptions));
        }





    }
}
