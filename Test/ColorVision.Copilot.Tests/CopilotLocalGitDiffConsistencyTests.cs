using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalGitDiffConsistencyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("CopilotLocalDiffConsistency-").FullName;
    private readonly string _gitExecutable;

    public CopilotLocalGitDiffConsistencyTests()
    {
        _gitExecutable = Path.Combine(_root, "git.exe");
        File.WriteAllText(_gitExecutable, string.Empty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusOutputTruncationUsesLowerBoundCountsWhileTheDisplayLimitKeepsExactCounts(bool processTruncated)
    {
        var repository = CreateRepository("repository");
        var count = processTruncated ? 1 : CopilotGitWorkingTreeInspectionService.MaxEntries + 1;
        var porcelain = "# branch.oid " + new string('a', 40) + "\n# branch.head fixture-branch\n"
            + string.Join('\n', Enumerable.Range(0, count).Select(index => $"? item-{index:D3}.txt")) + "\n"
            + (processTruncated ? "...<shell output truncated>...\n" : string.Empty);
        var statusRunner = new FakeRunner(_ => Success(porcelain) with { StandardOutputTruncated = processTruncated });
        var statusService = new CopilotGitWorkingTreeInspectionService(statusRunner, () => _gitExecutable);
        var diffService = new CopilotGitDiffInspectionService(new FakeRunner(_ => Success(string.Empty), _ => Success(string.Empty)),
            () => _gitExecutable);
        CopilotToolResult? statusResult = null;
        var local = new CopilotLocalGitDiffService(async (request, input, token) =>
        {
            statusResult = await statusService.ExecuteAsync(request, input, token);
            return statusResult;
        }, diffService.ExecuteAsync);

        var result = await local.ExecuteAsync([repository], "staged", CancellationToken.None);

        Assert.NotNull(statusResult);
        Assert.True(statusResult.Success, statusResult.ErrorMessage);
        using var payload = StatusPayload(statusResult);
        Assert.Equal(!processTruncated, payload.RootElement.GetProperty("status_complete").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("entries_truncated").GetBoolean());
        Assert.Equal(count, payload.RootElement.GetProperty("changed_path_count").GetInt32());
        Assert.Equal(Math.Min(count, CopilotGitWorkingTreeInspectionService.MaxEntries),
            payload.RootElement.GetProperty("entries").GetArrayLength());
        Assert.True(result.Success, result.Report);
        if (processTruncated)
        {
            Assert.Contains("至少", result.Report, StringComparison.Ordinal);
            Assert.Contains("不完整", result.Report, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"变更：{count} 个路径", result.Report, StringComparison.Ordinal);
            Assert.Contains("列表已截断", result.Report, StringComparison.Ordinal);
            Assert.DoesNotContain("至少", result.Report, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ADisappearingFirstRootCannotCombineItsStatusWithTheNextRepositoryPatch()
    {
        var first = CreateRepository("first");
        var second = CreateRepository("second");
        File.WriteAllText(Path.Combine(first, "first.txt"), "first repository");
        File.WriteAllText(Path.Combine(second, "second.txt"), "foreign patch");
        var moved = Path.GetFullPath(Path.Combine(_root, "first-removed"));
        var statusService = new CopilotGitWorkingTreeInspectionService(new FakeRunner(command =>
        {
            Assert.Equal(first, command.WorkingDirectory);
            Assert.Equal(_root, Path.GetDirectoryName(Path.GetFullPath(first)));
            Assert.Equal(_root, Path.GetDirectoryName(moved));
            Directory.Move(first, moved);
            return Success("# branch.oid " + new string('a', 40) + "\n# branch.head first-branch\n? first.txt\n");
        }), () => _gitExecutable);
        var diffRunner = new FakeRunner(command =>
        {
            Assert.Equal(second, command.WorkingDirectory);
            return Success("diff --git a/second.txt b/second.txt\n--- a/second.txt\n+++ b/second.txt\n@@ -1 +1 @@\n-before\n+foreign patch\n");
        }, command =>
        {
            Assert.Equal(second, command.WorkingDirectory);
            return Success("second.txt\0");
        });
        var diffService = new CopilotGitDiffInspectionService(diffRunner, () => _gitExecutable);
        CopilotToolResult? statusResult = null;
        CopilotToolResult? diffResult = null;
        var local = new CopilotLocalGitDiffService(async (request, input, token) =>
        {
            statusResult = await statusService.ExecuteAsync(request, input, token);
            return statusResult;
        }, async (request, input, token) =>
        {
            diffResult = await diffService.ExecuteAsync(request, input, token);
            return diffResult;
        });

        var result = await local.ExecuteAsync([first, second], "staged", CancellationToken.None);

        Assert.NotNull(statusResult);
        Assert.NotNull(diffResult);
        Assert.True(statusResult.Success, statusResult.ErrorMessage);
        using var status = StatusPayload(statusResult);
        Assert.Equal(first, status.RootElement.GetProperty("repository_root").GetString());
        Assert.False(diffResult.Success);
        Assert.Equal(0, diffRunner.CallCount);
        Assert.False(result.Success);
        Assert.DoesNotContain("foreign patch", result.Report, StringComparison.Ordinal);
    }

    private string CreateRepository(string name)
    {
        var repository = Path.GetFullPath(Path.Combine(_root, name));
        Directory.CreateDirectory(Path.Combine(repository, ".git"));
        return repository;
    }

    private static JsonDocument StatusPayload(CopilotToolResult result)
    {
        const string marker = "result_json: ";
        var index = result.Content.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0);
        return JsonDocument.Parse(result.Content[(index + marker.Length)..]);
    }

    private static CopilotShellProcessResult Success(string output) =>
        new(0, false, output, string.Empty, TimeSpan.FromMilliseconds(1));

    private sealed class FakeRunner(params Func<CopilotShellProcessCommand, CopilotShellProcessResult>[] steps)
        : ICopilotShellProcessRunner
    {
        private readonly Queue<Func<CopilotShellProcessCommand, CopilotShellProcessResult>> _steps = new(steps);
        public int CallCount { get; private set; }
        public Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_steps.Dequeue()(command));
        }
    }

    public void Dispose()
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(root).StartsWith("CopilotLocalDiffConsistency-", StringComparison.Ordinal));
        Directory.Delete(root, recursive: true);
    }
}
