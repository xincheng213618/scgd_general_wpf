using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotShellCommandOutputArchiveTests
{
    [Fact]
    public void DefaultToolCatalogContainsReadOnlyShellOutputReader()
    {
        var tool = Assert.Single(
            CopilotToolRegistry.CreateCoreDefaultTools(),
            candidate => candidate.Name == "ReadShellCommandOutput");

        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.False(tool.Capability.RequiresNativeApproval);
        Assert.Equal(
            CopilotToolAuditArgumentMode.NamesOnly,
            tool.Capability.AuditArgumentMode);
        Assert.Equal(
            CopilotToolEvidenceMode.RedactedExcerpt,
            tool.Capability.EvidenceMode);
    }

    [Fact]
    public void TemporaryArchiveRedactsCapsAndDeletesItsExactFile()
    {
        var archive =
            CopilotTemporaryRedactedOutputArchive.TryCreate(
                "ShellOutput",
                "stdout",
                maximumCharacters: 48);
        Assert.NotNull(archive);
        var path = archive!.StoragePath;
        var raw = "token=top-secret " + new string('x', 100);
        try
        {
            archive.Append(raw);

            Assert.True(File.Exists(path));
            Assert.Equal(raw.Length, archive.ObservedCharacters);
            Assert.Equal(48, archive.ArchivedCharacters);
            Assert.True(archive.IsTruncated);
            var page = archive.Read(
                offsetCharacters: 0,
                maximumCharacters: 48,
                CancellationToken.None);
            Assert.True(page.Available, page.ErrorMessage);
            Assert.Equal(48, page.ReturnedCharacters);
            Assert.True(page.EndOfAvailableOutput);
            Assert.True(page.ArchiveTruncated);
            Assert.Contains("token=<redacted>", page.Content);
            Assert.DoesNotContain("top-secret", page.Content);

            using var file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var bytes = new MemoryStream();
            file.CopyTo(bytes);
            var onDisk = Encoding.Unicode.GetString(
                bytes.ToArray());
            Assert.Contains("token=<redacted>", onDisk);
            Assert.DoesNotContain("top-secret", onDisk);
        }
        finally
        {
            archive.Dispose();
        }

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task TruncatedCommandOutputIsPagedAndConversationScoped()
    {
        using var registry =
            new CopilotShellCommandOutputArchiveRegistry();
        var fullOutput =
            new string('x', 70_000)
            + "\nshell-evidence token=shell-secret\n";
        var runner = new LargeOutputShellRunner(fullOutput);
        var executablePath = Environment.ProcessPath
            ?? typeof(CopilotShellCommandOutputArchiveTests)
                .Assembly.Location;
        var service = new CopilotShellCommandService(
            runner,
            _ => executablePath,
            registry);
        var request = CreateRequest(
            "run a command",
            "conversation-shell-archive");
        var result = await service.ExecuteAsync(
            request,
            CreateInput("Write-Output test"),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains(
            "stdout_preview_truncated: true",
            result.Content,
            StringComparison.Ordinal);
        Assert.Contains(
            $"stdout_observed_characters: {fullOutput.Length}",
            result.Content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("shell-secret", result.Content);
        var snapshot = Assert.Single(
            registry.GetSnapshots(request.ConversationId));
        Assert.Contains(
            $"output_archive_id: {snapshot.Id}",
            result.Content,
            StringComparison.Ordinal);
        Assert.True(snapshot.StandardOutputArchiveAvailable);
        Assert.False(snapshot.StandardOutputArchiveTruncated);

        var expected =
            CopilotMcpAuditLogger.RedactText(fullOutput);
        var archived = ReadAll(
            registry,
            request,
            snapshot.Id,
            CopilotShellCommandOutputStream.StandardOutput);
        Assert.Equal(expected, archived);
        Assert.Contains("shell-evidence", archived);
        Assert.Contains("token=<redacted>", archived);
        Assert.DoesNotContain("shell-secret", archived);

        var readTool =
            new CopilotReadShellCommandOutputTool(registry);
        var read = await readTool.ExecuteAsync(
            request,
            CreateReadInput(
                snapshot.Id,
                Math.Max(0, expected.Length - 128),
                128),
            CancellationToken.None);
        Assert.True(read.Success, read.ErrorMessage);
        Assert.Contains("end_of_output: true", read.Content);
        Assert.Contains("token=<redacted>", read.Content);
        Assert.DoesNotContain("shell-secret", read.Content);

        var crossConversation = await readTool.ExecuteAsync(
            CreateRequest(
                "read command output",
                "conversation-shell-other"),
            CreateReadInput(snapshot.Id, 0, 128),
            CancellationToken.None);
        Assert.False(crossConversation.Success);
        Assert.Equal(
            CopilotToolFailureKind.NotFound,
            crossConversation.FailureKind);

        Assert.Equal(
            1,
            registry.ClearConversation(request.ConversationId));
        var afterClear = registry.Read(
            request.ConversationId,
            snapshot.Id,
            CopilotShellCommandOutputStream.StandardOutput,
            0,
            128,
            CancellationToken.None);
        Assert.False(afterClear.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, afterClear.FailureKind);
    }

    [Fact]
    public async Task ShortCommandOutputDoesNotRetainAnArchive()
    {
        using var registry =
            new CopilotShellCommandOutputArchiveRegistry();
        var runner = new LargeOutputShellRunner(
            "ready\n",
            truncated: false);
        var executablePath = Environment.ProcessPath
            ?? typeof(CopilotShellCommandOutputArchiveTests)
                .Assembly.Location;
        var service = new CopilotShellCommandService(
            runner,
            _ => executablePath,
            registry);
        var request = CreateRequest(
            "run a command",
            "conversation-shell-short");
        var result = await service.ExecuteAsync(
            request,
            CreateInput("Write-Output ready"),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains(
            "stdout_preview_truncated: false",
            result.Content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("output_archive_id:", result.Content);
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public async Task RealPowerShellCommandArchivesOmittedOutput()
    {
        if (!OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(
                CopilotShellCommandService.FindTrustedShellExecutable(
                    CopilotShellKind.PowerShell)))
        {
            return;
        }

        using var registry =
            new CopilotShellCommandOutputArchiveRegistry();
        var service = new CopilotShellCommandService(
            new CopilotShellProcessRunner(),
            CopilotShellCommandService.FindTrustedShellExecutable,
            registry);
        var request = CreateRequest(
            "run PowerShell command",
            "conversation-shell-real");
        var result = await service.ExecuteAsync(
            request,
            CreateInput(
                "[Console]::Out.Write(('x' * 70000)); Write-Output 'shell-evidence'; Write-Output 'token=shell-secret'"),
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var snapshot = Assert.Single(
            registry.GetSnapshots(request.ConversationId));
        Assert.True(snapshot.StandardOutputPreviewTruncated);
        Assert.True(
            snapshot.ObservedStandardOutputCharacters > 65_536);
        Assert.False(snapshot.StandardOutputArchiveTruncated);
        var archived = ReadAll(
            registry,
            request,
            snapshot.Id,
            CopilotShellCommandOutputStream.StandardOutput);
        Assert.StartsWith(new string('x', 256), archived);
        Assert.Contains("shell-evidence", archived);
        Assert.Contains("token=<redacted>", archived);
        Assert.DoesNotContain("shell-secret", archived);
        Assert.Equal(
            archived.Length,
            snapshot.ArchivedStandardOutputCharacters);
    }

    private static string ReadAll(
        CopilotShellCommandOutputArchiveRegistry registry,
        CopilotAgentRequest request,
        string archiveId,
        CopilotShellCommandOutputStream stream)
    {
        var output = new StringBuilder();
        var offset = 0;
        while (true)
        {
            var result = registry.Read(
                request.ConversationId,
                archiveId,
                stream,
                offset,
                CopilotOutputArchiveLimits.MaximumReadCharacters,
                CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            var page = Assert.IsType<CopilotRedactedOutputArchivePage>(
                result.Page);
            Assert.Equal(offset, page.OffsetCharacters);
            output.Append(page.Content);
            if (page.EndOfAvailableOutput)
                return output.ToString();

            Assert.True(page.NextOffsetCharacters > offset);
            offset = page.NextOffsetCharacters;
        }
    }

    private static CopilotAgentRequest CreateRequest(
        string userText,
        string conversationId)
    {
        var workspace = Path.GetFullPath(Path.GetTempPath());
        return new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "task-shell-archive",
            WorkspacePath = workspace,
            UserText = userText,
            TaskIntentText = userText,
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = [workspace],
            WritableLocalRootPaths = [workspace],
            PreferredShell = CopilotShellKind.PowerShell,
        };
    }

    private static CopilotAgentToolInput CreateInput(string command) =>
        new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = command,
                ["shell"] = "powershell",
                ["workingDirectory"] =
                    Path.GetFullPath(Path.GetTempPath()),
                ["timeoutSeconds"] = 30,
            },
        };

    private static CopilotAgentToolInput CreateReadInput(
        string archiveId,
        int offsetCharacters,
        int maximumCharacters) =>
        new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["archiveId"] = archiveId,
                ["stream"] = "stdout",
                ["offsetCharacters"] = offsetCharacters,
                ["maximumCharacters"] = maximumCharacters,
            },
        };

    private sealed class LargeOutputShellRunner :
        ICopilotShellProcessRunner
    {
        private readonly string _fullOutput;
        private readonly bool _truncated;

        public LargeOutputShellRunner(
            string fullOutput,
            bool truncated = true)
        {
            _fullOutput = fullOutput;
            _truncated = truncated;
        }

        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            command.StandardOutputReceived?.Invoke(_fullOutput);
            var preview = _truncated
                ? _fullOutput[..Math.Min(8_192, _fullOutput.Length)]
                    + "\n...<shell output truncated>...\n"
                    + _fullOutput[
                        Math.Max(0, _fullOutput.Length - 8_192)..]
                : _fullOutput;
            return Task.FromResult(new CopilotShellProcessResult(
                ExitCode: 0,
                TimedOut: false,
                StandardOutput: preview,
                StandardError: string.Empty,
                Duration: TimeSpan.FromMilliseconds(10))
            {
                ProcessTreeContained = true,
                ObservedStandardOutputCharacters = _fullOutput.Length,
                StandardOutputTruncated = _truncated,
            });
        }
    }
}
