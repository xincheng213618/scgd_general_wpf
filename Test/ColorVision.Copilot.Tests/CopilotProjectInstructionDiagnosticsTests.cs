using ColorVision.Copilot;
using System.Globalization;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProjectInstructionDiagnosticsTests
{
    [Fact]
    public void MemoryPreviewReportsOrderedSourcesAndTargetsWithoutInstructionBodiesOrRetiredSettings()
    {
        using var fixture = new InstructionWorkspaceFixture();
        var workspace = fixture.WorkspacePath;
        var globalRoot = fixture.GlobalRootPath;
        CopilotProjectInstructionDocument[] documents =
        [
            new() { Path = Path.Combine(globalRoot, "AGENTS.md"), Content = "PRIVATE_GLOBAL_BODY" },
            new() { Path = Path.Combine(workspace, "AGENTS.override.md"), Content = "PRIVATE_PROJECT_BODY" },
            new() { Path = Path.Combine(workspace, ".claude", "rules", "cpp.md"), Content = "PRIVATE_RULE_BODY" },
            new() { Path = Path.Combine(workspace, "src", "CLAUDE.md"), Content = "PRIVATE_CLAUDE_BODY", IsTruncated = true },
            new() { Path = Path.Combine(workspace, "src", "CLAUDE.local.md"), Content = "PRIVATE_OVERLAY_BODY" },
        ];
        fixture.WriteDocuments(documents);
        var activeDocumentPath = fixture.WriteWorkspaceFile(Path.Combine("src", "main.cpp"), "int main() {}");
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault();

        var report = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                workspace, activeDocumentPath, globalRoot, options, documents),
            hasActiveAgentRun: true);

        Assert.Contains("个人指令根：" + globalRoot, report, StringComparison.Ordinal);
        Assert.Contains("项目根：" + workspace, report, StringComparison.Ordinal);
        Assert.Contains("活动目标：" + Path.Combine("src", "main.cpp"), report, StringComparison.Ordinal);
        Assert.Contains("Codex 全局指令", report, StringComparison.Ordinal);
        Assert.Contains("共享覆盖", report, StringComparison.Ordinal);
        Assert.Contains("Claude 路径规则", report, StringComparison.Ordinal);
        Assert.Contains("Claude 兼容指令", report, StringComparison.Ordinal);
        Assert.Contains("私有局部覆盖", report, StringComparison.Ordinal);
        Assert.Contains("已截断", report, StringComparison.Ordinal);
        Assert.Contains(options.MaximumBytes.ToString("N0", CultureInfo.CurrentCulture) + " UTF-8 字节", report, StringComparison.Ordinal);
        Assert.Contains("具有工作区补丁能力且存在可写目标", report, StringComparison.Ordinal);
        Assert.Contains("Chat 不注入", report, StringComparison.Ordinal);
        Assert.Contains("已固定请求启动时的指令快照", report, StringComparison.Ordinal);
        Assert.Contains("/memory open N", report, StringComparison.Ordinal);
        Assert.Contains("不读取 Codex Home 或项目的 config.toml", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex sandbox_mode", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex approvals_reviewer", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex features.", report, StringComparison.Ordinal);
        var previousPosition = -1;
        for (var index = 0; index < documents.Length; index++)
        {
            var position = report.IndexOf($"#{index + 1} · {Path.GetFileName(documents[index].Path)}", StringComparison.Ordinal);
            Assert.True(position > previousPosition);
            Assert.Same(documents[index], CopilotProjectInstructionDiagnostics.FindByPosition(documents, index + 1));
            Assert.DoesNotContain(documents[index].Content, report, StringComparison.Ordinal);
            previousPosition = position;
        }
    }

    [Fact]
    public void EmptyPreviewStillExplainsTheTargetBudgetAndRunningSnapshot()
    {
        using var fixture = new InstructionWorkspaceFixture();
        var workspace = fixture.WorkspacePath;
        var activeDocumentPath = fixture.WriteWorkspaceFile("main.cs", "class Program {}");
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault();

        var report = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                workspace, activeDocumentPath, string.Empty, options, []),
            hasActiveAgentRun: true);

        Assert.Contains("没有发现可加载的指令", report, StringComparison.Ordinal);
        Assert.Contains("项目根：" + workspace, report, StringComparison.Ordinal);
        Assert.Contains("活动目标：main.cs", report, StringComparison.Ordinal);
        Assert.Contains("发现预算：", report, StringComparison.Ordinal);
        Assert.Contains("项目根标记：", report, StringComparison.Ordinal);
        Assert.Contains("已固定请求启动时的指令快照", report, StringComparison.Ordinal);
        Assert.Contains("/init", report, StringComparison.Ordinal);
        Assert.DoesNotContain("#1", report, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenPositionsMatchTheBoundedPreviewAfterInvalidDocumentsAreRemoved()
    {
        using var fixture = new InstructionWorkspaceFixture();
        var workspace = fixture.WorkspacePath;
        var documents = Enumerable.Range(1, CopilotAgentProjectInstructions.MaxDocuments + 1)
            .Select(index => new CopilotProjectInstructionDocument
            {
                Path = Path.Combine(workspace, $"rule-{index}.md"),
                Content = "Instruction body " + index,
            })
            .Prepend(new CopilotProjectInstructionDocument { Path = Path.Combine(workspace, "empty.md") })
            .ToArray();
        fixture.WriteDocuments(documents);
        var effective = CopilotProjectInstructionDiagnostics.GetEffectiveDocuments(documents);

        var report = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                workspace, string.Empty, string.Empty, CopilotProjectInstructionDiscoveryConfig.CreateDefault(), documents),
            hasActiveAgentRun: false);

        Assert.Equal(CopilotAgentProjectInstructions.MaxDocuments, effective.Count);
        Assert.Same(documents[1], CopilotProjectInstructionDiagnostics.FindByPosition(documents, 1));
        Assert.Same(effective[^1], CopilotProjectInstructionDiagnostics.FindByPosition(documents, effective.Count));
        Assert.Null(CopilotProjectInstructionDiagnostics.FindByPosition(documents, 0));
        Assert.Null(CopilotProjectInstructionDiagnostics.FindByPosition(documents, effective.Count + 1));
        Assert.DoesNotContain("empty.md", report, StringComparison.Ordinal);
        Assert.DoesNotContain($"rule-{effective.Count + 1}.md", report, StringComparison.Ordinal);
        Assert.DoesNotContain("当前运行中的任务", report, StringComparison.Ordinal);
    }

    private sealed class InstructionWorkspaceFixture : IDisposable
    {
        private const string DirectoryPrefix = "copilot-memory-diagnostics-";
        private readonly string _rootPath = Directory.CreateTempSubdirectory(DirectoryPrefix).FullName;

        public InstructionWorkspaceFixture()
        {
            WorkspacePath = Directory.CreateDirectory(Path.Combine(_rootPath, "project")).FullName;
            GlobalRootPath = Directory.CreateDirectory(Path.Combine(_rootPath, "global")).FullName;
        }

        public string WorkspacePath { get; }
        public string GlobalRootPath { get; }

        public void WriteDocuments(IEnumerable<CopilotProjectInstructionDocument> documents)
        {
            foreach (var document in documents)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(document.Path)!);
                File.WriteAllText(document.Path, document.Content);
            }
        }

        public string WriteWorkspaceFile(string relativePath, string content)
        {
            var path = Path.Combine(WorkspacePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_rootPath));
            var temporaryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(rootPath), temporaryPath, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(rootPath).StartsWith(DirectoryPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The instruction fixture must remain directly inside the temporary directory.");
            }

            Directory.Delete(rootPath, recursive: true);
        }
    }
}
