using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    [Flags]
    internal enum CopilotToolExecutionHookPhases
    {
        None = 0,
        PermissionRequest = 1,
        BeforeExecute = 2,
        AfterExecute = 4,
        All = PermissionRequest | BeforeExecute | AfterExecute,
    }

    internal enum CopilotCodexConfiguredHookEvent
    {
        PermissionRequest,
        PreToolUse,
        PostToolUse,
        UserPromptSubmit,
        Stop,
    }

    internal sealed record CopilotCodexConfiguredHookIssue(
        string SourceFilePath,
        string Message);

    internal sealed record CopilotCodexCommandHookDefinition(
        string SourceId,
        string SourceFilePath,
        CopilotProjectInstructionConfigSources Source,
        CopilotCodexConfiguredHookEvent Event,
        string ToolNamePattern,
        string Command,
        int TimeoutSeconds,
        string StatusMessage,
        CopilotToolExecutionHookMode ExecutionMode,
        int Order,
        string ConfigurationFingerprint,
        int AdditionalContextLimitTokens = CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens)
    {
        public CopilotToolExecutionHookPhases Phases => Event switch
        {
            CopilotCodexConfiguredHookEvent.PermissionRequest =>
                CopilotToolExecutionHookPhases.PermissionRequest,
            CopilotCodexConfiguredHookEvent.PreToolUse =>
                CopilotToolExecutionHookPhases.BeforeExecute,
            CopilotCodexConfiguredHookEvent.PostToolUse =>
                CopilotToolExecutionHookPhases.AfterExecute,
            _ => CopilotToolExecutionHookPhases.None,
        };

        public CopilotCodexCommandHookDefinition CreateSnapshot() => this with { };

        public bool IsStructurallyValid()
        {
            return !string.IsNullOrWhiteSpace(SourceId)
                && string.Equals(SourceId, SourceId.Trim(), StringComparison.Ordinal)
                && SourceId.Length <= CopilotToolExecutionHookRegistry.MaxSourceIdLength
                && !SourceId.Any(char.IsControl)
                && Path.IsPathFullyQualified(SourceFilePath)
                && Enum.IsDefined(Source)
                && Source != CopilotProjectInstructionConfigSources.None
                && Enum.IsDefined(Event)
                && IsValidMatcher(ToolNamePattern)
                && !string.IsNullOrWhiteSpace(Command)
                && Command.Length <= CopilotProjectInstructionDiscoveryConfig.MaximumHookCommandCharacters
                && !Command.Contains('\0')
                && TimeoutSeconds >= 1
                && StatusMessage.Length <= CopilotProjectInstructionDiscoveryConfig.MaximumHookStatusMessageCharacters
                && !StatusMessage.Contains('\0')
                && Enum.IsDefined(ExecutionMode)
                && Order >= 0
                && AdditionalContextLimitTokens >= 0
                && ConfigurationFingerprint.Length == 64
                && ConfigurationFingerprint.All(Uri.IsHexDigit);
        }

        public bool Matches(string toolName)
        {
            if (!IsStructurallyValid())
                return false;

            var pattern = ToolNamePattern == "*" ? ".*" : ToolNamePattern;
            Regex matcher;
            try
            {
                matcher = new Regex(
                    pattern,
                    RegexOptions.IgnoreCase
                        | RegexOptions.CultureInvariant
                        | RegexOptions.NonBacktracking,
                    TimeSpan.FromMilliseconds(100));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return false;
            }

            return CopilotCodexConfiguredHookToolNames.GetMatcherInputs(toolName)
                .Any(matcher.IsMatch);
        }

        internal static bool IsValidMatcher(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > CopilotToolExecutionHookRegistry.MaxToolNamePatternLength
                || value.Contains('\0'))
            {
                return false;
            }

            try
            {
                _ = new Regex(
                    value == "*" ? ".*" : value,
                    RegexOptions.IgnoreCase
                        | RegexOptions.CultureInvariant
                        | RegexOptions.NonBacktracking,
                    TimeSpan.FromMilliseconds(100));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }

    internal sealed record CopilotCodexConfiguredHookDiscoveryResult(
        IReadOnlyList<CopilotCodexCommandHookDefinition> CommandHooks,
        IReadOnlyList<CopilotCodexConfiguredHookIssue> Issues,
        IReadOnlyList<string> SourceFilePaths)
    {
        public static CopilotCodexConfiguredHookDiscoveryResult Empty { get; } =
            new([], [], []);
    }

    internal static class CopilotCodexConfiguredHookToolNames
    {
        public static string GetCanonicalName(string? toolName)
        {
            var normalized = toolName?.Trim() ?? string.Empty;
            return normalized is "RunShellCommand" or "StartBackgroundShellCommand"
                ? "Bash"
                : normalized;
        }

        public static IReadOnlyList<string> GetMatcherInputs(string? toolName)
        {
            var normalized = toolName?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                return Array.Empty<string>();

            if (normalized is "RunShellCommand" or "StartBackgroundShellCommand")
                return ["Bash", normalized, "Shell"];
            return [normalized];
        }
    }

    internal static partial class CopilotProjectInstructionDiscoveryConfig
    {
        internal const int MaximumConfiguredHookHandlers = 128;
        internal const int MaximumHookCommandCharacters = 16_384;
        internal const int MaximumHookStatusMessageCharacters = 512;
        private const int DefaultHookTimeoutSeconds = 600;
        private const string HooksFileName = "hooks.json";

        private sealed class TomlCommandHookHandler
        {
            public HashSet<string> AssignedKeys { get; } = new(StringComparer.Ordinal);
            public string Type { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
            public string CommandWindows { get; set; } = string.Empty;
            public string CommandWindowsSnake { get; set; } = string.Empty;
            public int? TimeoutSeconds { get; set; }
            public bool? IsAsync { get; set; }
            public string StatusMessage { get; set; } = string.Empty;
            public int? AdditionalContextLimitTokens { get; set; }
            public string Error { get; set; } = string.Empty;
        }

        private static CopilotCodexConfiguredHookDiscoveryResult DiscoverConfiguredHooksForLayer(
            string allowedRootPath,
            string configFilePath,
            string configSource,
            string hookFilePath,
            CopilotProjectInstructionConfigSources source,
            int startingOrder)
        {
            var json = DiscoverConfiguredHookFile(
                allowedRootPath,
                hookFilePath,
                source,
                startingOrder);
            var inline = DiscoverConfiguredHooksInToml(
                configFilePath,
                configSource,
                source,
                startingOrder + json.CommandHooks.Count);
            var definitions = json.CommandHooks.Concat(inline.CommandHooks).ToArray();
            var issues = json.Issues.Concat(inline.Issues).ToList();
            if (json.SourceFilePaths.Count > 0 && inline.SourceFilePaths.Count > 0)
            {
                issues.Add(new CopilotCodexConfiguredHookIssue(
                    NormalizeHookSourcePath(configFilePath),
                    "This configuration layer defines hooks in both hooks.json and config.toml; both sets were loaded."));
            }

            return new CopilotCodexConfiguredHookDiscoveryResult(
                definitions,
                issues.ToArray(),
                json.SourceFilePaths
                    .Concat(inline.SourceFilePaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        private static CopilotCodexConfiguredHookDiscoveryResult DiscoverConfiguredHooksInToml(
            string configFilePath,
            string configSource,
            CopilotProjectInstructionConfigSources source,
            int startingOrder)
        {
            var normalizedPath = NormalizeHookSourcePath(configFilePath);
            if (normalizedPath.Length == 0 || string.IsNullOrWhiteSpace(configSource))
                return CopilotCodexConfiguredHookDiscoveryResult.Empty;

            var definitions = new List<CopilotCodexCommandHookDefinition>();
            var issues = new List<CopilotCodexConfiguredHookIssue>();
            var unsupportedEvents = new HashSet<string>(StringComparer.Ordinal);
            var lines = NormalizeLines(configSource);
            var sawHookDeclaration = false;
            var hasMatcherGroup = false;
            var matcherEventName = string.Empty;
            var matcher = "*";
            var matcherIsValid = true;
            var matcherWasAssigned = false;
            var acceptsHandlers = false;
            var hookEvent = default(CopilotCodexConfiguredHookEvent);
            TomlCommandHookHandler? handler = null;
            var handlerLimitReported = false;

            void AddIssue(string message) =>
                issues.Add(new CopilotCodexConfiguredHookIssue(normalizedPath, message));

            void CompleteHandler()
            {
                if (handler == null)
                    return;

                var completed = handler;
                handler = null;
                if (!acceptsHandlers)
                    return;
                if (!matcherIsValid)
                    return;
                if (completed.Error.Length > 0)
                {
                    AddIssue(completed.Error);
                    return;
                }
                if (startingOrder + definitions.Count >= MaximumConfiguredHookHandlers)
                {
                    if (!handlerLimitReported)
                    {
                        AddIssue($"Configured command hooks are limited to {MaximumConfiguredHookHandlers} handlers.");
                        handlerLimitReported = true;
                    }
                    return;
                }
                if (completed.Type.Length == 0)
                {
                    AddIssue($"Hook event '{hookEvent}' contains a handler without a valid type.");
                    return;
                }
                if (!TryCreateCommandHookDefinition(
                    completed.Type,
                    completed.Command,
                    completed.CommandWindows,
                    completed.CommandWindowsSnake,
                    completed.TimeoutSeconds,
                    completed.IsAsync,
                    completed.StatusMessage,
                    completed.AdditionalContextLimitTokens,
                    normalizedPath,
                    source,
                    hookEvent,
                    matcher,
                    startingOrder + definitions.Count,
                    out var definition,
                    out var error))
                {
                    AddIssue(error);
                    return;
                }
                if (hookEvent == CopilotCodexConfiguredHookEvent.PermissionRequest
                    && completed.AdditionalContextLimitTokens.HasValue)
                {
                    AddIssue(
                        "Hook event 'PermissionRequest' ignores additionalContextLimit because it cannot return additional context.");
                }
                if (hookEvent == CopilotCodexConfiguredHookEvent.Stop
                    && completed.AdditionalContextLimitTokens.HasValue)
                {
                    AddIssue(
                        "Hook event 'Stop' ignores additionalContextLimit because it cannot return additional context.");
                }
                if (hookEvent == CopilotCodexConfiguredHookEvent.UserPromptSubmit
                    && completed.IsAsync == true)
                {
                    AddIssue(
                        "Asynchronous UserPromptSubmit command hooks are parsed but skipped because their output cannot affect the submitted turn.");
                }
                definitions.Add(definition!);
            }

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = StripComment(lines[lineIndex]).Trim();
                if (line.Length == 0)
                    continue;

                if (line[0] == '[')
                {
                    CompleteHandler();
                    if (!TryParseHookArrayTableHeader(
                        line,
                        out var eventName,
                        out var isHandlerTable))
                    {
                        if (line.StartsWith("[[hooks.", StringComparison.Ordinal))
                        {
                            sawHookDeclaration = true;
                            AddIssue("config.toml contains an invalid inline hook table header.");
                        }
                        hasMatcherGroup = false;
                        acceptsHandlers = false;
                        continue;
                    }

                    sawHookDeclaration = true;
                    if (!isHandlerTable)
                    {
                        hasMatcherGroup = true;
                        matcherEventName = eventName;
                        matcher = "*";
                        matcherIsValid = true;
                        matcherWasAssigned = false;
                        acceptsHandlers = TryParseSupportedHookEvent(eventName, out hookEvent);
                        if (!acceptsHandlers && unsupportedEvents.Add(eventName))
                        {
                            AddIssue($"Hook event '{eventName}' is not connected to the ColorVision tool lifecycle yet.");
                        }
                        continue;
                    }

                    acceptsHandlers = hasMatcherGroup
                        && string.Equals(eventName, matcherEventName, StringComparison.Ordinal)
                        && TryParseSupportedHookEvent(eventName, out hookEvent);
                    if (!acceptsHandlers)
                    {
                        if (TryParseSupportedHookEvent(eventName, out _))
                        {
                            AddIssue($"Hook event '{eventName}' contains a handler without a preceding matcher group.");
                        }
                        else if (unsupportedEvents.Add(eventName))
                        {
                            AddIssue($"Hook event '{eventName}' is not connected to the ColorVision tool lifecycle yet.");
                        }
                    }
                    handler = new TomlCommandHookHandler();
                    continue;
                }

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                    continue;
                var key = line[..equalsIndex].Trim();
                var value = ReadTomlHookAssignmentValue(
                    lines,
                    ref lineIndex,
                    line[(equalsIndex + 1)..].Trim());
                if (handler != null)
                {
                    ApplyTomlHookHandlerAssignment(handler, hookEvent, key, value);
                    continue;
                }
                if (!hasMatcherGroup
                    || !string.Equals(key, "matcher", StringComparison.Ordinal))
                {
                    continue;
                }
                if (matcherWasAssigned)
                {
                    matcherIsValid = false;
                    AddIssue($"Hook event '{matcherEventName}' contains a duplicate matcher assignment.");
                    continue;
                }

                matcherWasAssigned = true;
                if (!TryParseConfiguredText(
                        value,
                        CopilotToolExecutionHookRegistry.MaxToolNamePatternLength,
                        out matcher))
                {
                    matcherIsValid = false;
                    AddIssue($"Hook event '{matcherEventName}' contains an invalid matcher.");
                    continue;
                }
                if (matcher.Length == 0)
                    matcher = "*";
                matcherIsValid = CopilotCodexCommandHookDefinition.IsValidMatcher(matcher);
                if (!matcherIsValid)
                    AddIssue($"Hook event '{matcherEventName}' contains an invalid matcher.");
            }

            CompleteHandler();
            return sawHookDeclaration
                ? new CopilotCodexConfiguredHookDiscoveryResult(
                    definitions.ToArray(),
                    issues.ToArray(),
                    [normalizedPath])
                : CopilotCodexConfiguredHookDiscoveryResult.Empty;
        }

        private static bool TryParseHookArrayTableHeader(
            string line,
            out string eventName,
            out bool isHandlerTable)
        {
            eventName = string.Empty;
            isHandlerTable = false;
            if (line.Length < 7
                || !line.StartsWith("[[", StringComparison.Ordinal)
                || !line.EndsWith("]]", StringComparison.Ordinal))
            {
                return false;
            }

            var segments = line[2..^2]
                .Split('.', StringSplitOptions.TrimEntries);
            if (segments.Length is not (2 or 3)
                || !string.Equals(segments[0], "hooks", StringComparison.Ordinal)
                || segments[1].Length == 0
                || (segments.Length == 3
                    && !string.Equals(segments[2], "hooks", StringComparison.Ordinal)))
            {
                return false;
            }

            eventName = segments[1];
            isHandlerTable = segments.Length == 3;
            return true;
        }

        private static string ReadTomlHookAssignmentValue(
            string[] lines,
            ref int lineIndex,
            string value)
        {
            if (!TryGetMultilineStringDelimiter(value, out var delimiter)
                || HasClosedMultilineString(value, delimiter))
            {
                return value;
            }

            var builder = new StringBuilder(value);
            for (var logicalLine = 1;
                logicalLine < MaximumConfiguredTextLines && lineIndex + 1 < lines.Length;
                logicalLine++)
            {
                lineIndex++;
                builder.Append('\n').Append(lines[lineIndex]);
                if (HasClosedMultilineString(builder.ToString(), delimiter))
                    break;
            }
            return builder.ToString();
        }

        private static void ApplyTomlHookHandlerAssignment(
            TomlCommandHookHandler handler,
            CopilotCodexConfiguredHookEvent hookEvent,
            string key,
            string value)
        {
            if (key is not ("type"
                    or "command"
                    or "commandWindows"
                    or "command_windows"
                    or "timeout"
                    or "async"
                    or "statusMessage"
                    or "additionalContextLimit"))
            {
                return;
            }
            if (!handler.AssignedKeys.Add(key))
            {
                handler.Error = $"Hook event '{hookEvent}' contains a duplicate '{key}' handler assignment.";
                return;
            }

            switch (key)
            {
                case "type":
                    if (TryParseConfiguredText(value, MaximumPersonalityCharacters, out var type))
                        handler.Type = type;
                    else
                        handler.Error = $"Hook event '{hookEvent}' contains a handler without a valid type.";
                    break;
                case "command":
                    AssignTomlHookText(value, MaximumHookCommandCharacters, text => handler.Command = text);
                    break;
                case "commandWindows":
                    AssignTomlHookText(value, MaximumHookCommandCharacters, text => handler.CommandWindows = text);
                    break;
                case "command_windows":
                    AssignTomlHookText(value, MaximumHookCommandCharacters, text => handler.CommandWindowsSnake = text);
                    break;
                case "timeout":
                    if (TryParseHookTimeout(value, out var timeoutSeconds))
                        handler.TimeoutSeconds = timeoutSeconds;
                    else
                        handler.Error = $"Hook event '{hookEvent}' command timeout must be a non-negative integer.";
                    break;
                case "async":
                    if (TryParseTomlBoolean(value, out var isAsync))
                        handler.IsAsync = isAsync;
                    else
                        handler.Error = $"Hook event '{hookEvent}' command async flag must be a boolean.";
                    break;
                case "statusMessage":
                    AssignTomlHookText(
                        value,
                        MaximumHookStatusMessageCharacters,
                        text => handler.StatusMessage = text);
                    break;
                case "additionalContextLimit":
                    if (TryParseHookTimeout(value, out var additionalContextLimitTokens))
                        handler.AdditionalContextLimitTokens = additionalContextLimitTokens;
                    else
                        handler.Error = $"Hook event '{hookEvent}' additionalContextLimit must be a non-negative integer.";
                    break;
            }

            void AssignTomlHookText(string sourceText, int maximumCharacters, Action<string> assign)
            {
                if (TryParseConfiguredText(sourceText, maximumCharacters, out var parsed))
                {
                    assign(parsed);
                    return;
                }
                handler.Error = $"Hook event '{hookEvent}' handler field '{key}' is invalid or oversized.";
            }
        }

        private static bool TryParseHookTimeout(string value, out int timeoutSeconds)
        {
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out timeoutSeconds)
                && timeoutSeconds >= 0;
        }

        private static CopilotCodexConfiguredHookDiscoveryResult DiscoverConfiguredHookFile(
            string allowedRootPath,
            string hookFilePath,
            CopilotProjectInstructionConfigSources source,
            int startingOrder)
        {
            var normalizedPath = NormalizeHookSourcePath(hookFilePath);
            if (normalizedPath.Length == 0 || !File.Exists(normalizedPath))
                return CopilotCodexConfiguredHookDiscoveryResult.Empty;
            if (!TryReadConfigSource(allowedRootPath, normalizedPath, out var json))
            {
                return new CopilotCodexConfiguredHookDiscoveryResult(
                    [],
                    [new CopilotCodexConfiguredHookIssue(
                        normalizedPath,
                        "hooks.json could not be read safely or exceeds the configuration size limit.")],
                    [normalizedPath]);
            }

            var hooks = new List<CopilotCodexCommandHookDefinition>();
            var issues = new List<CopilotCodexConfiguredHookIssue>();
            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("hooks", out var events)
                    || events.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new CopilotCodexConfiguredHookIssue(
                        normalizedPath,
                        "hooks.json must contain a top-level hooks object."));
                    return new(hooks, issues, [normalizedPath]);
                }

                foreach (var eventProperty in events.EnumerateObject())
                {
                    if (!TryParseSupportedHookEvent(eventProperty.Name, out var hookEvent))
                    {
                        if (eventProperty.Value.ValueKind == JsonValueKind.Array
                            && eventProperty.Value.GetArrayLength() > 0)
                        {
                            issues.Add(new CopilotCodexConfiguredHookIssue(
                                normalizedPath,
                                $"Hook event '{eventProperty.Name}' is not connected to the ColorVision tool lifecycle yet."));
                        }
                        continue;
                    }
                    ParseMatcherGroups(
                        eventProperty.Value,
                        normalizedPath,
                        source,
                        hookEvent,
                        startingOrder,
                        hooks,
                        issues);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                issues.Add(new CopilotCodexConfiguredHookIssue(
                    normalizedPath,
                    "hooks.json is invalid: " + CopilotUserFacingErrorFormatter.Sanitize(ex.Message)));
            }

            return new(hooks.ToArray(), issues.ToArray(), [normalizedPath]);
        }

        private static void ParseMatcherGroups(
            JsonElement groups,
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            CopilotCodexConfiguredHookEvent hookEvent,
            int startingOrder,
            List<CopilotCodexCommandHookDefinition> definitions,
            List<CopilotCodexConfiguredHookIssue> issues)
        {
            if (groups.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new CopilotCodexConfiguredHookIssue(
                    sourceFilePath,
                    $"Hook event '{hookEvent}' must be an array of matcher groups."));
                return;
            }

            foreach (var group in groups.EnumerateArray())
            {
                if (startingOrder + definitions.Count >= MaximumConfiguredHookHandlers)
                {
                    issues.Add(new CopilotCodexConfiguredHookIssue(
                        sourceFilePath,
                        $"Configured command hooks are limited to {MaximumConfiguredHookHandlers} handlers."));
                    return;
                }
                if (group.ValueKind != JsonValueKind.Object
                    || !group.TryGetProperty("hooks", out var handlers)
                    || handlers.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new CopilotCodexConfiguredHookIssue(
                        sourceFilePath,
                        $"Hook event '{hookEvent}' contains an invalid matcher group."));
                    continue;
                }

                var matcher = group.TryGetProperty("matcher", out var matcherElement)
                    && matcherElement.ValueKind == JsonValueKind.String
                        ? matcherElement.GetString()?.Trim() ?? string.Empty
                        : "*";
                if (matcher.Length == 0)
                    matcher = "*";
                if (!CopilotCodexCommandHookDefinition.IsValidMatcher(matcher))
                {
                    issues.Add(new CopilotCodexConfiguredHookIssue(
                        sourceFilePath,
                        $"Hook event '{hookEvent}' contains an invalid matcher."));
                    continue;
                }

                foreach (var handler in handlers.EnumerateArray())
                {
                    if (startingOrder + definitions.Count >= MaximumConfiguredHookHandlers)
                    {
                        issues.Add(new CopilotCodexConfiguredHookIssue(
                            sourceFilePath,
                            $"Configured command hooks are limited to {MaximumConfiguredHookHandlers} handlers."));
                        return;
                    }

                    if (!TryCreateCommandHookDefinition(
                        handler,
                        sourceFilePath,
                        source,
                        hookEvent,
                        matcher,
                        startingOrder + definitions.Count,
                        out var definition,
                        out var error))
                    {
                        issues.Add(new CopilotCodexConfiguredHookIssue(sourceFilePath, error));
                        continue;
                    }
                    if (hookEvent == CopilotCodexConfiguredHookEvent.PermissionRequest
                        && handler.TryGetProperty("additionalContextLimit", out _))
                    {
                        issues.Add(new CopilotCodexConfiguredHookIssue(
                            sourceFilePath,
                            "Hook event 'PermissionRequest' ignores additionalContextLimit because it cannot return additional context."));
                    }
                    if (hookEvent == CopilotCodexConfiguredHookEvent.Stop
                        && handler.TryGetProperty("additionalContextLimit", out _))
                    {
                        issues.Add(new CopilotCodexConfiguredHookIssue(
                            sourceFilePath,
                            "Hook event 'Stop' ignores additionalContextLimit because it cannot return additional context."));
                    }
                    if (hookEvent == CopilotCodexConfiguredHookEvent.UserPromptSubmit
                        && handler.TryGetProperty("async", out var asyncElement)
                        && asyncElement.ValueKind == JsonValueKind.True)
                    {
                        issues.Add(new CopilotCodexConfiguredHookIssue(
                            sourceFilePath,
                            "Asynchronous UserPromptSubmit command hooks are parsed but skipped because their output cannot affect the submitted turn."));
                    }
                    definitions.Add(definition!);
                }
            }
        }

        private static bool TryCreateCommandHookDefinition(
            JsonElement handler,
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            CopilotCodexConfiguredHookEvent hookEvent,
            string matcher,
            int order,
            out CopilotCodexCommandHookDefinition? definition,
            out string error)
        {
            definition = null;
            error = string.Empty;
            if (handler.ValueKind != JsonValueKind.Object
                || !handler.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                error = $"Hook event '{hookEvent}' contains a handler without a valid type.";
                return false;
            }

            var handlerType = typeElement.GetString()?.Trim() ?? string.Empty;
            if (!string.Equals(handlerType, "command", StringComparison.Ordinal))
            {
                error = $"Hook handler type '{handlerType}' is not connected to the ColorVision tool lifecycle yet.";
                return false;
            }

            int? timeoutSeconds = null;
            if (handler.TryGetProperty("timeout", out var timeoutElement))
            {
                if (timeoutElement.ValueKind != JsonValueKind.Number
                    || !timeoutElement.TryGetInt32(out var parsedTimeoutSeconds))
                {
                    error = $"Hook event '{hookEvent}' command timeout must be an integer.";
                    return false;
                }
                timeoutSeconds = parsedTimeoutSeconds;
            }

            bool? isAsync = null;
            if (handler.TryGetProperty("async", out var asyncElement))
            {
                if (asyncElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    error = $"Hook event '{hookEvent}' command async flag must be a boolean.";
                    return false;
                }
                isAsync = asyncElement.ValueKind == JsonValueKind.True;
            }

            int? additionalContextLimitTokens = null;
            if (handler.TryGetProperty("additionalContextLimit", out var additionalContextLimitElement))
            {
                if (additionalContextLimitElement.ValueKind != JsonValueKind.Number
                    || !additionalContextLimitElement.TryGetInt32(out var parsedAdditionalContextLimitTokens)
                    || parsedAdditionalContextLimitTokens < 0)
                {
                    error = $"Hook event '{hookEvent}' additionalContextLimit must be a non-negative integer.";
                    return false;
                }
                additionalContextLimitTokens = parsedAdditionalContextLimitTokens;
            }

            return TryCreateCommandHookDefinition(
                handlerType,
                ReadOptionalString(handler, "command"),
                ReadOptionalString(handler, "commandWindows"),
                ReadOptionalString(handler, "command_windows"),
                timeoutSeconds,
                isAsync,
                ReadOptionalString(handler, "statusMessage"),
                additionalContextLimitTokens,
                sourceFilePath,
                source,
                hookEvent,
                matcher,
                order,
                out definition,
                out error);
        }

        private static bool TryCreateCommandHookDefinition(
            string handlerType,
            string command,
            string commandWindows,
            string commandWindowsSnake,
            int? configuredTimeoutSeconds,
            bool? configuredAsync,
            string statusMessage,
            int? configuredAdditionalContextLimitTokens,
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            CopilotCodexConfiguredHookEvent hookEvent,
            string matcher,
            int order,
            out CopilotCodexCommandHookDefinition? definition,
            out string error)
        {
            definition = null;
            error = string.Empty;
            if (!string.Equals(handlerType, "command", StringComparison.Ordinal))
            {
                error = $"Hook handler type '{handlerType}' is not connected to the ColorVision tool lifecycle yet.";
                return false;
            }

            var selectedCommand = commandWindows.Length > 0
                ? commandWindows
                : commandWindowsSnake.Length > 0
                    ? commandWindowsSnake
                    : command;
            if (selectedCommand.Length == 0
                || selectedCommand.Length > MaximumHookCommandCharacters
                || selectedCommand.Contains('\0'))
            {
                error = $"Hook event '{hookEvent}' contains an empty or oversized command handler.";
                return false;
            }

            if (configuredTimeoutSeconds < 0)
            {
                error = $"Hook event '{hookEvent}' command timeout must be a non-negative integer.";
                return false;
            }
            var timeoutSeconds = Math.Max(
                1,
                configuredTimeoutSeconds ?? DefaultHookTimeoutSeconds);

            if (statusMessage.Length > MaximumHookStatusMessageCharacters
                || statusMessage.Contains('\0'))
            {
                error = $"Hook event '{hookEvent}' statusMessage is invalid or oversized.";
                return false;
            }

            var executionMode = configuredAsync == true
                ? CopilotToolExecutionHookMode.Async
                : CopilotToolExecutionHookMode.Sync;
            var additionalContextLimitTokens = configuredAdditionalContextLimitTokens
                ?? CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens;
            var fingerprint = ComputeHookFingerprint(
                sourceFilePath,
                source,
                hookEvent,
                matcher,
                selectedCommand,
                timeoutSeconds,
                statusMessage,
                additionalContextLimitTokens,
                executionMode,
                order);
            definition = new CopilotCodexCommandHookDefinition(
                "codex-config:" + fingerprint[..32],
                sourceFilePath,
                source,
                hookEvent,
                matcher,
                selectedCommand,
                timeoutSeconds,
                statusMessage,
                executionMode,
                order,
                fingerprint,
                additionalContextLimitTokens);
            return true;
        }

        private static string ReadOptionalString(JsonElement value, string propertyName)
        {
            return value.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString()?.Trim() ?? string.Empty
                    : string.Empty;
        }

        private static bool TryParseSupportedHookEvent(
            string value,
            out CopilotCodexConfiguredHookEvent hookEvent)
        {
            hookEvent = value switch
            {
                "PermissionRequest" => CopilotCodexConfiguredHookEvent.PermissionRequest,
                "PreToolUse" => CopilotCodexConfiguredHookEvent.PreToolUse,
                "PostToolUse" => CopilotCodexConfiguredHookEvent.PostToolUse,
                "UserPromptSubmit" => CopilotCodexConfiguredHookEvent.UserPromptSubmit,
                "Stop" => CopilotCodexConfiguredHookEvent.Stop,
                _ => default,
            };
            return value is "PermissionRequest" or "PreToolUse" or "PostToolUse" or "UserPromptSubmit" or "Stop";
        }

        private static string ComputeHookFingerprint(
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            CopilotCodexConfiguredHookEvent hookEvent,
            string matcher,
            string command,
            int timeoutSeconds,
            string statusMessage,
            int additionalContextLimitTokens,
            CopilotToolExecutionHookMode executionMode,
            int order)
        {
            var stableData = JsonSerializer.Serialize(new
            {
                SourceFilePath = sourceFilePath.ToUpperInvariant(),
                Source = source.ToString(),
                Event = hookEvent.ToString(),
                Matcher = matcher,
                Command = command,
                TimeoutSeconds = timeoutSeconds,
                StatusMessage = statusMessage,
                AdditionalContextLimitTokens = additionalContextLimitTokens,
                ExecutionMode = executionMode.ToString(),
                Order = order,
            });
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(stableData))).ToLowerInvariant();
        }

        private static string NormalizeHookSourcePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
