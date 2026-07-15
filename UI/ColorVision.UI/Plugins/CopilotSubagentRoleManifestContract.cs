using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Plugins
{
    public sealed class CopilotSubagentRoleManifestContract
    {
        public string Id { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Instructions { get; init; } = string.Empty;

        public string Scope { get; init; } = string.Empty;

        public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

        public string ChildMode { get; init; } = string.Empty;

        public IReadOnlyList<string> ParentModes { get; init; } = Array.Empty<string>();

        public int MaximumToolCalls { get; init; }

        public int MaximumAgentPasses { get; init; }

        public int MaximumDurationSeconds { get; init; }

        public int MaximumAnswerCharacters { get; init; }

        public int AdvertisedCharacters => ToolName.Length + DisplayName.Length + Description.Length;
    }

    public static class CopilotSubagentRoleManifestValidator
    {
        public const int MaximumRolesPerPlugin = 16;
        public const int MaximumAdvertisedCharactersPerPlugin = 8_000;
        public const int DefaultMaximumToolCalls = 6;
        public const int DefaultMaximumAgentPasses = 2;
        public const int DefaultMaximumDurationSeconds = 90;
        public const int DefaultMaximumAnswerCharacters = 12_000;

        private const int MaximumDescriptionCharacters = 1_200;
        private const int MaximumInstructionCharacters = 8_000;
        private static readonly Regex SourceIdRegex = new("^[a-z][a-z0-9._-]{1,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex SourceVersionRegex = new("^[A-Za-z0-9][A-Za-z0-9._+-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RoleIdRegex = new("^[a-z][a-z0-9-]{1,47}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ToolNameRegex = new("^Delegate[A-Z][A-Za-z0-9]{1,55}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly string[] DefaultParentModes = ["Auto", "Explain", "Web", "Code", "Diagnose"];
        private static readonly HashSet<string> KnownModes = new(["auto", "chat", "explain", "web", "code", "diagnose"], StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> KnownCapabilities = new(StringComparer.Ordinal)
        {
            ["searchfiles"] = "SearchFiles",
            ["greptext"] = "GrepText",
            ["readlocalfile"] = "ReadLocalFile",
            ["listdirectory"] = "ListDirectory",
            ["websearch"] = "WebSearch",
            ["fetchurl"] = "FetchUrl",
        };

        public static void ValidatePluginSource(string? pluginId, string? pluginName, string? pluginVersion)
        {
            string sourceId = pluginId?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!SourceIdRegex.IsMatch(sourceId))
                throw new FormatException("Subagent source id must contain 2-64 lowercase ASCII letters, digits, '.', '_' or '-'.");
            if (string.Equals(sourceId, "builtin", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("The built-in subagent source id is reserved.");

            string sourceName = FirstNonEmpty(pluginName, pluginId);
            if (sourceName.Length > 120)
                throw new FormatException("Subagent source name must contain 1-120 characters.");

            string sourceVersion = FirstNonEmpty(pluginVersion, "0");
            if (!SourceVersionRegex.IsMatch(sourceVersion))
                throw new FormatException("Subagent source version must be a stable 1-64 character version identifier.");
        }

        public static CopilotSubagentRoleManifestContract Create(CopilotSubagentRoleManifest role)
        {
            ArgumentNullException.ThrowIfNull(role);

            string roleId = role.Id?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!RoleIdRegex.IsMatch(roleId))
                throw new FormatException("Subagent role id must contain 2-48 lowercase ASCII letters, digits or '-'.");

            string toolName = FirstNonEmpty(role.ToolName, CreateDefaultToolName(roleId));
            if (!ToolNameRegex.IsMatch(toolName))
                throw new FormatException("Subagent tool name must use the form DelegateName with ASCII letters or digits.");

            string displayName = FirstNonEmpty(role.Name, roleId);
            if (displayName.Length > 80)
                throw new FormatException("Subagent display name must contain 1-80 characters.");

            string description = role.Description?.Trim() ?? string.Empty;
            if (description.Length is < 1 or > MaximumDescriptionCharacters)
                throw new FormatException($"Subagent description must contain 1-{MaximumDescriptionCharacters} characters.");

            string instructions = role.Instructions?.Trim() ?? string.Empty;
            if (instructions.Length is < 1 or > MaximumInstructionCharacters)
                throw new FormatException($"Subagent runtime instructions must contain 1-{MaximumInstructionCharacters} characters.");

            bool isWorkspace = ParseWorkspaceScope(role.Scope);
            string[] capabilities = ParseCapabilities(role.Capabilities, isWorkspace);
            string childMode = ParseChildMode(role.ChildMode, isWorkspace);
            string[] parentModes = ParseParentModes(role.ParentModes);

            return new CopilotSubagentRoleManifestContract
            {
                Id = roleId,
                ToolName = toolName,
                DisplayName = displayName,
                Description = description,
                Instructions = instructions,
                Scope = isWorkspace ? "WorkspaceReadOnly" : "PublicWeb",
                Capabilities = capabilities,
                ChildMode = childMode,
                ParentModes = parentModes,
                MaximumToolCalls = ReadBudget(role.MaximumToolCalls, DefaultMaximumToolCalls, 1, 12),
                MaximumAgentPasses = ReadBudget(role.MaximumAgentPasses, DefaultMaximumAgentPasses, 1, 3),
                MaximumDurationSeconds = ReadBudget(role.MaximumDurationSeconds, DefaultMaximumDurationSeconds, 10, 120),
                MaximumAnswerCharacters = ReadBudget(role.MaximumAnswerCharacters, DefaultMaximumAnswerCharacters, 1_000, 20_000),
            };
        }

        private static bool ParseWorkspaceScope(string? value)
        {
            return NormalizeToken(value) switch
            {
                "workspace" or "workspacereadonly" => true,
                "web" or "publicweb" => false,
                _ => throw new FormatException("Subagent scope must be WorkspaceReadOnly or PublicWeb."),
            };
        }

        private static string[] ParseCapabilities(List<string>? values, bool isWorkspace)
        {
            var capabilities = new List<string>();
            foreach (string value in values ?? [])
            {
                string normalized = NormalizeToken(value);
                if (!KnownCapabilities.TryGetValue(normalized, out string? canonical))
                    throw new FormatException($"Unknown subagent read capability '{value}'.");
                if (!capabilities.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                    capabilities.Add(canonical);
            }

            if (capabilities.Count == 0)
                throw new FormatException("Subagent read capabilities must contain at least one supported value.");

            bool hasWorkspace = capabilities.Any(value => value is "SearchFiles" or "GrepText" or "ReadLocalFile" or "ListDirectory");
            bool hasWeb = capabilities.Any(value => value is "WebSearch" or "FetchUrl");
            if (hasWorkspace && hasWeb)
                throw new FormatException("A subagent role cannot mix local workspace and public web capabilities.");
            if (isWorkspace && (!hasWorkspace || hasWeb))
                throw new FormatException("A workspace subagent must use only workspace read capabilities.");
            if (!isWorkspace && (!hasWeb || hasWorkspace))
                throw new FormatException("A public-web subagent must use only public web capabilities.");
            return capabilities.ToArray();
        }

        private static string ParseChildMode(string? value, bool isWorkspace)
        {
            string childMode = string.IsNullOrWhiteSpace(value) ? (isWorkspace ? "Code" : "Web") : CanonicalMode(value);
            if (isWorkspace && childMode is "Chat" or "Web")
                throw new FormatException("A workspace subagent cannot run in Chat or Web mode.");
            if (!isWorkspace && childMode != "Web")
                throw new FormatException("A public-web subagent must run in Web mode.");
            return childMode;
        }

        private static string[] ParseParentModes(List<string>? values)
        {
            string[] parentModes = values == null || values.Count == 0
                ? DefaultParentModes.ToArray()
                : values.Select(CanonicalMode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (parentModes.Length == 0 || parentModes.Contains("Chat", StringComparer.OrdinalIgnoreCase))
                throw new FormatException("Subagent parent modes must contain one or more defined non-Chat modes.");
            return parentModes;
        }

        private static string CanonicalMode(string? value)
        {
            string mode = value?.Trim() ?? string.Empty;
            if (!KnownModes.Contains(mode))
                throw new FormatException($"Unknown subagent Agent mode '{value}'.");
            return char.ToUpperInvariant(mode[0]) + mode[1..].ToLowerInvariant();
        }

        private static int ReadBudget(int value, int defaultValue, int minimum, int maximum)
        {
            int result = value == 0 ? defaultValue : value;
            if (result < minimum || result > maximum)
                throw new FormatException("Subagent budgets exceed the host-controlled safety bounds.");
            return result;
        }

        private static string CreateDefaultToolName(string roleId)
        {
            var builder = new StringBuilder("Delegate");
            foreach (string segment in roleId.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Append(char.ToUpperInvariant(segment[0]));
                if (segment.Length > 1)
                    builder.Append(segment.AsSpan(1));
            }
            return builder.ToString();
        }

        private static string NormalizeToken(string? value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }
    }
}
