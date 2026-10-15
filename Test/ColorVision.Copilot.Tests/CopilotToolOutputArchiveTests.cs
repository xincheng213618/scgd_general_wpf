using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolOutputArchiveTests
{
    private const int SmallTokenLimit = 256;

    [Fact]
    public async Task TruncatedToolOutputCanBeReadOnlyFromItsOwningConversation()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "archive-conversation",
            TaskId = "archive-task",
        };
        const string secret = "private-tool-output-secret";
        var originalContent = $"api_key={secret};\n"
            + new string('x', 30_000)
            + "\nfull-result-tail";
        var outcome = CreateOutcome(
            request,
            new CopilotReadLocalFileTool(),
            originalContent);

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);
        var root = document.RootElement;
        var archive = root.GetProperty("content_archive");
        var archiveId = archive.GetProperty("archive_id").GetString();

        Assert.NotNull(archiveId);
        Assert.StartsWith("tool:", archiveId, StringComparison.Ordinal);
        Assert.Equal("ReadToolOutput", archive.GetProperty("retrieval_tool").GetString());
        Assert.True(archive.GetProperty("content_redacted").GetBoolean());
        Assert.True(root.GetProperty("content_truncated").GetBoolean());
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(formatted)
            <= SmallTokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
        Assert.Equal(originalContent, outcome.Result.Content);
        Assert.Equal(formatted, outcome.FormattedModelResult);
        Assert.Single(registry.GetSnapshots(request.ConversationId));

        var readTool = new CopilotReadToolOutputTool(registry);
        var page = await readTool.ExecuteAsync(
            request,
            CreateReadInput(archiveId!),
            CancellationToken.None);

        Assert.True(page.Success);
        Assert.Contains("api_key=<redacted>;", page.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, page.Content, StringComparison.Ordinal);
        Assert.Contains("content_redacted: true", page.Content, StringComparison.Ordinal);

        var otherConversation = await readTool.ExecuteAsync(
            new CopilotAgentRequest { ConversationId = "other-conversation" },
            CreateReadInput(archiveId!),
            CancellationToken.None);

        Assert.False(otherConversation.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, otherConversation.FailureKind);
        Assert.Equal(1, registry.ClearConversation(request.ConversationId));
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Theory]
    [InlineData(256, false)]
    [InlineData(256, true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task ModelVisibleArchivePagesContinueFromTheLastReturnedCharacter(int? tokenLimit, bool unicode)
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "model-page-conversation",
            TaskId = "model-page-task",
            ToolOutputTokenLimitOverride = tokenLimit,
        };
        var original = " \t" + string.Concat(Enumerable.Range(0, 3_000).Select(index =>
            unicode ? $"{index:D4}界😀|" : $"{index:D4}row|")) + "tail\t ";
        var snapshot = registry.Retain(request.ConversationId, "ReadLocalFile", "call:original", original);
        Assert.NotNull(snapshot);
        Assert.Equal(original.Length, snapshot.ArchivedCharacters);
        var readTool = new CopilotReadToolOutputTool(registry);
        var combined = new StringBuilder();
        var offset = 0;

        while (true)
        {
            var page = await readTool.ExecuteAsync(
                request,
                CreateReadInput(snapshot.Id, offset),
                CancellationToken.None);
            var captured = CopilotToolResultContract.Capture(readTool.Name, page);
            Assert.True(captured.Success);
            var outcome = CreateOutcome(request, readTool, captured.Content, captured);
            var formatted = CopilotToolOutputArchivePolicy.Format(outcome, tokenLimit, registry);
            using var document = JsonDocument.Parse(formatted);
            var modelContent = document.RootElement.GetProperty("content").GetString()!
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            if (tokenLimit.HasValue)
            {
                var maximumWeight = tokenLimit.Value * CopilotTokenEstimator.AsciiCharactersPerToken;
                Assert.InRange(formatted.Length, 1, maximumWeight);
                Assert.True(CopilotTokenEstimator.EstimateTextWeight(formatted) <= maximumWeight);
            }
            else
            {
                Assert.InRange(formatted.Length, 1, CopilotFrameworkToolResultFormatter.MaxSerializedCharacters);
                Assert.InRange(modelContent.Length, 1, CopilotFrameworkToolResultFormatter.MaxContentCharacters);
            }
            const string contentMarker = "\ncontent:\n";
            var contentStart = modelContent.IndexOf(contentMarker, StringComparison.Ordinal);
            Assert.True(contentStart >= 0, "The model-visible archive page must retain its header and content boundary.");
            var header = modelContent[..contentStart].Split('\n')
                .Select(line => line.Split(": ", 2, StringSplitOptions.None))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            var body = modelContent[(contentStart + contentMarker.Length)..];
            var returned = int.Parse(header["returned_characters"], CultureInfo.InvariantCulture);
            var next = int.Parse(header["next_offset_characters"], CultureInfo.InvariantCulture);
            var endOfOutput = header["end_of_output"] == "true";

            Assert.Equal(offset, int.Parse(header["offset_characters"], CultureInfo.InvariantCulture));
            Assert.True(next > offset, "Each model-visible archive page must advance its continuation cursor.");
            Assert.InRange(next, offset + 1, original.Length);
            Assert.Equal(body.Length, returned);
            Assert.Equal(offset + returned, next);
            Assert.Equal(original[offset..next], body);
            Assert.Equal(next == original.Length, endOfOutput);
            Assert.Equal(
                $"Read {returned} redacted character(s) from archived ReadLocalFile output; "
                    + (endOfOutput ? "reached the archive end." : "more archived output is available."),
                document.RootElement.GetProperty("summary").GetString());
            Assert.False(char.IsLowSurrogate(body[0]));
            Assert.False(char.IsHighSurrogate(body[^1]));
            combined.Append(body);
            offset = next;
            if (endOfOutput)
                break;
        }

        Assert.Equal(original, combined.ToString());
        Assert.Single(registry.GetSnapshots(request.ConversationId));
    }

    [Theory]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz123456", "<redacted>")]
    [InlineData("AKIAABCDEFGHIJKLMNOP", "<redacted>")]
    [InlineData("accesskey=shortcredential;", "accesskey=<redacted>;")]
    [InlineData("privatekey=\"shortcredential\";", "privatekey=\"<redacted>\";")]
    public async Task CredentialsAreRedactedBeforeArchiveCoordinatesAreAssigned(string credential, string redactedCredential)
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "credential-page-conversation",
            TaskId = "credential-page-task",
        };
        var original = " \tbefore|\0" + credential + "|after\t ";
        var expected = original.Replace(credential, redactedCredential, StringComparison.Ordinal)
            .Replace("\0", string.Empty, StringComparison.Ordinal);
        var snapshot = registry.Retain(request.ConversationId, "ReadLocalFile", "call:credential", original);
        Assert.NotNull(snapshot);
        Assert.Equal(original.Length, snapshot.ObservedCharacters);
        Assert.Equal(expected.Length, snapshot.ArchivedCharacters);
        var stored = registry.Read(request.ConversationId, snapshot.Id, 0,
            CopilotOutputArchiveLimits.MaximumReadCharacters, CancellationToken.None);
        Assert.True(stored.Success);
        Assert.Equal(expected, stored.Page!.Content);
        Assert.Equal(expected.Length, stored.Page.NextOffsetCharacters);
        var readTool = new CopilotReadToolOutputTool(registry);
        var page = await readTool.ExecuteAsync(request, CreateReadInput(snapshot.Id), CancellationToken.None);
        var captured = CopilotToolResultContract.Capture(readTool.Name, page);
        Assert.True(captured.Success);
        var formatted = CopilotToolOutputArchivePolicy.Format(
            CreateOutcome(request, readTool, captured.Content, captured), SmallTokenLimit, registry);
        using var document = JsonDocument.Parse(formatted);
        var modelContent = document.RootElement.GetProperty("content").GetString()!
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        const string contentMarker = "\ncontent:\n";
        var contentStart = modelContent.IndexOf(contentMarker, StringComparison.Ordinal);
        Assert.True(contentStart >= 0);
        var header = modelContent[..contentStart].Split('\n')
            .Select(line => line.Split(": ", 2, StringSplitOptions.None))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        var body = modelContent[(contentStart + contentMarker.Length)..];

        Assert.Equal(expected, body);
        Assert.DoesNotContain(credential, captured.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, formatted, StringComparison.Ordinal);
        Assert.Equal(expected.Length, int.Parse(header["returned_characters"], CultureInfo.InvariantCulture));
        Assert.Equal(expected.Length, int.Parse(header["next_offset_characters"], CultureInfo.InvariantCulture));
        Assert.Equal(expected.Length, int.Parse(header["archived_characters"], CultureInfo.InvariantCulture));
        Assert.Equal("true", header["end_of_output"]);
        Assert.Single(registry.GetSnapshots(request.ConversationId));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(128)]
    public async Task ModelBudgetTooSmallForAnArchivePageExposesNoCursorOrCompletionSummary(int tokenLimit)
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest { ConversationId = "small-page-budget", ToolOutputTokenLimitOverride = tokenLimit };
        var snapshot = registry.Retain(request.ConversationId, "ReadLocalFile", "call:original", new string('x', 16_384));
        Assert.NotNull(snapshot);
        var tool = new CopilotReadToolOutputTool(registry);
        var captured = CopilotToolResultContract.Capture(tool.Name,
            await tool.ExecuteAsync(request, CreateReadInput(snapshot.Id), CancellationToken.None));
        Assert.True(captured.Success);
        var formatted = CopilotToolOutputArchivePolicy.Format(CreateOutcome(request, tool, captured.Content, captured), tokenLimit, registry);
        using var document = JsonDocument.Parse(formatted);

        Assert.True(document.RootElement.GetProperty("content_truncated").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("summary", out _));
        Assert.DoesNotContain("next_offset_characters", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("<empty>", formatted, StringComparison.Ordinal);
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(formatted) <= tokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
        Assert.Single(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public async Task TruncatedFailureErrorCanBeReadFromItsRedactedArchive()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "failure-archive-conversation",
            TaskId = "failure-archive-task",
        };
        const string secret = "private-failure-secret";
        var error = $"token={secret};\n"
            + new string('x', 30_000)
            + "\nfull-error-tail";
        var tool = new LargeOutputTool(string.Empty);
        var outcome = CreateFailedOutcome(request, tool, error);

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);
        var root = document.RootElement;
        var archiveId = root
            .GetProperty("content_archive")
            .GetProperty("archive_id")
            .GetString();

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.NotNull(archiveId);
        Assert.Equal(error, outcome.Result.ErrorMessage);
        Assert.Single(registry.GetSnapshots(request.ConversationId));

        var page = await new CopilotReadToolOutputTool(registry).ExecuteAsync(
            request,
            CreateReadInput(archiveId!),
            CancellationToken.None);

        Assert.True(page.Success);
        Assert.Contains("[Tool Error]", page.Content, StringComparison.Ordinal);
        Assert.Contains("token=<redacted>;", page.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, page.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchiveIsNotCreatedWhenItsReferenceCannotFitTheProviderBudget()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "zero-budget-conversation",
            TaskId = "zero-budget-task",
        };
        var outcome = CreateOutcome(
            request,
            new CopilotReadLocalFileTool(),
            new string('x', 30_000));

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            toolOutputTokenLimit: 0,
            registry);

        Assert.Empty(formatted);
        Assert.Null(outcome.ToolOutputArchive);
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public void ReadingAnArchiveNeverCreatesARecursiveArchive()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "archive-reader-conversation",
            TaskId = "archive-reader-task",
        };
        var outcome = CreateOutcome(
            request,
            new CopilotReadToolOutputTool(registry),
            new string('x', 30_000));

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);

        Assert.True(document.RootElement.GetProperty("content_truncated").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("content_archive", out _));
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public void DedicatedShellOutputArchivesAreNotDuplicated()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "shell-archive-conversation",
            TaskId = "shell-archive-task",
        };
        var outcome = CreateOutcome(
            request,
            new CopilotShellCommandTool(),
            new string('x', 30_000));

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);

        Assert.True(document.RootElement.GetProperty("content_truncated").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("content_archive", out _));
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public async Task ExecutorPublishesTheExactArchivedModelResult()
    {
        var conversationId = "runtime-archive-" + Guid.NewGuid().ToString("N");
        var request = new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "runtime-archive-task",
            ToolOutputTokenLimitOverride = int.MaxValue,
        };
        var tool = new LargeOutputTool(new string('x', 400_000));
        var events = new List<CopilotAgentEvent>();
        try
        {
            var outcome = await new CopilotToolExecutor([]).ExecuteAsync(
                new CopilotToolInvocation
                {
                    CallId = "call:runtime-archive",
                    Round = 1,
                    Attempt = 1,
                    MaxAttempts = 1,
                    RuntimeName = "test",
                    Tool = tool,
                    AgentRequest = request,
                    ToolInput = CopilotAgentToolInput.Empty,
                },
                events.Add,
                CancellationToken.None);

            var toolResultEvent = Assert.Single(
                events,
                item => item.Type == CopilotAgentEventType.ToolResult);
            Assert.NotNull(outcome.ToolOutputArchive);
            Assert.NotNull(outcome.FormattedModelResult);
            Assert.Equal(outcome.FormattedModelResult, toolResultEvent.ModelToolResult);
            Assert.True(
                toolResultEvent.ModelToolResult.Length
                    <= CopilotCodeReviewSnapshot.MaximumModelObservationCharacters);
            CopilotAgentEventProtocol.Validate(toolResultEvent);
            using var document = JsonDocument.Parse(toolResultEvent.ModelToolResult);
            Assert.Equal(
                outcome.ToolOutputArchive!.Id,
                document.RootElement
                    .GetProperty("content_archive")
                    .GetProperty("archive_id")
                    .GetString());

            var readTool = new CopilotReadToolOutputTool(CopilotToolOutputArchiveRegistry.Shared);
            var readEvents = new List<CopilotAgentEvent>();
            var readOutcome = await new CopilotToolExecutor([]).ExecuteAsync(
                new CopilotToolInvocation
                {
                    CallId = "call:runtime-archive-read",
                    Round = 2,
                    Attempt = 1,
                    MaxAttempts = 1,
                    RuntimeName = "test",
                    Tool = readTool,
                    AgentRequest = new CopilotAgentRequest
                    {
                        ConversationId = conversationId,
                        TaskId = request.TaskId,
                        ToolOutputTokenLimitOverride = SmallTokenLimit,
                    },
                    ToolInput = CreateReadInput(outcome.ToolOutputArchive.Id),
                },
                readEvents.Add,
                CancellationToken.None);
            Assert.True(readOutcome.Result.Success);
            var nativeRead = Assert.IsType<CopilotToolOutputArchiveReadResult>(readOutcome.Result.ToolOutputArchiveRead);
            Assert.NotNull(nativeRead.Page);
            var readResultEvent = Assert.Single(readEvents, item => item.Type == CopilotAgentEventType.ToolResult);
            var publishedResult = Assert.IsType<CopilotToolResult>(readResultEvent.ToolResult);
            var publishedRead = Assert.IsType<CopilotToolOutputArchiveReadResult>(publishedResult.ToolOutputArchiveRead);
            Assert.Equal(nativeRead.Page, publishedRead.Page);
            Assert.Equal(readOutcome.FormattedModelResult, readResultEvent.ModelToolResult);
            Assert.InRange(readResultEvent.ModelToolResult.Length, 1,
                SmallTokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
            Assert.True(CopilotTokenEstimator.EstimateTextWeight(readResultEvent.ModelToolResult)
                <= SmallTokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
            CopilotAgentEventProtocol.Validate(readResultEvent);
            using var readDocument = JsonDocument.Parse(readResultEvent.ModelToolResult);
            var modelContent = readDocument.RootElement.GetProperty("content").GetString()!
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            const string contentMarker = "\ncontent:\n";
            var contentStart = modelContent.IndexOf(contentMarker, StringComparison.Ordinal);
            Assert.True(contentStart >= 0);
            var header = modelContent[..contentStart].Split('\n')
                .Select(line => line.Split(": ", 2, StringSplitOptions.None))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            var body = modelContent[(contentStart + contentMarker.Length)..];

            Assert.Equal(body.Length, int.Parse(header["returned_characters"], CultureInfo.InvariantCulture));
            Assert.Equal(body.Length, int.Parse(header["next_offset_characters"], CultureInfo.InvariantCulture));
            Assert.Equal(new string('x', body.Length), body);
            Assert.True(body.Length < nativeRead.Page.ReturnedCharacters);
            Assert.Equal("false", header["end_of_output"]);
            Assert.Null(readOutcome.ToolOutputArchive);
            Assert.False(readDocument.RootElement.TryGetProperty("content_archive", out _));
            Assert.Single(CopilotToolOutputArchiveRegistry.Shared.GetSnapshots(conversationId));
        }
        finally
        {
            CopilotToolOutputArchiveRegistry.Shared.ClearConversation(conversationId);
        }
    }

    [Fact]
    public void ArchiveRetentionEvictsAndDisposesTheOldestEntry()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        CopilotToolOutputArchiveSnapshot? first = null;
        CopilotToolOutputArchiveSnapshot? latest = null;
        for (var index = 0;
            index <= CopilotToolOutputArchiveRegistry.MaximumRetainedArchives;
            index++)
        {
            latest = registry.Retain(
                "retention-conversation",
                "ReadLocalFile",
                $"call:{index}",
                $"content-{index}");
            first ??= latest;
        }

        Assert.NotNull(first);
        Assert.NotNull(latest);
        Assert.Equal(
            CopilotToolOutputArchiveRegistry.MaximumRetainedArchives,
            registry.GetSnapshots("retention-conversation").Count);
        Assert.False(registry.Read(
            "retention-conversation",
            first!.Id,
            0,
            100,
            CancellationToken.None).Success);
        Assert.True(registry.Read(
            "retention-conversation",
            latest!.Id,
            0,
            100,
            CancellationToken.None).Success);
    }

    [Fact]
    public void ArchiveCapacityAndPagingDoNotSplitUnicodeSurrogatePairs()
    {
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
            "ToolOutput",
            "content",
            maximumCharacters: 3);
        Assert.NotNull(archive);

        archive!.Append("😀😀");
        archive.Complete();

        Assert.Equal(2, archive.ArchivedCharacters);
        Assert.True(archive.IsTruncated);
        var page = archive.Read(
            offsetCharacters: 0,
            maximumCharacters: 1,
            CancellationToken.None);
        Assert.Equal("😀", page.Content);
        Assert.Equal(2, page.ReturnedCharacters);
        Assert.Equal(2, page.NextOffsetCharacters);

        var interiorOffset = archive.Read(
            offsetCharacters: 1,
            maximumCharacters: 1,
            CancellationToken.None);
        Assert.Equal(2, interiorOffset.OffsetCharacters);
        Assert.Empty(interiorOffset.Content);
        Assert.True(interiorOffset.EndOfAvailableOutput);
    }

    [Fact]
    public void ArchiveIsNotCreatedThroughAReparsePointDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"copilot-output-archive-root-{Guid.NewGuid():N}");
        var outside = Path.Combine(
            Path.GetTempPath(),
            $"copilot-output-archive-outside-{Guid.NewGuid():N}");
        var linkedDirectory = Path.Combine(root, "ColorVision");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(linkedDirectory, outside);

        try
        {
            using var archive = CopilotTemporaryRedactedOutputArchive.TryCreateUnderRoot(
                root,
                "ToolOutput",
                "content");

            Assert.Null(archive);
            Assert.Empty(Directory.EnumerateFiles(
                outside,
                "*.log",
                SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(linkedDirectory, recursive: false);
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void ArchiveStorageCannotBeModifiedOrRenamedWhileRetained()
    {
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
            "ToolOutput",
            "content");
        Assert.NotNull(archive);

        archive!.Append(new string('a', 64));
        archive.Complete();

        Assert.Throws<IOException>(() =>
        {
            using var stream = new FileStream(
                archive.StoragePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
        });
        Assert.Throws<IOException>(() => File.Move(
            archive.StoragePath,
            archive.StoragePath + ".moved"));

        var page = archive.Read(
            offsetCharacters: 0,
            maximumCharacters: 64,
            CancellationToken.None);
        Assert.True(page.Available);
        Assert.Equal(new string('a', 64), page.Content);
    }

    [Fact]
    public void ReadingAndSearchingBeforeCompletionPreserveTheAppendPosition()
    {
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
            "ToolOutput",
            "content");
        Assert.NotNull(archive);
        var first = "needle-" + new string('a', 64);
        var second = new string('b', 64) + "-tail";

        archive!.Append(first);
        var pageBeforeCompletion = archive.Read(
            offsetCharacters: 0,
            maximumCharacters: 64,
            CancellationToken.None);
        var searchBeforeCompletion = archive.Search(
            "needle",
            offsetCharacters: 0,
            CancellationToken.None);
        archive.Append(second);
        archive.Complete();

        Assert.True(pageBeforeCompletion.Available);
        Assert.True(searchBeforeCompletion.Available);
        Assert.True(searchBeforeCompletion.Matched);
        var completePage = archive.Read(
            offsetCharacters: 0,
            maximumCharacters: first.Length + second.Length,
            CancellationToken.None);
        Assert.Equal(first + second, completePage.Content);
    }

    private static CopilotToolExecutionOutcome CreateOutcome(
        CopilotAgentRequest request,
        ICopilotTool tool,
        string content,
        CopilotToolResult? result = null)
    {
        const string CallId = "call:tool-output-archive";
        return new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = CallId,
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = request,
                ToolInput = CopilotAgentToolInput.Empty,
                ToolCall = new CopilotToolCall
                {
                    ToolName = tool.Name,
                    ToolInput = CopilotAgentToolInput.Empty,
                },
            },
            Result = result ?? new CopilotToolResult
            {
                ToolName = tool.Name,
                Success = true,
                Summary = "Produced a large text result.",
                Content = content,
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = CallId,
                ToolName = tool.Name,
                Attempt = 1,
                MaxAttempts = 1,
                State = CopilotToolExecutionState.Completed,
            },
        };
    }

    private static CopilotToolExecutionOutcome CreateFailedOutcome(
        CopilotAgentRequest request,
        ICopilotTool tool,
        string error)
    {
        const string CallId = "call:failed-tool-output-archive";
        return new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = CallId,
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = request,
                ToolInput = CopilotAgentToolInput.Empty,
            },
            Result = new CopilotToolResult
            {
                ToolName = tool.Name,
                Success = false,
                Summary = "The tool failed with a large diagnostic.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Internal,
                FailureCode = "large_diagnostic",
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = CallId,
                ToolName = tool.Name,
                Attempt = 1,
                MaxAttempts = 1,
                State = CopilotToolExecutionState.Failed,
                FailureKind = CopilotToolFailureKind.Internal,
            },
        };
    }

    private static CopilotAgentToolInput CreateReadInput(string archiveId, int offsetCharacters = 0) =>
        new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["archiveId"] = archiveId,
                ["offsetCharacters"] = offsetCharacters,
                ["maximumCharacters"] = CopilotOutputArchiveLimits.MaximumReadCharacters,
            },
        };

    private sealed class LargeOutputTool(string content) : ICopilotTool
    {
        public string Name => "LargeOutput";

        public string Description => "Returns a large result for runtime archive tests.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Produced a large text result.",
                Content = content,
            });
    }
}
