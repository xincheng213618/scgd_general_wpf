using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotCodexCommandHookProcessResult(
        int ExitCode,
        bool TimedOut,
        string StandardOutput,
        string StandardError);

    internal interface ICopilotCodexCommandHookRunner
    {
        Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken);
    }

    internal sealed class CopilotCodexCommandHookRunner : ICopilotCodexCommandHookRunner
    {
        private readonly ICopilotShellProcessRunner _processRunner;

        public CopilotCodexCommandHookRunner()
            : this(new CopilotShellProcessRunner())
        {
        }

        internal CopilotCodexCommandHookRunner(ICopilotShellProcessRunner processRunner)
        {
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public async Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(request);
            var executablePath = CopilotShellCommandService.FindTrustedShellExecutable(
                CopilotShellKind.CommandPrompt);
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new FileNotFoundException("A trusted Windows command interpreter could not be located.");

            var workingDirectory = ResolveWorkingDirectory(request);
            var result = await _processRunner.RunAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.CommandPrompt,
                    Path.GetFullPath(executablePath),
                    CopilotShellCommandService.BuildArguments(
                        CopilotShellKind.CommandPrompt,
                        definition.Command),
                    workingDirectory,
                    TimeSpan.FromSeconds(definition.TimeoutSeconds))
                {
                    EnvironmentVariables = request.CodexShellEnvironmentPolicy
                        .CreateEnvironmentVariables(request.ConversationId),
                    StandardInput = standardInput,
                },
                cancellationToken).ConfigureAwait(false);
            return new CopilotCodexCommandHookProcessResult(
                result.ExitCode,
                result.TimedOut,
                result.StandardOutput,
                result.StandardError);
        }

        internal static string ResolveWorkingDirectory(CopilotAgentRequest request)
        {
            var candidates = new[] { request.WorkspacePath }
                .Concat(request.WritableLocalRootPaths ?? Array.Empty<string>())
                .Concat(request.SearchRootPaths ?? Array.Empty<string>())
                .Concat(request.TrustedProjectRootPaths ?? Array.Empty<string>());
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                try
                {
                    var fullPath = Path.GetFullPath(candidate.Trim());
                    if (Directory.Exists(fullPath)
                        && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                }
            }

            throw new DirectoryNotFoundException(
                "The submitted Copilot request no longer has a safe hook working directory.");
        }
    }

    internal sealed class CopilotCodexCommandHook :
        ICopilotToolPermissionRequestHook,
        ICopilotToolPostExecutionOutputHook
    {
        private readonly CopilotCodexCommandHookDefinition _definition;
        private readonly ICopilotCodexCommandHookRunner _runner;

        public CopilotCodexCommandHook(
            CopilotCodexCommandHookDefinition definition,
            ICopilotCodexCommandHookRunner? runner = null)
        {
            _definition = definition?.CreateSnapshot()
                ?? throw new ArgumentNullException(nameof(definition));
            if (!_definition.IsStructurallyValid())
                throw new ArgumentException("The configured Codex command hook is invalid.", nameof(definition));
            _runner = runner ?? new CopilotCodexCommandHookRunner();
        }

        public async Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.PermissionRequest)
                return CopilotToolPermissionRequestDecision.Prompt;

            var result = await RunAsync(
                context.Invocation,
                outcome: null,
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "PermissionRequest");
            if (failure != null)
                return CopilotToolPermissionRequestDecision.Deny(failure, "configured_hook_failed");
            if (result.ExitCode == 2)
            {
                return CopilotToolPermissionRequestDecision.Deny(
                    NormalizeReason(result.StandardError, "A configured PermissionRequest hook denied this tool call."),
                    "configured_hook_denied");
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                return invalidJson
                    ? CopilotToolPermissionRequestDecision.Deny(
                        "A configured PermissionRequest hook returned invalid JSON.",
                        "configured_hook_invalid_output")
                    : CopilotToolPermissionRequestDecision.Prompt;
            }
            using (root)
            {
                if (TryReadStopDecision(root.RootElement, out var stopReason))
                {
                    return CopilotToolPermissionRequestDecision.Deny(
                        stopReason,
                        "configured_hook_denied");
                }
                if (!TryReadHookSpecificOutput(
                    root.RootElement,
                    "PermissionRequest",
                    out var specific,
                    out var specificError))
                {
                    return specificError.Length == 0
                        ? CopilotToolPermissionRequestDecision.Prompt
                        : CopilotToolPermissionRequestDecision.Deny(
                            specificError,
                            "configured_hook_invalid_output");
                }
                if (!specific.TryGetProperty("decision", out var decision)
                    || decision.ValueKind == JsonValueKind.Null)
                {
                    return CopilotToolPermissionRequestDecision.Prompt;
                }
                if (decision.ValueKind != JsonValueKind.Object
                    || !decision.TryGetProperty("behavior", out var behavior)
                    || behavior.ValueKind != JsonValueKind.String)
                {
                    return CopilotToolPermissionRequestDecision.Deny(
                        "A configured PermissionRequest hook returned an invalid decision.",
                        "configured_hook_invalid_output");
                }

                return behavior.GetString() switch
                {
                    "deny" => CopilotToolPermissionRequestDecision.Deny(
                        NormalizeReason(
                            ReadOptionalString(decision, "message"),
                            "A configured PermissionRequest hook denied this tool call."),
                        "configured_hook_denied"),
                    // Configured hooks cannot bypass ColorVision's native approval binding.
                    "allow" => CopilotToolPermissionRequestDecision.Prompt,
                    _ => CopilotToolPermissionRequestDecision.Deny(
                        "A configured PermissionRequest hook returned an unsupported behavior.",
                        "configured_hook_invalid_output"),
                };
            }
        }

        public async Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.PreToolUse)
                return CopilotToolExecutionHookDecision.Proceed;

            var result = await RunAsync(
                context.Invocation,
                outcome: null,
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "PreToolUse");
            if (failure != null)
            {
                return CopilotToolExecutionHookDecision.Deny(
                    failure,
                    "configured_hook_failed",
                    CopilotToolFailureKind.Internal);
            }
            if (result.ExitCode == 2)
            {
                return CopilotToolExecutionHookDecision.Deny(
                    NormalizeReason(result.StandardError, "A configured PreToolUse hook denied this tool call."),
                    "configured_hook_denied");
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                return invalidJson
                    ? CopilotToolExecutionHookDecision.Deny(
                        "A configured PreToolUse hook returned invalid JSON.",
                        "configured_hook_invalid_output",
                        CopilotToolFailureKind.Internal)
                    : CopilotToolExecutionHookDecision.Proceed;
            }
            using (root)
            {
                if (TryReadStopDecision(root.RootElement, out var stopReason))
                {
                    return CopilotToolExecutionHookDecision.Deny(
                        stopReason,
                        "configured_hook_denied");
                }
                if (!TryReadHookSpecificOutput(
                    root.RootElement,
                    "PreToolUse",
                    out var specific,
                    out var specificError))
                {
                    return specificError.Length == 0
                        ? CopilotToolExecutionHookDecision.Proceed
                        : CopilotToolExecutionHookDecision.Deny(
                            specificError,
                            "configured_hook_invalid_output",
                            CopilotToolFailureKind.Internal);
                }
                if (specific.TryGetProperty("updatedInput", out var updatedInput)
                    && updatedInput.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    return CopilotToolExecutionHookDecision.Deny(
                        "A configured PreToolUse hook requested an input rewrite that ColorVision cannot bind to the existing approval snapshot.",
                        "configured_hook_input_rewrite_unsupported",
                        CopilotToolFailureKind.Authorization);
                }
                if (!specific.TryGetProperty("permissionDecision", out var permissionDecision)
                    || permissionDecision.ValueKind == JsonValueKind.Null)
                {
                    return CopilotToolExecutionHookDecision.Proceed;
                }
                if (permissionDecision.ValueKind != JsonValueKind.String)
                {
                    return CopilotToolExecutionHookDecision.Deny(
                        "A configured PreToolUse hook returned an invalid permissionDecision.",
                        "configured_hook_invalid_output",
                        CopilotToolFailureKind.Internal);
                }

                return permissionDecision.GetString() switch
                {
                    "deny" => CopilotToolExecutionHookDecision.Deny(
                        NormalizeReason(
                            ReadOptionalString(specific, "permissionDecisionReason"),
                            "A configured PreToolUse hook denied this tool call."),
                        "configured_hook_denied"),
                    "allow" => CopilotToolExecutionHookDecision.Proceed,
                    "ask" => CopilotToolExecutionHookDecision.Deny(
                        NormalizeReason(
                            ReadOptionalString(specific, "permissionDecisionReason"),
                            "A configured PreToolUse hook requested approval that is not bound to this tool invocation."),
                        "configured_hook_approval_required",
                        CopilotToolFailureKind.Authorization),
                    _ => CopilotToolExecutionHookDecision.Deny(
                        "A configured PreToolUse hook returned an unsupported permissionDecision.",
                        "configured_hook_invalid_output",
                        CopilotToolFailureKind.Internal),
                };
            }
        }

        public async Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            _ = await AfterExecuteWithOutputAsync(outcome, cancellationToken).ConfigureAwait(false);
        }

        public async Task<CopilotToolPostExecutionOutput?> AfterExecuteWithOutputAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.PostToolUse)
                return null;

            var result = await RunAsync(
                outcome.Invocation,
                outcome,
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "PostToolUse");
            if (failure != null)
                throw new InvalidOperationException(failure);
            if (result.ExitCode == 2)
            {
                var feedback = CopilotApprovalRequestReason.Normalize(result.StandardError);
                if (_definition.ExecutionMode == CopilotToolExecutionHookMode.Async)
                {
                    throw new InvalidOperationException(
                        "An asynchronous configured PostToolUse hook exited with code 2.");
                }
                return feedback.Length == 0
                    ? new CopilotToolPostExecutionOutput(
                        FailureMessage: "A configured PostToolUse hook exited with code 2 without feedback.")
                    : new CopilotToolPostExecutionOutput(
                        FeedbackMessage: feedback,
                        Control: CopilotToolPostExecutionControl.Blocked);
            }
            JsonDocument? root = null;
            if (LooksLikeJson(result.StandardOutput)
                && !TryParseJsonOutput(result.StandardOutput, out root, out _))
            {
                throw new InvalidOperationException(
                    "A configured PostToolUse hook returned invalid JSON.");
            }
            using (root)
            {
                if (root == null)
                    return null;

                var output = ReadPostToolUseOutput(root.RootElement);
                return output.HasOutput ? output : null;
            }
        }

        private CopilotToolPostExecutionOutput ReadPostToolUseOutput(JsonElement root)
        {
            if (!HasOnlyPostToolUseProperties(root)
                || !TryReadOptionalString(root, "systemMessage", out var systemMessage)
                || !TryReadOptionalString(root, "stopReason", out var stopReason)
                || !TryReadOptionalString(root, "decision", out var decision)
                || !TryReadOptionalBoolean(root, "continue", defaultValue: true, out var shouldContinue)
                || !TryReadOptionalBoolean(root, "suppressOutput", defaultValue: false, out var suppressOutput))
            {
                return new CopilotToolPostExecutionOutput(
                    FailureMessage: "A configured PostToolUse hook returned an invalid universal output field.");
            }

            var additionalContext = string.Empty;
            var hasSpecificOutput = TryReadHookSpecificOutput(
                root,
                "PostToolUse",
                out var specific,
                out var specificError);
            if (!hasSpecificOutput && specificError.Length > 0)
            {
                return new CopilotToolPostExecutionOutput(
                    SystemMessage: systemMessage,
                    FailureMessage: specificError);
            }
            if (hasSpecificOutput)
            {
                if (!HasOnlyPostToolUseSpecificProperties(specific))
                {
                    return CreateInvalidPostToolOutput(
                        systemMessage,
                        "A configured PostToolUse hook returned an unknown hook-specific output field.");
                }
                if (specific.TryGetProperty("updatedMCPToolOutput", out var updatedOutput)
                    && updatedOutput.ValueKind != JsonValueKind.Null)
                {
                    return CreateInvalidPostToolOutput(
                        systemMessage,
                        "A configured PostToolUse hook returned unsupported updatedMCPToolOutput.");
                }
                if (!TryReadOptionalString(specific, "additionalContext", out additionalContext))
                {
                    return CreateInvalidPostToolOutput(
                        systemMessage,
                        "A configured PostToolUse hook returned invalid additionalContext.");
                }
            }

            if (_definition.ExecutionMode == CopilotToolExecutionHookMode.Async)
            {
                return new CopilotToolPostExecutionOutput(
                    SystemMessage: systemMessage,
                    AdditionalContext: additionalContext);
            }

            var hasBlockDecision = string.Equals(decision, "block", StringComparison.Ordinal);
            var reasonWasProvided = root.TryGetProperty("reason", out var reasonElement)
                && reasonElement.ValueKind != JsonValueKind.Null;
            if (!TryReadOptionalString(root, "reason", out var reason)
                || decision.Length > 0 && !hasBlockDecision)
            {
                return CreateInvalidPostToolOutput(
                    systemMessage,
                    "A configured PostToolUse hook returned an invalid decision or reason.");
            }

            var invalidReason = suppressOutput
                ? "A configured PostToolUse hook returned unsupported suppressOutput."
                : hasBlockDecision && reason.Length == 0
                    ? "A configured PostToolUse hook returned decision:block without a non-empty reason."
                    : !hasBlockDecision && shouldContinue && reasonWasProvided
                        ? "A configured PostToolUse hook returned reason without decision."
                        : string.Empty;
            var usableAdditionalContext = invalidReason.Length == 0
                ? additionalContext
                : string.Empty;
            if (!shouldContinue)
            {
                var feedback = reason.Length > 0
                    ? reason
                    : NormalizeReason(
                        stopReason,
                        "PostToolUse hook stopped execution");
                return new CopilotToolPostExecutionOutput(
                    FeedbackMessage: feedback,
                    SystemMessage: systemMessage,
                    AdditionalContext: usableAdditionalContext,
                    Control: CopilotToolPostExecutionControl.Stopped);
            }
            if (invalidReason.Length > 0)
                return CreateInvalidPostToolOutput(systemMessage, invalidReason);
            if (hasBlockDecision)
            {
                return new CopilotToolPostExecutionOutput(
                    FeedbackMessage: reason,
                    SystemMessage: systemMessage,
                    AdditionalContext: additionalContext,
                    Control: CopilotToolPostExecutionControl.Blocked);
            }
            return new CopilotToolPostExecutionOutput(
                SystemMessage: systemMessage,
                AdditionalContext: additionalContext);
        }

        private static CopilotToolPostExecutionOutput CreateInvalidPostToolOutput(
            string systemMessage,
            string failureMessage) => new(
                SystemMessage: systemMessage,
                FailureMessage: failureMessage);

        private static bool HasOnlyPostToolUseProperties(JsonElement root)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not (
                    "continue"
                    or "stopReason"
                    or "suppressOutput"
                    or "systemMessage"
                    or "decision"
                    or "reason"
                    or "hookSpecificOutput"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlyPostToolUseSpecificProperties(JsonElement specific)
        {
            foreach (var property in specific.EnumerateObject())
            {
                if (property.Name is not (
                    "hookEventName"
                    or "additionalContext"
                    or "updatedMCPToolOutput"))
                {
                    return false;
                }
            }
            return true;
        }

        private async Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotToolInvocation invocation,
            CopilotToolExecutionOutcome? outcome,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _runner.RunAsync(
                    _definition,
                    invocation.AgentRequest,
                    BuildInputJson(invocation, outcome),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "The configured Codex command hook could not be executed: "
                    + CopilotUserFacingErrorFormatter.Sanitize(ex.Message),
                    ex);
            }
        }

        private string BuildInputJson(
            CopilotToolInvocation invocation,
            CopilotToolExecutionOutcome? outcome)
        {
            var request = invocation.AgentRequest;
            var eventName = _definition.Event.ToString();
            var input = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_id"] = request.ConversationId,
                ["turn_id"] = request.TaskId,
                ["transcript_path"] = null,
                ["cwd"] = CopilotCodexCommandHookRunner.ResolveWorkingDirectory(request),
                ["hook_event_name"] = eventName,
                ["model"] = request.Profile?.Model ?? string.Empty,
                ["permission_mode"] = ResolvePermissionMode(request),
                ["tool_name"] = CopilotCodexConfiguredHookToolNames.GetCanonicalName(
                    invocation.Tool.Name),
                ["tool_input"] = CreateToolInput(invocation.ToolInput),
            };
            if (_definition.Event != CopilotCodexConfiguredHookEvent.PermissionRequest)
                input["tool_use_id"] = invocation.CallId;
            if (_definition.Event == CopilotCodexConfiguredHookEvent.PostToolUse)
            {
                input["tool_response"] = new
                {
                    success = outcome?.Result.Success == true,
                    summary = outcome?.Result.Summary ?? string.Empty,
                    content = outcome?.Result.Content ?? string.Empty,
                    error = outcome?.Result.ErrorMessage ?? string.Empty,
                    failure_code = outcome?.Result.FailureCode ?? string.Empty,
                };
            }
            return JsonSerializer.Serialize(input) + Environment.NewLine;
        }

        private static IReadOnlyDictionary<string, object?> CreateToolInput(
            CopilotAgentToolInput toolInput)
        {
            toolInput ??= CopilotAgentToolInput.Empty;
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var argument in toolInput.Arguments)
                result[argument.Key] = argument.Value;
            AddIfMissing(result, "query", toolInput.Query);
            AddIfMissing(result, "path", toolInput.Path);
            AddIfMissing(result, "cursor", toolInput.Cursor);
            AddIfMissing(result, "startLine", toolInput.StartLine);
            AddIfMissing(result, "startColumn", toolInput.StartColumn);
            AddIfMissing(result, "endLine", toolInput.EndLine);
            return result;
        }

        private static void AddIfMissing(
            Dictionary<string, object?> values,
            string name,
            object? value)
        {
            if (value == null
                || value is string text && string.IsNullOrWhiteSpace(text)
                || values.ContainsKey(name))
            {
                return;
            }
            values[name] = value;
        }

        private static string ResolvePermissionMode(CopilotAgentRequest request)
        {
            if (request.Mode == CopilotAgentMode.Plan)
                return "plan";
            return request.CodexApprovalPolicy.Mode switch
            {
                CopilotCodexApprovalPolicyMode.Never => "dontAsk",
                _ => "default",
            };
        }

        private static string? GetProcessFailure(
            CopilotCodexCommandHookProcessResult result,
            string eventName)
        {
            if (result.TimedOut)
                return $"A configured {eventName} hook exceeded its timeout.";
            if (result.ExitCode is 0 or 2)
                return null;
            return $"A configured {eventName} hook exited with code {result.ExitCode}.";
        }

        private static bool TryParseJsonOutput(
            string? output,
            out JsonDocument? document,
            out bool invalidJson)
        {
            document = null;
            var normalized = output?.Trim() ?? string.Empty;
            invalidJson = false;
            if (normalized.Length == 0)
                return false;
            if (!LooksLikeJson(normalized))
                return false;
            try
            {
                document = JsonDocument.Parse(normalized, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    return true;
                document.Dispose();
                document = null;
                invalidJson = true;
                return false;
            }
            catch (JsonException)
            {
                invalidJson = true;
                return false;
            }
        }

        private static bool LooksLikeJson(string? value)
        {
            var normalized = value?.TrimStart() ?? string.Empty;
            return normalized.StartsWith('{') || normalized.StartsWith('[');
        }

        private static bool TryReadHookSpecificOutput(
            JsonElement root,
            string expectedEventName,
            out JsonElement specific,
            out string error)
        {
            specific = default;
            error = string.Empty;
            if (!root.TryGetProperty("hookSpecificOutput", out specific)
                || specific.ValueKind == JsonValueKind.Null)
            {
                return false;
            }
            if (specific.ValueKind != JsonValueKind.Object
                || !string.Equals(
                    ReadOptionalString(specific, "hookEventName"),
                    expectedEventName,
                    StringComparison.Ordinal))
            {
                error = $"A configured {expectedEventName} hook returned output for a different event.";
                return false;
            }
            return true;
        }

        private static bool TryReadStopDecision(JsonElement root, out string reason)
        {
            reason = string.Empty;
            if (root.TryGetProperty("continue", out var shouldContinue)
                && shouldContinue.ValueKind == JsonValueKind.False)
            {
                reason = NormalizeReason(
                    ReadOptionalString(root, "stopReason"),
                    "A configured hook stopped this tool call.");
                return true;
            }
            if (string.Equals(ReadOptionalString(root, "decision"), "block", StringComparison.Ordinal))
            {
                reason = NormalizeReason(
                    ReadOptionalString(root, "reason"),
                    "A configured hook blocked this tool call.");
                return true;
            }
            return false;
        }

        private static string ReadOptionalString(JsonElement value, string propertyName)
        {
            return value.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString()?.Trim() ?? string.Empty
                    : string.Empty;
        }

        private static bool TryReadOptionalString(
            JsonElement value,
            string propertyName,
            out string result)
        {
            result = string.Empty;
            if (!value.TryGetProperty(propertyName, out var property)
                || property.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (property.ValueKind != JsonValueKind.String)
                return false;
            result = property.GetString()?.Trim() ?? string.Empty;
            return true;
        }

        private static bool TryReadOptionalBoolean(
            JsonElement value,
            string propertyName,
            bool defaultValue,
            out bool result)
        {
            result = defaultValue;
            if (!value.TryGetProperty(propertyName, out var property))
                return true;
            if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return false;
            result = property.ValueKind == JsonValueKind.True;
            return true;
        }

        private static string NormalizeReason(string? value, string fallback)
        {
            var normalized = CopilotApprovalRequestReason.Normalize(value);
            return normalized.Length == 0 ? fallback : normalized;
        }
    }

    internal static class CopilotCodexCommandHookFactory
    {
        public static IReadOnlyList<CopilotToolExecutionHookBinding> Resolve(
            IEnumerable<CopilotCodexCommandHookDefinition>? definitions,
            string toolName,
            ICopilotCodexCommandHookRunner? runner = null)
        {
            return (definitions ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.Matches(toolName) == true)
                .OrderBy(definition => definition.Order)
                .Select(definition => new CopilotToolExecutionHookBinding(
                    definition.SourceId,
                    new CopilotCodexCommandHook(definition, runner),
                    definition.ExecutionMode,
                    definition.Phases,
                    TimeSpan.FromSeconds(definition.TimeoutSeconds)))
                .ToArray();
        }

        public static IReadOnlyList<CopilotToolExecutionHookRegistryEntry> CreateSnapshotEntries(
            IEnumerable<CopilotCodexCommandHookDefinition>? definitions)
        {
            return (definitions ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true)
                .OrderBy(definition => definition.Order)
                .Select(definition => CopilotToolExecutionHookRegistry.CreateSnapshotEntry(
                    definition.SourceId,
                    definition.ToolNamePattern,
                    definition.Order,
                    new CopilotCodexCommandHook(definition),
                    definition.ConfigurationFingerprint,
                    definition.ExecutionMode))
                .ToArray();
        }
    }
}
