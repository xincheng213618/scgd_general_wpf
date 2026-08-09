using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotCodexProjectTrustPersistence
    {
        private const int MaximumConfigBytes = 256 * 1024;
        private static readonly object FileGate = new();

        internal static bool RequiresDecision(
            string? projectRootPath,
            CopilotProjectInstructionDiscoveryOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return !string.IsNullOrWhiteSpace(projectRootPath)
                && options.ProjectTrustLevel == CopilotCodexProjectTrustLevel.Unspecified;
        }

        internal static bool TryTrustProject(
            string? globalInstructionRootPath,
            string? projectRootPath,
            out string error)
        {
            error = string.Empty;
            var globalRoot = NormalizeExistingDirectory(globalInstructionRootPath);
            if (globalRoot.Length == 0)
            {
                error = "Codex Home 目录不可用，无法保存项目信任。";
                return false;
            }

            var projectRoot = NormalizeExistingDirectory(projectRootPath);
            if (projectRoot.Length == 0)
            {
                error = "项目根目录不可用，无法保存项目信任。";
                return false;
            }

            if (projectRoot.IndexOfAny(['\r', '\n', '\0']) >= 0)
            {
                error = "项目根目录包含不受支持的字符，无法保存项目信任。";
                return false;
            }

            lock (FileGate)
            {
                try
                {
                    var existing = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
                    if (existing.ProjectTrustLevel == CopilotCodexProjectTrustLevel.Trusted)
                        return true;
                    if (existing.ProjectTrustLevel != CopilotCodexProjectTrustLevel.Unspecified)
                    {
                        error = "项目已有显式或无效的 trust_level，未覆盖现有决定。";
                        return false;
                    }

                    var configPath = Path.Combine(globalRoot, "config.toml");
                    var source = ReadConfig(configPath);
                    if (!TryAddTrustedProject(source, projectRoot, out var updated, out error))
                        return false;
                    if (Encoding.UTF8.GetByteCount(updated) > MaximumConfigBytes)
                    {
                        error = "Codex config.toml 已达到大小限制，未修改项目信任。";
                        return false;
                    }

                    WriteAtomically(configPath, source, updated);
                    var verified = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
                    if (verified.ProjectTrustLevel != CopilotCodexProjectTrustLevel.Trusted)
                    {
                        error = "项目信任已写入，但重新加载后未生效。";
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    error = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
                    return false;
                }
            }
        }

        private static string ReadConfig(string configPath)
        {
            if (!File.Exists(configPath))
                return string.Empty;

            var file = new FileInfo(configPath);
            if (file.Length > MaximumConfigBytes
                || (file.Attributes & FileAttributes.ReparsePoint) != 0
                || CopilotWorkspaceSearchSupport.HasReparsePointInPath(configPath))
            {
                throw new IOException("Codex config.toml 过大或指向重解析路径，未修改项目信任。");
            }
            return File.ReadAllText(configPath, Encoding.UTF8);
        }

        private static bool TryAddTrustedProject(
            string source,
            string projectRoot,
            out string updated,
            out string error)
        {
            var newline = source.Contains("\r\n", StringComparison.Ordinal)
                ? "\r\n"
                : source.IndexOf('\n') >= 0
                    ? "\n"
                    : Environment.NewLine;
            var matchingTableLineEnds = new List<int>();
            foreach (var line in EnumerateLines(source))
            {
                var parsed = CopilotProjectInstructionDiscoveryConfig.StripComment(line.Text).Trim();
                if (CopilotProjectInstructionDiscoveryConfig.TryParseProjectTableHeader(
                        parsed,
                        out var configuredPath)
                    && string.Equals(configuredPath, projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    matchingTableLineEnds.Add(line.EndIncludingNewline);
                }
            }

            if (matchingTableLineEnds.Count > 1)
            {
                updated = source;
                error = "Codex config.toml 中存在重复的项目表，未修改项目信任。";
                return false;
            }

            if (matchingTableLineEnds.Count == 1)
            {
                var insertAt = matchingTableLineEnds[0];
                var separator = insertAt > 0 && source[insertAt - 1] is not ('\r' or '\n')
                    ? newline
                    : string.Empty;
                updated = source.Insert(insertAt, $"{separator}trust_level = \"trusted\"{newline}");
                error = string.Empty;
                return true;
            }

            var builder = new StringBuilder(source);
            if (builder.Length > 0 && builder[^1] != '\n' && builder[^1] != '\r')
                builder.Append(newline);
            if (builder.Length > 0 && !EndsWithBlankLine(builder))
                builder.Append(newline);
            builder.Append("[projects.\"")
                .Append(EscapeTomlBasicString(projectRoot))
                .Append("\"]")
                .Append(newline)
                .Append("trust_level = \"trusted\"")
                .Append(newline);
            updated = builder.ToString();
            error = string.Empty;
            return true;
        }

        private static void WriteAtomically(string configPath, string expectedContent, string content)
        {
            var temporaryPath = configPath + ".copilot." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(configPath))
                {
                    if (!string.Equals(
                        File.ReadAllText(configPath, Encoding.UTF8),
                        expectedContent,
                        StringComparison.Ordinal))
                    {
                        throw new IOException("Codex config.toml 在保存期间已被其他进程修改；未覆盖新内容。");
                    }
                    File.Replace(temporaryPath, configPath, destinationBackupFileName: null);
                }
                else
                {
                    if (expectedContent.Length > 0)
                        throw new IOException("Codex config.toml 在保存期间被移除；未重建文件。");
                    File.Move(temporaryPath, configPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static IEnumerable<(string Text, int EndIncludingNewline)> EnumerateLines(string source)
        {
            var index = 0;
            while (index < source.Length)
            {
                var lineStart = index;
                while (index < source.Length && source[index] is not ('\r' or '\n'))
                    index++;
                var lineEnd = index;
                if (index < source.Length && source[index] == '\r')
                    index++;
                if (index < source.Length && source[index] == '\n')
                    index++;
                yield return (source[lineStart..lineEnd], index);
            }
        }

        private static string NormalizeExistingDirectory(string? path)
        {
            var normalized = NormalizeConfiguredDirectory(path);
            return normalized.Length > 0
                && Directory.Exists(normalized)
                && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(normalized)
                    ? normalized
                    : string.Empty;
        }

        private static string NormalizeConfiguredDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return string.Empty;
            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathFullyQualified(trimmed))
                    return string.Empty;
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string EscapeTomlBasicString(string value) => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        private static bool EndsWithBlankLine(StringBuilder builder)
        {
            var index = builder.Length - 1;
            var lineFeedCount = 0;
            while (index >= 0)
            {
                if (builder[index] == '\n')
                    lineFeedCount++;
                else if (builder[index] is not ('\r' or ' ' or '\t'))
                    break;
                index--;
            }
            return lineFeedCount >= 2;
        }
    }
}
