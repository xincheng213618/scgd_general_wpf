using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotTemplatePatchTool : ICopilotTool, ICopilotApplicationCapabilityClient
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotTemplatePatchTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotTemplatePatchTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => CopilotSharedCapabilityCatalog.PreviewTemplatePatch.AgentToolName;

        public ICopilotApplicationCapabilityInvoker ApplicationCapabilityInvoker => _capabilityInvoker;

        public string Description => CopilotSharedCapabilityCatalog.PreviewTemplatePatch.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.PreviewTemplatePatch.AgentCapability;

        public CopilotToolAccess Access => Capability.Access;

        public CopilotToolRiskLevel RiskLevel => Capability.RiskLevel;

        public CopilotToolApprovalMode ApprovalMode => Capability.ApprovalMode;

        public CopilotToolIdempotency Idempotency => Capability.Idempotency;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.PreviewTemplatePatch.AgentInputSchema;

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode is CopilotAgentMode.Chat or CopilotAgentMode.Diagnose)
                return false;

            var context = CopilotLiveContextRegistry.Current;
            if (context == null || !context.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return false;

            return CopilotTemplatePatchIntentSupport.HasChangeIntent(request.UserText)
                && !CopilotTemplatePatchIntentSupport.HasApplyIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Mode == CopilotAgentMode.Diagnose)
                return Failure("Template patch preview is unavailable in Diagnose mode.", "Start a separate explicit template-edit request before creating a preview.");

            Dictionary<string, JsonElement> arguments;
            try
            {
                if (toolInput.TryGetJsonElementArgument("proposed_changes", out var structuredChanges))
                {
                    var templateIdentifier = toolInput.GetStringArgument("template_identifier");
                    arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["template_identifier"] = JsonSerializer.SerializeToElement(
                            string.IsNullOrWhiteSpace(templateIdentifier)
                                ? "active-template"
                                : templateIdentifier),
                        ["proposed_changes"] = structuredChanges,
                    };
                    var currentJson = toolInput.GetStringArgument("current_json");
                    if (!string.IsNullOrWhiteSpace(currentJson))
                        arguments["current_json"] = JsonSerializer.SerializeToElement(currentJson);
                }
                else
                {
                    var payloadText = ExtractJsonObject(toolInput?.Query);
                    if (string.IsNullOrWhiteSpace(payloadText))
                    {
                        return Failure(
                            "Template patch input is missing.",
                            "The planner must provide template_identifier and proposed_changes.");
                    }

                    using var document = JsonDocument.Parse(payloadText);
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                        return Failure("Template patch input is invalid.", "The template patch input root must be a JSON object.");

                    if (TryGetString(document.RootElement, "preview_id", out var previewId))
                        return Failure("Template patch preview input cannot apply an existing preview.", $"Use ApplyTemplatePatch with preview_id={previewId} after an explicit apply request.");

                    var proposedChanges = document.RootElement.TryGetProperty("proposed_changes", out var proposedElement)
                        ? proposedElement
                        : document.RootElement;
                    if (proposedChanges.ValueKind != JsonValueKind.Object)
                        return Failure("Template patch input is invalid.", "proposed_changes must be a JSON object.");

                    arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["template_identifier"] = JsonSerializer.SerializeToElement("active-template"),
                        ["proposed_changes"] = proposedChanges.Clone(),
                    };
                }
            }
            catch (JsonException ex)
            {
                return Failure("Template patch input is invalid JSON.", ex.Message);
            }

            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                CopilotSharedCapabilityCatalog.PreviewTemplatePatch.McpToolName,
                arguments,
                request,
                frameworkApprovalGranted: false,
                cancellationToken);
            return CopilotApplicationCapabilityInvocation.ToToolResult(
                result,
                Name,
                "Template patch preview created.",
                "Template patch preview failed.");
        }

        private CopilotToolResult Failure(string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = summary,
                ErrorMessage = error,
            };
        }

        private static string ExtractJsonObject(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            var start = value.IndexOf('{');
            var end = value.LastIndexOf('}');
            return start >= 0 && end > start ? value[start..(end + 1)] : string.Empty;
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
                return false;

            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

    }

    internal static class CopilotTemplatePatchIntentSupport
    {
        private static readonly string[] ChangeIntentMarkers =
        {
            "change", "adjust", "modify", "set ", "preview", "apply", "patch", "parameter", "threshold", "exposure",
            "修改", "调整", "改成", "设置", "预览", "应用", "参数", "阈值", "曝光",
        };
        private static readonly string[] ApplyIntentMarkers = { "apply", "confirm", "use this preview", "应用", "确认", "使用这个预览" };

        public static bool HasChangeIntent(string? text) => ChangeIntentMarkers.Any(marker => (text ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase));

        public static bool HasApplyIntent(string? text) => ApplyIntentMarkers.Any(marker => (text ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
