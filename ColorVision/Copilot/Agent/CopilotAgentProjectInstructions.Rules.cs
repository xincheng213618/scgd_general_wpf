using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    public static partial class CopilotAgentProjectInstructions
    {
        private static List<RuleInstructionFile> DiscoverRuleFiles(
            string rootPath,
            IReadOnlyList<string> targetFilePaths)
        {
            var targetRelativePaths = targetFilePaths
                .Where(path => IsPathWithin(path, rootPath))
                .Select(path => Path.GetRelativePath(rootPath, path).Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var rules = new List<RuleInstructionFile>();
            foreach (var path in EnumerateSafeRuleFiles(rootPath))
            {
                if (!TryReadRuleFrontmatter(path, out var frontmatter))
                    continue;

                var isPathScoped = frontmatter.PathPatterns.Count > 0;
                if (isPathScoped
                    && !targetRelativePaths.Any(relativePath =>
                        frontmatter.PathPatterns.Any(pattern => IsGlobMatch(pattern, relativePath))))
                {
                    continue;
                }

                rules.Add(new RuleInstructionFile(path, frontmatter.HasFrontmatter, isPathScoped));
            }
            return rules;
        }

        private static string[] EnumerateSafeRuleFiles(string rootPath)
        {
            var normalizedRoot = Path.GetFullPath(rootPath);
            var rulesRoot = Path.GetFullPath(Path.Combine(normalizedRoot, ".claude", "rules"));
            if (!Directory.Exists(rulesRoot)
                || !IsPathWithin(rulesRoot, normalizedRoot)
                || !IsSafeDirectoryChain(normalizedRoot, rulesRoot))
            {
                return Array.Empty<string>();
            }

            var files = new List<string>();
            var pendingDirectories = new Queue<string>();
            pendingDirectories.Enqueue(rulesRoot);
            var visitedDirectories = 0;
            while (pendingDirectories.Count > 0
                && visitedDirectories < MaxRuleDirectories
                && files.Count < MaxRuleFiles)
            {
                var directory = pendingDirectories.Dequeue();
                visitedDirectories++;
                if (!IsSafeDirectoryChain(normalizedRoot, directory))
                    continue;

                string[] entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(directory)
                        .Take(MaxRuleEntriesPerDirectory)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entry);
                    }
                    catch
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (visitedDirectories + pendingDirectories.Count < MaxRuleDirectories
                            && IsPathWithin(entry, rulesRoot))
                        {
                            pendingDirectories.Enqueue(Path.GetFullPath(entry));
                        }
                        continue;
                    }

                    if (!string.Equals(Path.GetExtension(entry), ".md", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var fullPath = Path.GetFullPath(entry);
                    if (IsSafeInstructionFile(normalizedRoot, fullPath))
                        files.Add(fullPath);
                    if (files.Count >= MaxRuleFiles)
                        break;
                }
            }

            return files
                .OrderBy(path => Path.GetRelativePath(rulesRoot, path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool TryReadRuleFrontmatter(
            string path,
            out RuleFrontmatter frontmatter)
        {
            frontmatter = new RuleFrontmatter(false, Array.Empty<string>());
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                var buffer = new char[MaxRuleFrontmatterCharacters + 1];
                var count = reader.ReadBlock(buffer, 0, buffer.Length);
                var value = new string(buffer, 0, Math.Min(count, MaxRuleFrontmatterCharacters))
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n');
                var lines = value.Split('\n');
                if (lines.Length == 0 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
                    return true;

                var closingIndex = Array.FindIndex(
                    lines,
                    1,
                    line => string.Equals(line.Trim(), "---", StringComparison.Ordinal));
                if (closingIndex < 0)
                    return false;

                var patterns = new List<string>();
                var pathsFound = false;
                for (var index = 1; index < closingIndex; index++)
                {
                    var line = lines[index];
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                        continue;
                    var separatorIndex = trimmed.IndexOf(':');
                    var isPathsKey = separatorIndex >= 0
                        && string.Equals(trimmed[..separatorIndex].Trim(), "paths", StringComparison.OrdinalIgnoreCase);
                    if (!isPathsKey)
                        continue;
                    if (GetIndentation(line) != 0)
                        return false;
                    if (pathsFound)
                        return false;

                    pathsFound = true;
                    var inlineValue = trimmed[(separatorIndex + 1)..].Trim();
                    if (inlineValue.Length > 0)
                    {
                        if (!TryParseInlineRulePatterns(inlineValue, patterns))
                            return false;
                        continue;
                    }

                    for (var itemIndex = index + 1; itemIndex < closingIndex; itemIndex++)
                    {
                        var itemLine = lines[itemIndex];
                        var item = itemLine.Trim();
                        if (item.Length == 0 || item.StartsWith('#'))
                            continue;
                        if (GetIndentation(itemLine) == 0)
                            break;
                        if (!item.StartsWith('-'))
                            return false;
                        if (!TryAddRulePattern(item[1..], patterns))
                            return false;
                        index = itemIndex;
                    }
                }

                if (pathsFound && patterns.Count == 0)
                    return false;
                frontmatter = new RuleFrontmatter(true, patterns.ToArray());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseInlineRulePatterns(string value, List<string> patterns)
        {
            if (!value.StartsWith('['))
                return TryAddRulePattern(value, patterns);
            if (!value.EndsWith(']'))
                return false;

            var item = new StringBuilder();
            char quote = default;
            var braceDepth = 0;
            foreach (var character in value[1..^1])
            {
                if (quote != default)
                {
                    item.Append(character);
                    if (character == quote)
                        quote = default;
                    continue;
                }

                if (character is '\'' or '"')
                {
                    quote = character;
                    item.Append(character);
                    continue;
                }
                if (character == '{')
                    braceDepth++;
                else if (character == '}')
                    braceDepth--;
                if (braceDepth < 0)
                    return false;
                if (character == ',' && braceDepth == 0)
                {
                    if (!TryAddRulePattern(item.ToString(), patterns))
                        return false;
                    item.Clear();
                    continue;
                }
                item.Append(character);
            }

            return quote == default
                && braceDepth == 0
                && TryAddRulePattern(item.ToString(), patterns);
        }

        private static bool TryAddRulePattern(string value, List<string> patterns)
        {
            if (patterns.Count >= MaxRulePathPatterns)
                return false;
            if (!TryNormalizeRulePattern(value, out var pattern))
                return false;
            if (!patterns.Contains(pattern, StringComparer.OrdinalIgnoreCase))
                patterns.Add(pattern);
            return true;
        }

        private static bool TryNormalizeRulePattern(string value, out string pattern)
        {
            pattern = string.Empty;
            var normalized = value.Trim();
            try
            {
                if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
                    normalized = JsonSerializer.Deserialize<string>(normalized) ?? string.Empty;
                else if (normalized.Length >= 2 && normalized[0] == '\'' && normalized[^1] == '\'')
                    normalized = normalized[1..^1].Replace("''", "'", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }

            normalized = normalized.Trim().Replace('\\', '/');
            if (normalized.Length == 0
                || normalized.Length > MaxRulePathPatternCharacters
                || normalized.StartsWith('/')
                || normalized.StartsWith('!')
                || normalized.Contains(':')
                || normalized.Any(char.IsControl)
                || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            {
                return false;
            }

            pattern = normalized;
            return true;
        }

        private static bool IsGlobMatch(string pattern, string relativePath)
        {
            if (!TryBuildGlobRegex(pattern, out var regex))
                return false;
            try
            {
                return Regex.IsMatch(
                    relativePath.Replace('\\', '/'),
                    regex,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(50));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private static bool TryBuildGlobRegex(string pattern, out string regex)
        {
            var builder = new StringBuilder(pattern.Length * 2).Append('^');
            for (var index = 0; index < pattern.Length; index++)
            {
                var character = pattern[index];
                if (character == '*')
                {
                    if (index + 1 < pattern.Length && pattern[index + 1] == '*')
                    {
                        index++;
                        if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                        {
                            index++;
                            builder.Append("(?:.*/)?");
                        }
                        else
                        {
                            builder.Append(".*");
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }
                    continue;
                }
                if (character == '?')
                {
                    builder.Append("[^/]");
                    continue;
                }
                if (character == '{')
                {
                    var endIndex = pattern.IndexOf('}', index + 1);
                    if (endIndex < 0)
                    {
                        regex = string.Empty;
                        return false;
                    }
                    var alternatives = pattern[(index + 1)..endIndex].Split(',');
                    if (alternatives.Length < 2 || alternatives.Any(item => item.Length == 0 || item.ContainsAny('{', '}')))
                    {
                        regex = string.Empty;
                        return false;
                    }
                    builder.Append("(?:")
                        .Append(string.Join('|', alternatives.Select(Regex.Escape)))
                        .Append(')');
                    index = endIndex;
                    continue;
                }
                if (character == '}')
                {
                    regex = string.Empty;
                    return false;
                }
                builder.Append(Regex.Escape(character.ToString()));
            }

            regex = builder.Append('$').ToString();
            return true;
        }

    }
}
