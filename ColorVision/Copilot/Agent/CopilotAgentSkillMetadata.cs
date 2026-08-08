using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal sealed record CopilotAgentSkillDependency(string Type, string Value, string Description);

    internal sealed class CopilotAgentSkillMetadata
    {
        private const int MaxMetadataFileBytes = 65_536;
        private const int MaxDisplayNameCharacters = 80;
        private const int MaxDescriptionCharacters = 240;
        private const int MaxDefaultPromptCharacters = 512;
        private const int MaxDependencyCharacters = 512;
        private const int MaxDependencies = 16;

        public static CopilotAgentSkillMetadata Empty { get; } = new();

        public string DisplayName { get; init; } = string.Empty;

        public string ShortDescription { get; init; } = string.Empty;

        public string DefaultPrompt { get; init; } = string.Empty;

        public bool? AllowImplicitInvocation { get; init; }

        public IReadOnlyList<CopilotAgentSkillDependency> Dependencies { get; init; } = Array.Empty<CopilotAgentSkillDependency>();

        public static CopilotAgentSkillMetadata Read(string? skillDirectoryPath)
        {
            if (!TryResolveSkillDirectory(skillDirectoryPath, out var skillDirectory))
                return Empty;

            var json = TryReadJson(skillDirectory);
            var yaml = TryReadYaml(skillDirectory);
            if (json == null && yaml == null)
                return Empty;
            if (json == null)
                return yaml!;
            if (yaml == null)
                return json;

            return new CopilotAgentSkillMetadata
            {
                DisplayName = FirstNonEmpty(yaml.DisplayName, json.DisplayName),
                ShortDescription = FirstNonEmpty(yaml.ShortDescription, json.ShortDescription),
                DefaultPrompt = FirstNonEmpty(yaml.DefaultPrompt, json.DefaultPrompt),
                AllowImplicitInvocation = yaml.AllowImplicitInvocation ?? json.AllowImplicitInvocation,
                Dependencies = yaml.Dependencies.Count > 0 ? yaml.Dependencies : json.Dependencies,
            };
        }

        private static CopilotAgentSkillMetadata? TryReadJson(string skillDirectoryPath)
        {
            var filePath = Path.Combine(skillDirectoryPath, "SKILL.json");
            if (!TryReadMetadataFile(skillDirectoryPath, filePath, out var content))
                return null;

            try
            {
                using var document = JsonDocument.Parse(content, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = 16,
                });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return null;

                var displayName = string.Empty;
                var shortDescription = string.Empty;
                var defaultPrompt = string.Empty;
                if (TryGetObject(document.RootElement, "interface", out var interfaceElement))
                {
                    displayName = ReadString(interfaceElement, "displayName", "display_name", MaxDisplayNameCharacters);
                    shortDescription = ReadString(interfaceElement, "shortDescription", "short_description", MaxDescriptionCharacters);
                    defaultPrompt = ReadString(interfaceElement, "defaultPrompt", "default_prompt", MaxDefaultPromptCharacters);
                }

                return new CopilotAgentSkillMetadata
                {
                    DisplayName = displayName,
                    ShortDescription = shortDescription,
                    DefaultPrompt = defaultPrompt,
                    Dependencies = ReadJsonDependencies(document.RootElement),
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static CopilotAgentSkillMetadata? TryReadYaml(string skillDirectoryPath)
        {
            var agentsDirectoryPath = Path.Combine(skillDirectoryPath, "agents");
            if (!IsSafeDirectory(agentsDirectoryPath))
                return null;
            var filePath = Path.Combine(agentsDirectoryPath, "openai.yaml");
            if (!TryReadMetadataFile(skillDirectoryPath, filePath, out var content))
                return null;

            var displayName = string.Empty;
            var shortDescription = string.Empty;
            var defaultPrompt = string.Empty;
            bool? allowImplicitInvocation = null;
            var dependencies = new List<CopilotAgentSkillDependency>();
            string section = string.Empty;
            var inTools = false;
            string dependencyType = string.Empty;
            string dependencyValue = string.Empty;
            string dependencyDescription = string.Empty;

            void CommitDependency()
            {
                if (dependencies.Count < MaxDependencies && dependencyType.Length > 0 && dependencyValue.Length > 0)
                    dependencies.Add(new CopilotAgentSkillDependency(dependencyType, dependencyValue, dependencyDescription));
                dependencyType = string.Empty;
                dependencyValue = string.Empty;
                dependencyDescription = string.Empty;
            }

            foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var uncommentedLine = StripYamlComment(rawLine);
                var trimmedStart = uncommentedLine.TrimStart();
                if (trimmedStart.Length == 0 || trimmedStart.StartsWith('#'))
                    continue;
                var indentation = uncommentedLine.Length - trimmedStart.Length;
                var normalizedLine = trimmedStart.TrimEnd();
                if (indentation == 0)
                {
                    CommitDependency();
                    section = normalizedLine.EndsWith(':') ? normalizedLine[..^1].Trim().ToLowerInvariant() : string.Empty;
                    inTools = false;
                    continue;
                }

                if (section == "interface" && TryReadYamlScalar(normalizedLine, out var key, out var value))
                {
                    switch (key)
                    {
                        case "display_name":
                        case "displayname":
                            displayName = NormalizeText(value, MaxDisplayNameCharacters);
                            break;
                        case "short_description":
                        case "shortdescription":
                            shortDescription = NormalizeText(value, MaxDescriptionCharacters);
                            break;
                        case "default_prompt":
                        case "defaultprompt":
                            defaultPrompt = NormalizeText(value, MaxDefaultPromptCharacters);
                            break;
                    }
                    continue;
                }

                if (section == "policy" && TryReadYamlScalar(normalizedLine, out key, out value)
                    && key == "allow_implicit_invocation"
                    && bool.TryParse(value, out var parsedPolicy))
                {
                    allowImplicitInvocation = parsedPolicy;
                    continue;
                }

                if (section != "dependencies")
                    continue;
                if (string.Equals(normalizedLine, "tools:", StringComparison.OrdinalIgnoreCase))
                {
                    inTools = true;
                    continue;
                }
                if (!inTools)
                    continue;

                var dependencyLine = normalizedLine;
                if (dependencyLine.StartsWith('-'))
                {
                    CommitDependency();
                    dependencyLine = dependencyLine[1..].TrimStart();
                }
                if (!TryReadYamlScalar(dependencyLine, out key, out value))
                    continue;
                switch (key)
                {
                    case "type":
                        dependencyType = NormalizeText(value, MaxDependencyCharacters);
                        break;
                    case "value":
                        dependencyValue = NormalizeText(value, MaxDependencyCharacters);
                        break;
                    case "description":
                        dependencyDescription = NormalizeText(value, MaxDescriptionCharacters);
                        break;
                }
            }
            CommitDependency();

            return new CopilotAgentSkillMetadata
            {
                DisplayName = displayName,
                ShortDescription = shortDescription,
                DefaultPrompt = defaultPrompt,
                AllowImplicitInvocation = allowImplicitInvocation,
                Dependencies = dependencies.ToArray(),
            };
        }

        private static IReadOnlyList<CopilotAgentSkillDependency> ReadJsonDependencies(JsonElement root)
        {
            if (!TryGetObject(root, "dependencies", out var dependenciesElement)
                || !dependenciesElement.TryGetProperty("tools", out var toolsElement)
                || toolsElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CopilotAgentSkillDependency>();
            }

            var dependencies = new List<CopilotAgentSkillDependency>();
            foreach (var item in toolsElement.EnumerateArray().Take(MaxDependencies))
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                var type = ReadString(item, "type", "type", MaxDependencyCharacters);
                var value = ReadString(item, "value", "value", MaxDependencyCharacters);
                if (type.Length == 0 || value.Length == 0)
                    continue;
                dependencies.Add(new CopilotAgentSkillDependency(
                    type,
                    value,
                    ReadString(item, "description", "description", MaxDescriptionCharacters)));
            }
            return dependencies;
        }

        private static bool TryResolveSkillDirectory(string? path, out string skillDirectoryPath)
        {
            skillDirectoryPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                return false;
            try
            {
                skillDirectoryPath = Path.GetFullPath(path);
                return IsSafeDirectory(skillDirectoryPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadMetadataFile(string skillDirectoryPath, string filePath, out string content)
        {
            content = string.Empty;
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!IsDescendantPath(skillDirectoryPath, fullPath))
                    return false;
                var file = new FileInfo(fullPath);
                if (!file.Exists || file.Length <= 0 || file.Length > MaxMetadataFileBytes || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                content = reader.ReadToEnd();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadYamlScalar(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                return false;
            key = line[..separatorIndex].Trim().ToLowerInvariant();
            value = Unquote(line[(separatorIndex + 1)..].Trim());
            return key.Length > 0;
        }

        private static string ReadString(JsonElement element, string primaryName, string fallbackName, int maximumCharacters)
        {
            if ((!element.TryGetProperty(primaryName, out var property)
                    && !element.TryGetProperty(fallbackName, out property))
                || property.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }
            return NormalizeText(property.GetString(), maximumCharacters);
        }

        private static bool TryGetObject(JsonElement element, string name, out JsonElement value)
        {
            return element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"')
                return value[1..^1].Trim();
            return value;
        }

        private static string StripYamlComment(string line)
        {
            var singleQuoted = false;
            var doubleQuoted = false;
            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character == '\'' && !doubleQuoted)
                    singleQuoted = !singleQuoted;
                else if (character == '"' && !singleQuoted && (index == 0 || line[index - 1] != '\\'))
                    doubleQuoted = !doubleQuoted;
                else if (character == '#' && !singleQuoted && !doubleQuoted
                    && (index == 0 || char.IsWhiteSpace(line[index - 1])))
                {
                    return line[..index].TrimEnd();
                }
            }
            return line;
        }

        private static string NormalizeText(string? value, int maximumCharacters)
        {
            var normalized = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length <= maximumCharacters)
                return normalized;
            return normalized[..(maximumCharacters - 1)].TrimEnd() + "…";
        }

        private static string FirstNonEmpty(string primary, string fallback) =>
            string.IsNullOrWhiteSpace(primary) ? fallback : primary;

        private static bool IsSafeDirectory(string path)
        {
            try
            {
                return Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDescendantPath(string parentPath, string candidatePath)
        {
            var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentPath));
            var normalizedCandidate = Path.GetFullPath(candidatePath);
            return normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
