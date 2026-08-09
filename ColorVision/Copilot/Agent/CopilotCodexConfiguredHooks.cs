using System;
using System.Collections.Generic;
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
        string ConfigurationFingerprint)
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
                && Command.IndexOf('\0') < 0
                && TimeoutSeconds is >= 1 and <= CopilotProjectInstructionDiscoveryConfig.MaximumHookTimeoutSeconds
                && StatusMessage.Length <= CopilotProjectInstructionDiscoveryConfig.MaximumHookStatusMessageCharacters
                && StatusMessage.IndexOf('\0') < 0
                && Enum.IsDefined(ExecutionMode)
                && Order >= 0
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
                || value.IndexOf('\0') >= 0)
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
        internal const int MaximumHookTimeoutSeconds = 30;
        private const int DefaultHookTimeoutSeconds = 5;
        private const string HooksFileName = "hooks.json";

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
                if (definitions.Count >= MaximumConfiguredHookHandlers)
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
                    if (definitions.Count >= MaximumConfiguredHookHandlers)
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

            var command = ReadOptionalString(handler, "commandWindows");
            if (command.Length == 0)
                command = ReadOptionalString(handler, "command_windows");
            if (command.Length == 0)
                command = ReadOptionalString(handler, "command");
            if (command.Length == 0
                || command.Length > MaximumHookCommandCharacters
                || command.IndexOf('\0') >= 0)
            {
                error = $"Hook event '{hookEvent}' contains an empty or oversized command handler.";
                return false;
            }

            var timeoutSeconds = DefaultHookTimeoutSeconds;
            if (handler.TryGetProperty("timeout", out var timeoutElement)
                && (timeoutElement.ValueKind != JsonValueKind.Number
                    || !timeoutElement.TryGetInt32(out timeoutSeconds)
                    || timeoutSeconds is < 1 or > MaximumHookTimeoutSeconds))
            {
                error = $"Hook event '{hookEvent}' command timeout must be between 1 and {MaximumHookTimeoutSeconds} seconds.";
                return false;
            }

            var isAsync = handler.TryGetProperty("async", out var asyncElement)
                && asyncElement.ValueKind == JsonValueKind.True;
            if (handler.TryGetProperty("async", out asyncElement)
                && asyncElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = $"Hook event '{hookEvent}' command async flag must be a boolean.";
                return false;
            }

            var statusMessage = ReadOptionalString(handler, "statusMessage");
            if (statusMessage.Length > MaximumHookStatusMessageCharacters
                || statusMessage.IndexOf('\0') >= 0)
            {
                error = $"Hook event '{hookEvent}' statusMessage is invalid or oversized.";
                return false;
            }

            var executionMode = isAsync
                ? CopilotToolExecutionHookMode.Async
                : CopilotToolExecutionHookMode.Sync;
            var fingerprint = ComputeHookFingerprint(
                sourceFilePath,
                source,
                hookEvent,
                matcher,
                command,
                timeoutSeconds,
                statusMessage,
                executionMode,
                order);
            definition = new CopilotCodexCommandHookDefinition(
                "codex-config:" + fingerprint[..32],
                sourceFilePath,
                source,
                hookEvent,
                matcher,
                command,
                timeoutSeconds,
                statusMessage,
                executionMode,
                order,
                fingerprint);
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
                _ => default,
            };
            return value is "PermissionRequest" or "PreToolUse" or "PostToolUse";
        }

        private static string ComputeHookFingerprint(
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            CopilotCodexConfiguredHookEvent hookEvent,
            string matcher,
            string command,
            int timeoutSeconds,
            string statusMessage,
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
