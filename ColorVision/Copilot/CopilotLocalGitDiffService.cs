using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed record CopilotLocalGitDiffResult(bool Success, string Report);

    public sealed class CopilotLocalGitDiffService
    {
        private const string ResultJsonMarker = "result_json: ";
        private readonly Func<CopilotAgentRequest, CopilotAgentToolInput, CancellationToken, Task<CopilotToolResult>> _inspectStatus;
        private readonly Func<CopilotAgentRequest, CopilotAgentToolInput, CancellationToken, Task<CopilotToolResult>> _inspectDiff;

        public CopilotLocalGitDiffService()
        {
            var statusService = new CopilotGitWorkingTreeInspectionService();
            var diffService = new CopilotGitDiffInspectionService();
            _inspectStatus = statusService.ExecuteAsync;
            _inspectDiff = diffService.ExecuteAsync;
        }

        public CopilotLocalGitDiffService(
            Func<CopilotAgentRequest, CopilotAgentToolInput, CancellationToken, Task<CopilotToolResult>> inspectStatus,
            Func<CopilotAgentRequest, CopilotAgentToolInput, CancellationToken, Task<CopilotToolResult>> inspectDiff)
        {
            _inspectStatus = inspectStatus ?? throw new ArgumentNullException(nameof(inspectStatus));
            _inspectDiff = inspectDiff ?? throw new ArgumentNullException(nameof(inspectDiff));
        }

        public async Task<CopilotLocalGitDiffResult> ExecuteAsync(
            IReadOnlyList<string> searchRootPaths,
            string? requestedScope,
            CancellationToken cancellationToken)
        {
            var scope = string.IsNullOrWhiteSpace(requestedScope) ? "both" : requestedScope.Trim().ToLowerInvariant();
            if (scope is not ("both" or "staged" or "unstaged"))
                return new CopilotLocalGitDiffResult(false, "参数无效。用法：/diff [both|staged|unstaged]");

            var request = new CopilotAgentRequest
            {
                UserText = "/diff",
                Mode = CopilotAgentMode.Diagnose,
                SearchRootPaths = searchRootPaths ?? Array.Empty<string>(),
            };
            var statusResult = await _inspectStatus(request, CopilotAgentToolInput.Empty, cancellationToken);
            if (!statusResult.Success)
                return Failure(statusResult);

            var diffInput = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?> { ["scope"] = scope },
            };
            var diffResult = await _inspectDiff(request, diffInput, cancellationToken);
            if (!diffResult.Success)
                return Failure(diffResult);

            try
            {
                using var statusJson = ParseResultJson(statusResult.Content);
                using var diffJson = ParseResultJson(diffResult.Content);
                return new CopilotLocalGitDiffResult(true, FormatReport(statusJson.RootElement, diffJson.RootElement));
            }
            catch (JsonException ex)
            {
                return new CopilotLocalGitDiffResult(false, "Git 返回了无法解析的本地快照：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return new CopilotLocalGitDiffResult(false, CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
        }

        private static CopilotLocalGitDiffResult Failure(CopilotToolResult result)
        {
            var detail = string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.Summary : result.Summary + "\n" + result.ErrorMessage;
            return new CopilotLocalGitDiffResult(false, CopilotUserFacingErrorFormatter.Sanitize(detail));
        }

        private static JsonDocument ParseResultJson(string content)
        {
            var normalizedContent = content ?? string.Empty;
            var markerIndex = normalizedContent.IndexOf(ResultJsonMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
                throw new InvalidOperationException("Git 检查没有返回结构化结果。");
            return JsonDocument.Parse(normalizedContent[(markerIndex + ResultJsonMarker.Length)..]);
        }

        private static string FormatReport(JsonElement status, JsonElement diff)
        {
            var builder = new StringBuilder();
            var repositoryRoot = ReadString(status, "repository_root");
            var branch = ReadString(status, "branch");
            builder.AppendLine("仓库：" + (string.IsNullOrWhiteSpace(repositoryRoot) ? "未知" : repositoryRoot));
            builder.AppendLine("分支：" + (string.IsNullOrWhiteSpace(branch) ? "detached / unborn HEAD" : branch));
            builder.Append("变更：")
                .Append(ReadInt(status, "changed_path_count")).Append(" 个路径 · ")
                .Append(ReadInt(status, "staged_count")).Append(" 已暂存 · ")
                .Append(ReadInt(status, "unstaged_count")).Append(" 未暂存 · ")
                .Append(ReadInt(status, "untracked_count")).Append(" 未跟踪 · ")
                .Append(ReadInt(status, "conflict_count")).AppendLine(" 冲突");

            AppendUntrackedFiles(builder, status);
            AppendDiffSections(builder, diff);

            if (ReadBool(status, "entries_truncated"))
                builder.AppendLine().Append("…文件状态列表已截断。");
            if (ReadBool(diff, "patch_truncated"))
                builder.AppendLine().Append("…补丁内容已按本地显示上限截断，可运行 /review 进行更完整的审查。");

            return builder.ToString().TrimEnd();
        }

        private static void AppendUntrackedFiles(StringBuilder builder, JsonElement status)
        {
            if (!status.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            var untracked = entries.EnumerateArray()
                .Where(entry => ReadBool(entry, "is_untracked"))
                .Select(entry => ReadString(entry, "path"))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            if (untracked.Length == 0)
                return;

            builder.AppendLine().AppendLine("未跟踪文件");
            foreach (var path in untracked)
                builder.Append("  ?? ").AppendLine(path);
        }

        private static void AppendDiffSections(StringBuilder builder, JsonElement diff)
        {
            if (!diff.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
                return;

            var appendedPatch = false;
            foreach (var section in sections.EnumerateArray())
            {
                var patch = ReadString(section, "patch").TrimEnd();
                if (string.IsNullOrWhiteSpace(patch))
                    continue;

                builder.AppendLine().AppendLine(ReadString(section, "scope") == "staged" ? "已暂存补丁" : "未暂存补丁");
                builder.AppendLine(patch);
                appendedPatch = true;
            }

            if (!appendedPatch)
                builder.AppendLine().Append("所选范围没有补丁。");
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : 0;
        }

        private static bool ReadBool(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property)
                && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                && property.GetBoolean();
        }
    }
}
