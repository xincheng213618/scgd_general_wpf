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

    internal sealed record CopilotCodexUserPromptSubmitOutput(
        bool ShouldStop = false,
        string StopReason = "",
        string SystemMessage = "",
        string AdditionalContext = "",
        int AdditionalContextLimitTokens = CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens,
        string FailureCode = "")
    {
        public bool HasFailure => !string.IsNullOrWhiteSpace(FailureCode);
    }

    internal sealed record CopilotCodexStopOutput(
        bool ShouldStop = false,
        string StopReason = "",
        bool ShouldContinue = false,
        string ContinuationReason = "",
        string SystemMessage = "",
        string FailureCode = "")
    {
        public bool HasFailure => !string.IsNullOrWhiteSpace(FailureCode);
    }

    internal sealed record CopilotCodexCompactOutput(
        bool ShouldStop = false,
        string StopReason = "",
        string SystemMessage = "",
        string FailureCode = "")
    {
        public bool HasFailure => !string.IsNullOrWhiteSpace(FailureCode);
    }

    internal sealed record CopilotCodexSubagentStartOutput(
        string SystemMessage = "",
        string AdditionalContext = "",
        int AdditionalContextLimitTokens = CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens,
        string FailureMessage = "",
        string FailureCode = "")
    {
        public bool HasFailure => !string.IsNullOrWhiteSpace(FailureCode);
    }

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
        ICopilotToolPermissionRequestOutputHook,
        ICopilotToolPreExecutionOutputHook,
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
            var output = await OnPermissionRequestWithOutputAsync(
                context,
                cancellationToken).ConfigureAwait(false);
            return output?.Decision ?? CopilotToolPermissionRequestDecision.Prompt;
        }

        public async Task<CopilotToolPermissionRequestOutput?> OnPermissionRequestWithOutputAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.PermissionRequest)
                return null;

            var result = await RunAsync(
                context.Invocation,
                outcome: null,
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "PermissionRequest");
            if (failure != null)
            {
                return CreatePermissionRequestOutput(
                    CopilotToolPermissionRequestDecision.Deny(
                        failure,
                        "configured_hook_failed"));
            }
            if (result.ExitCode == 2)
            {
                return CreatePermissionRequestOutput(
                    CopilotToolPermissionRequestDecision.Deny(
                        NormalizeReason(result.StandardError, "A configured PermissionRequest hook denied this tool call."),
                        "configured_hook_denied"));
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                return invalidJson
                    ? CreatePermissionRequestOutput(
                        CopilotToolPermissionRequestDecision.Deny(
                            "A configured PermissionRequest hook returned invalid JSON.",
                            "configured_hook_invalid_output"))
                    : null;
            }
            if (root == null)
            {
                return CreatePermissionRequestOutput(
                    CopilotToolPermissionRequestDecision.Deny(
                        "A configured PermissionRequest hook did not return a usable JSON document.",
                        "configured_hook_invalid_output"));
            }
            using (root)
            {
                if (!TryReadOptionalString(
                    root.RootElement,
                    "systemMessage",
                    out var systemMessage))
                {
                    return CreateInvalidPermissionRequestOutput(
                        string.Empty,
                        "A configured PermissionRequest hook returned an invalid systemMessage.");
                }
                if (!HasOnlyPermissionRequestProperties(root.RootElement)
                    || !TryReadOptionalString(
                        root.RootElement,
                        "stopReason",
                        out _)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "continue",
                        defaultValue: true,
                        out var shouldContinue)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "suppressOutput",
                        defaultValue: false,
                        out var suppressOutput))
                {
                    return CreateInvalidPermissionRequestOutput(
                        systemMessage,
                        "A configured PermissionRequest hook returned an invalid universal output field.");
                }
                if (!shouldContinue
                    || HasNonNullProperty(root.RootElement, "stopReason")
                    || suppressOutput)
                {
                    var unsupportedField = !shouldContinue
                        ? "continue:false"
                        : HasNonNullProperty(root.RootElement, "stopReason")
                            ? "stopReason"
                            : "suppressOutput";
                    return CreateInvalidPermissionRequestOutput(
                        systemMessage,
                        $"A configured PermissionRequest hook returned unsupported {unsupportedField}.");
                }
                if (!TryReadHookSpecificOutput(
                    root.RootElement,
                    "PermissionRequest",
                    out var specific,
                    out var specificError))
                {
                    return specificError.Length == 0
                        ? CreatePermissionRequestOutput(
                            CopilotToolPermissionRequestDecision.Prompt,
                            systemMessage)
                        : CreateInvalidPermissionRequestOutput(
                            systemMessage,
                            specificError);
                }
                if (!HasOnlyPermissionRequestSpecificProperties(specific))
                {
                    return CreateInvalidPermissionRequestOutput(
                        systemMessage,
                        "A configured PermissionRequest hook returned an invalid hook-specific output field.");
                }
                if (!specific.TryGetProperty("decision", out var decision)
                    || decision.ValueKind == JsonValueKind.Null)
                {
                    return CreatePermissionRequestOutput(
                        CopilotToolPermissionRequestDecision.Prompt,
                        systemMessage);
                }
                if (decision.ValueKind != JsonValueKind.Object
                    || !HasOnlyPermissionRequestDecisionProperties(decision)
                    || !decision.TryGetProperty("behavior", out var behavior)
                    || behavior.ValueKind != JsonValueKind.String
                    || !TryReadOptionalString(decision, "message", out var message)
                    || !TryReadOptionalBoolean(
                        decision,
                        "interrupt",
                        defaultValue: false,
                        out var interrupt))
                {
                    return CreateInvalidPermissionRequestOutput(
                        systemMessage,
                        "A configured PermissionRequest hook returned an invalid decision.");
                }
                if (HasNonNullProperty(decision, "updatedInput")
                    || HasNonNullProperty(decision, "updatedPermissions")
                    || interrupt)
                {
                    var unsupportedField = HasNonNullProperty(decision, "updatedInput")
                        ? "updatedInput"
                        : HasNonNullProperty(decision, "updatedPermissions")
                            ? "updatedPermissions"
                            : "interrupt:true";
                    return CreateInvalidPermissionRequestOutput(
                        systemMessage,
                        $"A configured PermissionRequest hook returned unsupported {unsupportedField}.");
                }

                var permissionDecision = behavior.GetString() switch
                {
                    "deny" => CopilotToolPermissionRequestDecision.Deny(
                        NormalizeReason(
                            message,
                            "A configured PermissionRequest hook denied this tool call."),
                        "configured_hook_denied"),
                    // Configured hooks cannot bypass ColorVision's native approval binding.
                    "allow" => CopilotToolPermissionRequestDecision.Prompt,
                    _ => CopilotToolPermissionRequestDecision.Deny(
                        "A configured PermissionRequest hook returned an unsupported behavior.",
                        "configured_hook_invalid_output"),
                };
                return CreatePermissionRequestOutput(
                    permissionDecision,
                    systemMessage);
            }
        }

        private static CopilotToolPermissionRequestOutput CreateInvalidPermissionRequestOutput(
            string systemMessage,
            string failureMessage) => CreatePermissionRequestOutput(
                CopilotToolPermissionRequestDecision.Deny(
                    failureMessage,
                    "configured_hook_invalid_output"),
                systemMessage);

        private static CopilotToolPermissionRequestOutput CreatePermissionRequestOutput(
            CopilotToolPermissionRequestDecision decision,
            string systemMessage = "") => new(decision, systemMessage);

        internal async Task<CopilotCodexUserPromptSubmitOutput?> OnUserPromptSubmitAsync(
            CopilotAgentRequest request,
            string prompt,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.UserPromptSubmit)
                return null;

            var result = await RunAsync(
                request,
                BuildUserPromptSubmitInputJson(request, prompt),
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "UserPromptSubmit");
            if (failure != null)
                return CreateInvalidUserPromptSubmitOutput(string.Empty, failure, "configured_hook_failed");
            if (result.ExitCode == 2)
            {
                return new CopilotCodexUserPromptSubmitOutput(
                    ShouldStop: true,
                    StopReason: NormalizeReason(
                        result.StandardError,
                        "A configured UserPromptSubmit hook blocked this prompt."));
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                if (invalidJson)
                {
                    return CreateInvalidUserPromptSubmitOutput(
                        string.Empty,
                        "A configured UserPromptSubmit hook returned invalid JSON.");
                }
                return new CopilotCodexUserPromptSubmitOutput(
                    AdditionalContext: result.StandardOutput?.Trim() ?? string.Empty,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
            }
            if (root == null)
            {
                return CreateInvalidUserPromptSubmitOutput(
                    string.Empty,
                    "A configured UserPromptSubmit hook did not return a usable JSON document.");
            }
            using (root)
            {
                if (!TryReadOptionalString(root.RootElement, "systemMessage", out var systemMessage))
                {
                    return CreateInvalidUserPromptSubmitOutput(
                        string.Empty,
                        "A configured UserPromptSubmit hook returned an invalid systemMessage.");
                }
                if (!HasOnlyUserPromptSubmitProperties(root.RootElement)
                    || !TryReadOptionalString(root.RootElement, "stopReason", out var stopReason)
                    || !TryReadOptionalString(root.RootElement, "reason", out var reason)
                    || !TryReadOptionalString(root.RootElement, "decision", out var decision)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "continue",
                        defaultValue: true,
                        out var shouldContinue)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "suppressOutput",
                        defaultValue: false,
                        out _))
                {
                    return CreateInvalidUserPromptSubmitOutput(
                        systemMessage,
                        "A configured UserPromptSubmit hook returned an invalid universal output field.");
                }

                if (decision.Length > 0 && !string.Equals(decision, "block", StringComparison.Ordinal))
                {
                    return CreateInvalidUserPromptSubmitOutput(
                        systemMessage,
                        "A configured UserPromptSubmit hook returned an unsupported decision.");
                }

                var additionalContext = string.Empty;
                if (!TryReadHookSpecificOutput(
                    root.RootElement,
                    "UserPromptSubmit",
                    out var specific,
                    out var specificError))
                {
                    if (specificError.Length > 0)
                    {
                        return CreateInvalidUserPromptSubmitOutput(
                            systemMessage,
                            specificError);
                    }
                }
                else if (!HasOnlyUserPromptSubmitSpecificProperties(specific)
                    || !TryReadOptionalString(specific, "additionalContext", out additionalContext))
                {
                    return CreateInvalidUserPromptSubmitOutput(
                        systemMessage,
                        "A configured UserPromptSubmit hook returned an invalid hook-specific output field.");
                }

                if (!shouldContinue)
                {
                    return new CopilotCodexUserPromptSubmitOutput(
                        ShouldStop: true,
                        StopReason: NormalizeReason(
                            stopReason,
                            "A configured UserPromptSubmit hook stopped this prompt."),
                        SystemMessage: systemMessage,
                        AdditionalContext: additionalContext,
                        AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
                }
                if (string.Equals(decision, "block", StringComparison.Ordinal))
                {
                    if (reason.Length == 0)
                    {
                        return CreateInvalidUserPromptSubmitOutput(
                            systemMessage,
                            "A configured UserPromptSubmit hook returned decision:block without a non-empty reason.");
                    }
                    return new CopilotCodexUserPromptSubmitOutput(
                        ShouldStop: true,
                        StopReason: reason,
                        SystemMessage: systemMessage,
                        AdditionalContext: additionalContext,
                        AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
                }

                return new CopilotCodexUserPromptSubmitOutput(
                    SystemMessage: systemMessage,
                    AdditionalContext: additionalContext,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
            }
        }

        private static CopilotCodexUserPromptSubmitOutput CreateInvalidUserPromptSubmitOutput(
            string systemMessage,
            string failureMessage,
            string failureCode = "configured_hook_invalid_output") => new(
                ShouldStop: true,
                StopReason: failureMessage,
                SystemMessage: systemMessage,
                FailureCode: failureCode);

        internal async Task<CopilotCodexStopOutput?> OnStopAsync(
            CopilotAgentRequest request,
            bool stopHookActive,
            string? lastAssistantMessage,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.Stop)
                return null;

            return await OnStopCoreAsync(
                request,
                "Stop",
                BuildStopInputJson(request, stopHookActive, lastAssistantMessage),
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<CopilotCodexStopOutput?> OnSubagentStopAsync(
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext subagent,
            bool stopHookActive,
            string? lastAssistantMessage,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(subagent);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.SubagentStop)
                return null;

            return await OnStopCoreAsync(
                request,
                "SubagentStop",
                BuildSubagentStopInputJson(
                    request,
                    subagent,
                    stopHookActive,
                    lastAssistantMessage),
                cancellationToken).ConfigureAwait(false);
        }

        internal Task<CopilotCodexStopOutput?> RunStopEventAsync(
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext? subagent,
            bool stopHookActive,
            string? lastAssistantMessage,
            CancellationToken cancellationToken) => subagent == null
                ? OnStopAsync(
                    request,
                    stopHookActive,
                    lastAssistantMessage,
                    cancellationToken)
                : OnSubagentStopAsync(
                    request,
                    subagent,
                    stopHookActive,
                    lastAssistantMessage,
                    cancellationToken);

        private async Task<CopilotCodexStopOutput> OnStopCoreAsync(
            CopilotAgentRequest request,
            string eventName,
            string inputJson,
            CancellationToken cancellationToken)
        {
            var result = await RunAsync(
                request,
                inputJson,
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, eventName);
            if (failure != null)
                return CreateInvalidStopOutput(string.Empty, failure, "configured_hook_failed");
            if (result.ExitCode == 2)
            {
                var continuationReason = CopilotApprovalRequestReason.Normalize(result.StandardError);
                if (_definition.ExecutionMode == CopilotToolExecutionHookMode.Async)
                {
                    return CreateInvalidStopOutput(
                        string.Empty,
                        $"An asynchronous configured {eventName} hook exited with code 2.",
                        "configured_hook_failed");
                }
                return continuationReason.Length == 0
                    ? CreateInvalidStopOutput(
                        string.Empty,
                        $"A configured {eventName} hook exited with code 2 without a continuation prompt.")
                    : new CopilotCodexStopOutput(
                        ShouldContinue: _definition.ExecutionMode == CopilotToolExecutionHookMode.Sync,
                        ContinuationReason: continuationReason);
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                return string.IsNullOrWhiteSpace(result.StandardOutput)
                    ? new CopilotCodexStopOutput()
                    : _definition.ExecutionMode == CopilotToolExecutionHookMode.Async
                        && !invalidJson
                        ? new CopilotCodexStopOutput()
                        : CreateInvalidStopOutput(
                            string.Empty,
                            $"A configured {eventName} hook returned invalid JSON.");
            }
            if (root == null)
            {
                return CreateInvalidStopOutput(
                    string.Empty,
                    $"A configured {eventName} hook did not return a usable JSON document.");
            }

            using (root)
            {
                if (!HasOnlyStopProperties(root.RootElement)
                    || !TryReadOptionalString(root.RootElement, "systemMessage", out var systemMessage)
                    || !TryReadOptionalString(root.RootElement, "stopReason", out var stopReason)
                    || !TryReadOptionalString(root.RootElement, "decision", out var decision)
                    || !TryReadOptionalString(root.RootElement, "reason", out var reason)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "continue",
                        defaultValue: true,
                        out var shouldContinue)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "suppressOutput",
                        defaultValue: false,
                        out _))
                {
                    return CreateInvalidStopOutput(
                        string.Empty,
                        $"A configured {eventName} hook returned an invalid output field.");
                }

                if (_definition.ExecutionMode == CopilotToolExecutionHookMode.Async)
                    return new CopilotCodexStopOutput(SystemMessage: systemMessage);
                if (decision.Length > 0
                    && !string.Equals(decision, "block", StringComparison.Ordinal))
                {
                    return CreateInvalidStopOutput(
                        systemMessage,
                        $"A configured {eventName} hook returned an unsupported decision.");
                }
                if (!shouldContinue)
                {
                    return new CopilotCodexStopOutput(
                        ShouldStop: true,
                        StopReason: stopReason,
                        SystemMessage: systemMessage);
                }
                if (decision.Length == 0)
                    return new CopilotCodexStopOutput(SystemMessage: systemMessage);
                if (reason.Length == 0)
                {
                    return CreateInvalidStopOutput(
                        systemMessage,
                        $"A configured {eventName} hook returned decision:block without a non-empty reason.");
                }
                return new CopilotCodexStopOutput(
                    ShouldContinue: true,
                    ContinuationReason: reason,
                    SystemMessage: systemMessage);
            }
        }

        internal async Task<CopilotCodexSubagentStartOutput?> OnSubagentStartAsync(
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext subagent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(subagent);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.SubagentStart)
                return null;

            var result = await RunAsync(
                request,
                BuildSubagentStartInputJson(request, subagent),
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "SubagentStart");
            if (failure != null)
                return CreateInvalidSubagentStartOutput(string.Empty, failure, "configured_hook_failed");
            if (result.ExitCode == 2)
            {
                return CreateInvalidSubagentStartOutput(
                    string.Empty,
                    "A configured SubagentStart hook exited with code 2.",
                    "configured_hook_failed");
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                if (invalidJson)
                {
                    return CreateInvalidSubagentStartOutput(
                        string.Empty,
                        "A configured SubagentStart hook returned invalid JSON.");
                }
                return new CopilotCodexSubagentStartOutput(
                    AdditionalContext: _definition.ExecutionMode == CopilotToolExecutionHookMode.Sync
                        ? result.StandardOutput?.Trim() ?? string.Empty
                        : string.Empty,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
            }
            if (root == null)
            {
                return CreateInvalidSubagentStartOutput(
                    string.Empty,
                    "A configured SubagentStart hook did not return a usable JSON document.");
            }

            using (root)
            {
                if (!HasOnlySubagentStartProperties(root.RootElement)
                    || !TryReadOptionalString(root.RootElement, "systemMessage", out var systemMessage)
                    || !TryReadOptionalString(root.RootElement, "stopReason", out _)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "continue",
                        defaultValue: true,
                        out _)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "suppressOutput",
                        defaultValue: false,
                        out _))
                {
                    return CreateInvalidSubagentStartOutput(
                        string.Empty,
                        "A configured SubagentStart hook returned an invalid universal output field.");
                }

                var additionalContext = string.Empty;
                if (!TryReadHookSpecificOutput(
                    root.RootElement,
                    "SubagentStart",
                    out var specific,
                    out var specificError))
                {
                    if (specificError.Length > 0)
                    {
                        return CreateInvalidSubagentStartOutput(
                            systemMessage,
                            specificError);
                    }
                }
                else if (!HasOnlySubagentStartSpecificProperties(specific)
                    || !TryReadOptionalString(specific, "additionalContext", out additionalContext))
                {
                    return CreateInvalidSubagentStartOutput(
                        systemMessage,
                        "A configured SubagentStart hook returned an invalid hook-specific output field.");
                }

                return new CopilotCodexSubagentStartOutput(
                    SystemMessage: systemMessage,
                    AdditionalContext: _definition.ExecutionMode == CopilotToolExecutionHookMode.Sync
                        ? additionalContext
                        : string.Empty,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
            }
        }

        private static CopilotCodexSubagentStartOutput CreateInvalidSubagentStartOutput(
            string systemMessage,
            string failureMessage,
            string failureCode = "configured_hook_invalid_output") => new(
                SystemMessage: systemMessage,
                FailureMessage: failureMessage,
                FailureCode: failureCode);

        private static CopilotCodexStopOutput CreateInvalidStopOutput(
            string systemMessage,
            string failureMessage,
            string failureCode = "configured_hook_invalid_output") => new(
                SystemMessage: systemMessage,
                StopReason: failureMessage,
                FailureCode: failureCode);

        internal async Task<CopilotCodexCompactOutput?> OnCompactAsync(
            CopilotAgentRequest request,
            CopilotCodexConfiguredHookEvent hookEvent,
            string trigger,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (hookEvent is not (CopilotCodexConfiguredHookEvent.PreCompact
                or CopilotCodexConfiguredHookEvent.PostCompact))
            {
                throw new ArgumentOutOfRangeException(nameof(hookEvent));
            }
            if (_definition.Event != hookEvent)
                return null;

            var eventName = hookEvent.ToString();
            var result = await RunAsync(
                request,
                BuildCompactInputJson(request, eventName, trigger),
                cancellationToken).ConfigureAwait(false);
            var failure = result.TimedOut
                ? $"A configured {eventName} hook exceeded its timeout."
                : result.ExitCode == 0
                    ? string.Empty
                    : $"A configured {eventName} hook exited with code {result.ExitCode}.";
            if (failure.Length > 0)
                return CreateInvalidCompactOutput(string.Empty, failure, "configured_hook_failed");

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                return invalidJson
                    ? CreateInvalidCompactOutput(
                        string.Empty,
                        $"A configured {eventName} hook returned invalid JSON.")
                    : new CopilotCodexCompactOutput();
            }
            if (root == null)
            {
                return CreateInvalidCompactOutput(
                    string.Empty,
                    $"A configured {eventName} hook did not return a usable JSON document.");
            }

            using (root)
            {
                if (!HasOnlyCompactProperties(root.RootElement)
                    || !TryReadOptionalString(root.RootElement, "systemMessage", out var systemMessage)
                    || !TryReadOptionalString(root.RootElement, "stopReason", out var stopReason)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "continue",
                        defaultValue: true,
                        out var shouldContinue)
                    || !TryReadOptionalBoolean(
                        root.RootElement,
                        "suppressOutput",
                        defaultValue: false,
                        out _))
                {
                    return CreateInvalidCompactOutput(
                        string.Empty,
                        $"A configured {eventName} hook returned an invalid universal output field.");
                }

                if (_definition.ExecutionMode == CopilotToolExecutionHookMode.Async
                    || shouldContinue)
                {
                    return new CopilotCodexCompactOutput(SystemMessage: systemMessage);
                }
                return new CopilotCodexCompactOutput(
                    ShouldStop: true,
                    StopReason: NormalizeReason(
                        stopReason,
                        $"A configured {eventName} hook stopped compaction."),
                    SystemMessage: systemMessage);
            }
        }

        private static CopilotCodexCompactOutput CreateInvalidCompactOutput(
            string systemMessage,
            string failureMessage,
            string failureCode = "configured_hook_invalid_output") => new(
                StopReason: failureMessage,
                SystemMessage: systemMessage,
                FailureCode: failureCode);

        public async Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            var output = await BeforeExecuteWithOutputAsync(
                context,
                cancellationToken).ConfigureAwait(false);
            return output?.Decision ?? CopilotToolExecutionHookDecision.Proceed;
        }

        public async Task<CopilotToolPreExecutionOutput?> BeforeExecuteWithOutputAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (_definition.Event != CopilotCodexConfiguredHookEvent.PreToolUse)
                return null;

            var result = await RunAsync(
                context.Invocation,
                outcome: null,
                cancellationToken).ConfigureAwait(false);
            var failure = GetProcessFailure(result, "PreToolUse");
            if (failure != null)
            {
                return CreatePreToolOutput(
                    CopilotToolExecutionHookDecision.Deny(
                        failure,
                        "configured_hook_failed",
                        CopilotToolFailureKind.Internal));
            }
            if (result.ExitCode == 2)
            {
                return CreatePreToolOutput(
                    CopilotToolExecutionHookDecision.Deny(
                        NormalizeReason(result.StandardError, "A configured PreToolUse hook denied this tool call."),
                        "configured_hook_denied"));
            }

            if (!TryParseJsonOutput(result.StandardOutput, out var root, out var invalidJson))
            {
                return invalidJson
                    ? CreatePreToolOutput(
                        CopilotToolExecutionHookDecision.Deny(
                            "A configured PreToolUse hook returned invalid JSON.",
                            "configured_hook_invalid_output",
                            CopilotToolFailureKind.Internal))
                    : null;
            }
            if (root == null)
            {
                return CreatePreToolOutput(
                    CopilotToolExecutionHookDecision.Deny(
                        "A configured PreToolUse hook did not return a usable JSON document.",
                        "configured_hook_invalid_output",
                        CopilotToolFailureKind.Internal));
            }
            using (root)
            {
                if (!TryReadOptionalString(
                    root.RootElement,
                    "systemMessage",
                    out var systemMessage))
                {
                    return CreateInvalidPreToolOutput(
                        string.Empty,
                        "A configured PreToolUse hook returned an invalid systemMessage.");
                }

                var additionalContext = string.Empty;
                var hasSpecificOutput = TryReadHookSpecificOutput(
                    root.RootElement,
                    "PreToolUse",
                    out var specific,
                    out var specificError);
                if (!hasSpecificOutput && specificError.Length > 0)
                {
                    return CreateInvalidPreToolOutput(
                        systemMessage,
                        specificError);
                }
                if (hasSpecificOutput
                    && !TryReadOptionalString(
                        specific,
                        "additionalContext",
                        out additionalContext))
                {
                    return CreateInvalidPreToolOutput(
                        systemMessage,
                        "A configured PreToolUse hook returned invalid additionalContext.");
                }

                if (TryReadStopDecision(root.RootElement, out var stopReason))
                {
                    return CreatePreToolOutput(
                        CopilotToolExecutionHookDecision.Deny(
                            stopReason,
                            "configured_hook_denied"),
                        systemMessage,
                        additionalContext);
                }
                if (!hasSpecificOutput)
                {
                    var output = CreatePreToolOutput(
                        CopilotToolExecutionHookDecision.Proceed,
                        systemMessage,
                        additionalContext);
                    return output.HasOutput ? output : null;
                }
                if (specific.TryGetProperty("updatedInput", out var updatedInput)
                    && updatedInput.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                {
                    return CreatePreToolOutput(
                        CopilotToolExecutionHookDecision.Deny(
                            "A configured PreToolUse hook requested an input rewrite that ColorVision cannot bind to the existing approval snapshot.",
                            "configured_hook_input_rewrite_unsupported",
                            CopilotToolFailureKind.Authorization),
                        systemMessage,
                        additionalContext);
                }
                if (!specific.TryGetProperty("permissionDecision", out var permissionDecision)
                    || permissionDecision.ValueKind == JsonValueKind.Null)
                {
                    return CreatePreToolOutput(
                        CopilotToolExecutionHookDecision.Proceed,
                        systemMessage,
                        additionalContext);
                }
                if (permissionDecision.ValueKind != JsonValueKind.String)
                {
                    return CreateInvalidPreToolOutput(
                        systemMessage,
                        "A configured PreToolUse hook returned an invalid permissionDecision.");
                }

                var permissionDecisionValue = permissionDecision.GetString();
                if (permissionDecisionValue is not ("deny" or "allow" or "ask"))
                {
                    return CreateInvalidPreToolOutput(
                        systemMessage,
                        "A configured PreToolUse hook returned an unsupported permissionDecision.");
                }
                var decision = permissionDecisionValue switch
                {
                    "deny" => CopilotToolExecutionHookDecision.Deny(
                        NormalizeReason(
                            ReadOptionalString(specific, "permissionDecisionReason"),
                            "A configured PreToolUse hook denied this tool call."),
                        "configured_hook_denied"),
                    "allow" => CopilotToolExecutionHookDecision.Proceed,
                    _ => CopilotToolExecutionHookDecision.Deny(
                        NormalizeReason(
                            ReadOptionalString(specific, "permissionDecisionReason"),
                            "A configured PreToolUse hook requested approval that is not bound to this tool invocation."),
                        "configured_hook_approval_required",
                        CopilotToolFailureKind.Authorization),
                };
                return CreatePreToolOutput(
                    decision,
                    systemMessage,
                    additionalContext);
            }
        }

        private CopilotToolPreExecutionOutput CreateInvalidPreToolOutput(
            string systemMessage,
            string failureMessage) => CreatePreToolOutput(
                CopilotToolExecutionHookDecision.Deny(
                    failureMessage,
                    "configured_hook_invalid_output",
                    CopilotToolFailureKind.Internal),
                systemMessage);

        private CopilotToolPreExecutionOutput CreatePreToolOutput(
            CopilotToolExecutionHookDecision decision,
            string systemMessage = "",
            string additionalContext = "") => new(
                decision,
                systemMessage,
                additionalContext,
                _definition.AdditionalContextLimitTokens);

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
                    AdditionalContext: additionalContext,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
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
                    Control: CopilotToolPostExecutionControl.Stopped,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
            }
            if (invalidReason.Length > 0)
                return CreateInvalidPostToolOutput(systemMessage, invalidReason);
            if (hasBlockDecision)
            {
                return new CopilotToolPostExecutionOutput(
                    FeedbackMessage: reason,
                    SystemMessage: systemMessage,
                    AdditionalContext: additionalContext,
                    Control: CopilotToolPostExecutionControl.Blocked,
                    AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
            }
            return new CopilotToolPostExecutionOutput(
                SystemMessage: systemMessage,
                AdditionalContext: additionalContext,
                AdditionalContextLimitTokens: _definition.AdditionalContextLimitTokens);
        }

        private static CopilotToolPostExecutionOutput CreateInvalidPostToolOutput(
            string systemMessage,
            string failureMessage) => new(
                SystemMessage: systemMessage,
                FailureMessage: failureMessage);

        private static bool HasOnlyPermissionRequestProperties(JsonElement root)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not (
                    "continue"
                    or "stopReason"
                    or "suppressOutput"
                    or "systemMessage"
                    or "hookSpecificOutput"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlyUserPromptSubmitProperties(JsonElement root)
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

        private static bool HasOnlyStopProperties(JsonElement root)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not (
                    "continue"
                    or "stopReason"
                    or "suppressOutput"
                    or "systemMessage"
                    or "decision"
                    or "reason"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlySubagentStartProperties(JsonElement root)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not (
                    "continue"
                    or "stopReason"
                    or "suppressOutput"
                    or "systemMessage"
                    or "hookSpecificOutput"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlyCompactProperties(JsonElement root)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not (
                    "continue"
                    or "stopReason"
                    or "suppressOutput"
                    or "systemMessage"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlyUserPromptSubmitSpecificProperties(JsonElement specific)
        {
            foreach (var property in specific.EnumerateObject())
            {
                if (property.Name is not (
                    "hookEventName"
                    or "additionalContext"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlySubagentStartSpecificProperties(JsonElement specific)
        {
            foreach (var property in specific.EnumerateObject())
            {
                if (property.Name is not (
                    "hookEventName"
                    or "additionalContext"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlyPermissionRequestSpecificProperties(JsonElement specific)
        {
            foreach (var property in specific.EnumerateObject())
            {
                if (property.Name is not (
                    "hookEventName"
                    or "decision"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasOnlyPermissionRequestDecisionProperties(JsonElement decision)
        {
            foreach (var property in decision.EnumerateObject())
            {
                if (property.Name is not (
                    "behavior"
                    or "updatedInput"
                    or "updatedPermissions"
                    or "message"
                    or "interrupt"))
                {
                    return false;
                }
            }
            return true;
        }

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

        private async Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _runner.RunAsync(
                    _definition,
                    request,
                    standardInput,
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

        private string BuildUserPromptSubmitInputJson(
            CopilotAgentRequest request,
            string prompt)
        {
            var input = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_id"] = request.ConversationId,
                ["turn_id"] = request.TaskId,
                ["transcript_path"] = null,
                ["cwd"] = CopilotCodexCommandHookRunner.ResolveWorkingDirectory(request),
                ["hook_event_name"] = "UserPromptSubmit",
                ["model"] = request.Profile?.Model ?? string.Empty,
                ["permission_mode"] = ResolvePermissionMode(request),
                ["prompt"] = prompt ?? string.Empty,
            };
            return JsonSerializer.Serialize(input) + Environment.NewLine;
        }

        private static string BuildStopInputJson(
            CopilotAgentRequest request,
            bool stopHookActive,
            string? lastAssistantMessage)
        {
            var input = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_id"] = request.ConversationId,
                ["turn_id"] = request.TaskId,
                ["transcript_path"] = null,
                ["cwd"] = CopilotCodexCommandHookRunner.ResolveWorkingDirectory(request),
                ["hook_event_name"] = "Stop",
                ["model"] = request.Profile?.Model ?? string.Empty,
                ["permission_mode"] = ResolvePermissionMode(request),
                ["stop_hook_active"] = stopHookActive,
                ["last_assistant_message"] = string.IsNullOrWhiteSpace(lastAssistantMessage)
                    ? null
                    : lastAssistantMessage,
            };
            return JsonSerializer.Serialize(input) + Environment.NewLine;
        }

        private static string BuildSubagentStartInputJson(
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext subagent)
        {
            var input = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_id"] = request.ConversationId,
                ["turn_id"] = subagent.TurnId,
                ["transcript_path"] = null,
                ["cwd"] = CopilotCodexCommandHookRunner.ResolveWorkingDirectory(request),
                ["hook_event_name"] = "SubagentStart",
                ["model"] = request.Profile?.Model ?? string.Empty,
                ["permission_mode"] = ResolvePermissionMode(request),
                ["agent_id"] = subagent.AgentId,
                ["agent_type"] = subagent.AgentType,
            };
            return JsonSerializer.Serialize(input) + Environment.NewLine;
        }

        private static string BuildSubagentStopInputJson(
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext subagent,
            bool stopHookActive,
            string? lastAssistantMessage)
        {
            var input = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_id"] = request.ConversationId,
                ["turn_id"] = subagent.TurnId,
                ["transcript_path"] = null,
                ["cwd"] = CopilotCodexCommandHookRunner.ResolveWorkingDirectory(request),
                ["hook_event_name"] = "SubagentStop",
                ["model"] = request.Profile?.Model ?? string.Empty,
                ["permission_mode"] = ResolvePermissionMode(request),
                ["agent_id"] = subagent.AgentId,
                ["agent_type"] = subagent.AgentType,
                ["agent_transcript_path"] = null,
                ["stop_hook_active"] = stopHookActive,
                ["last_assistant_message"] = string.IsNullOrWhiteSpace(lastAssistantMessage)
                    ? null
                    : lastAssistantMessage,
            };
            return JsonSerializer.Serialize(input) + Environment.NewLine;
        }

        private static string BuildCompactInputJson(
            CopilotAgentRequest request,
            string eventName,
            string trigger)
        {
            var input = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_id"] = request.ConversationId,
                ["turn_id"] = request.TaskId,
                ["transcript_path"] = null,
                ["cwd"] = CopilotCodexCommandHookRunner.ResolveWorkingDirectory(request),
                ["hook_event_name"] = eventName,
                ["model"] = request.Profile?.Model ?? string.Empty,
                ["trigger"] = trigger ?? string.Empty,
            };
            return JsonSerializer.Serialize(input) + Environment.NewLine;
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

        private static bool HasNonNullProperty(JsonElement value, string propertyName)
        {
            return value.TryGetProperty(propertyName, out var property)
                && property.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
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
                .Where(definition => definition != null
                    && definition.Phases != CopilotToolExecutionHookPhases.None
                    && definition.Matches(toolName))
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
