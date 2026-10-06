using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotForegroundProcessEvidenceTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ColorVisionCopilotForegroundProcessEvidenceTests",
        Guid.NewGuid().ToString("N")));

    [Theory]
    [InlineData(null, false)]
    [InlineData(256, false)]
    [InlineData(256, true)]
    public async Task ShellModelPagesContinueFromTheLastDeliveredCharacter(int? tokenLimit, bool unicode)
    {
        var request = CreateRequest(tokenLimit);
        var stream = unicode ? "stderr" : "stdout";
        var canonical = " \t" + string.Concat(Enumerable.Range(0, 3_000).Select(index =>
            unicode ? $"{index:D4}界😀|" : $"{index:D4}row|")) + "tail\t ";
        using var registry = new CopilotShellCommandOutputArchiveRegistry();
        using var capture = new CopilotShellCommandOutputCapture();
        var splitAt = unicode ? canonical.IndexOf("😀", StringComparison.Ordinal) + 1 : canonical.Length / 2;
        if (unicode)
        {
            capture.AppendStandardError(canonical[..splitAt]);
            capture.AppendStandardError(canonical[splitAt..]);
        }
        else
        {
            capture.AppendStandardOutput(canonical[..splitAt]);
            capture.AppendStandardOutput(canonical[splitAt..]);
        }
        capture.Complete();
        var snapshot = registry.Retain(request.ConversationId, capture, new CopilotShellProcessResult(
            ExitCode: 0,
            TimedOut: false,
            StandardOutput: unicode ? string.Empty : canonical,
            StandardError: unicode ? canonical : string.Empty,
            Duration: TimeSpan.Zero)
        {
            StandardOutputTruncated = !unicode,
            StandardErrorTruncated = unicode,
        });
        Assert.NotNull(snapshot);
        Assert.Equal(canonical.Length, unicode ? snapshot.ArchivedStandardErrorCharacters : snapshot.ArchivedStandardOutputCharacters);
        var tool = new CopilotReadShellCommandOutputTool(registry);
        var combined = new StringBuilder();
        var offset = 0;
        while (true)
        {
            var events = new List<CopilotAgentEvent>();
            var outcome = await new CopilotToolExecutor([]).ExecuteAsync(new CopilotToolInvocation
            {
                CallId = $"call:shell-model-page-{stream}-{offset}",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = request,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["archiveId"] = snapshot.Id,
                        ["stream"] = stream,
                        ["offsetCharacters"] = offset,
                        ["maximumCharacters"] = CopilotOutputArchiveLimits.MaximumReadCharacters,
                    },
                },
            }, events.Add, CancellationToken.None);
            Assert.True(outcome.Result.Success, outcome.Result.ErrorMessage);
            var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
            CopilotAgentEventProtocol.Validate(terminal);
            Assert.Equal(outcome.FormattedModelResult, terminal.ModelToolResult);
            Assert.Null(outcome.ToolOutputArchive);
            var result = JObject.Parse(terminal.ModelToolResult);
            Assert.Null(result["content_archive"]);
            var modelContent = result["content"]!.Value<string>()!
                .Replace("\r\n", "\n", StringComparison.Ordinal);
            if (tokenLimit.HasValue)
            {
                var maximumWeight = tokenLimit.Value * CopilotTokenEstimator.AsciiCharactersPerToken;
                Assert.InRange(terminal.ModelToolResult.Length, 1, maximumWeight);
                Assert.True(CopilotTokenEstimator.EstimateTextWeight(terminal.ModelToolResult) <= maximumWeight);
            }
            else
            {
                Assert.InRange(terminal.ModelToolResult.Length, 1, CopilotFrameworkToolResultFormatter.MaxSerializedCharacters);
                Assert.InRange(modelContent.Length, 1, CopilotFrameworkToolResultFormatter.MaxContentCharacters);
            }
            const string contentMarker = "\ncontent:\n";
            var contentStart = modelContent.IndexOf(contentMarker, StringComparison.Ordinal);
            Assert.True(contentStart >= 0, "The model page must retain its complete header and content boundary.");
            var header = modelContent[..contentStart].Split('\n')
                .Select(line => line.Split(": ", 2, StringSplitOptions.None))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            var body = modelContent[(contentStart + contentMarker.Length)..];
            var returned = int.Parse(header["returned_characters"], CultureInfo.InvariantCulture);
            var next = int.Parse(header["next_offset_characters"], CultureInfo.InvariantCulture);
            var endOfOutput = header["end_of_output"] == "true";
            Assert.Equal(snapshot.Id, header["archive_id"]);
            Assert.Equal(stream, header["stream"]);
            Assert.Equal(offset, int.Parse(header["offset_characters"], CultureInfo.InvariantCulture));
            Assert.True(next > offset, "Each delivered model page must advance its continuation cursor.");
            Assert.InRange(next, offset + 1, canonical.Length);
            Assert.Equal(body.Length, returned);
            Assert.Equal(offset + returned, next);
            Assert.Equal(canonical[offset..next], body);
            Assert.Equal(canonical.Length, int.Parse(header["archived_characters"], CultureInfo.InvariantCulture));
            Assert.Equal("false", header["archive_truncated"]);
            Assert.Equal(next == canonical.Length, endOfOutput);
            Assert.Equal(
                $"Read {returned} archived {stream} character(s) from shell output archive {snapshot.Id}; "
                    + (endOfOutput ? "reached the archive end." : "more archived output is available."),
                result["summary"]!.Value<string>());
            Assert.False(char.IsLowSurrogate(body[0]));
            Assert.False(char.IsHighSurrogate(body[^1]));
            combined.Append(body);
            offset = next;
            if (endOfOutput)
                break;
        }
        Assert.Equal(canonical, combined.ToString());
        Assert.Single(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public async Task ShellArchiveCredentialsRemainRedactedThroughSmallRuntimePages()
    {
        const string standardOutput = "trace-start|sk-abcdefghijklmnopqrstuvwxyz123456|trace-end";
        const string standardError = "error-start|AKIAABCDEFGHIJKLMNOP|error-end";
        var request = CreateRequest();
        using var registry = new CopilotShellCommandOutputArchiveRegistry();
        using var capture = new CopilotShellCommandOutputCapture();
        capture.AppendStandardOutput(standardOutput[..15]);
        capture.AppendStandardOutput(standardOutput[15..]);
        capture.AppendStandardError(standardError[..14]);
        capture.AppendStandardError(standardError[14..]);
        capture.Complete();
        var snapshot = registry.Retain(request.ConversationId, capture, new CopilotShellProcessResult(
            ExitCode: 0,
            TimedOut: false,
            StandardOutput: standardOutput,
            StandardError: standardError,
            Duration: TimeSpan.Zero)
        {
            StandardOutputTruncated = true,
            StandardErrorTruncated = true,
        });
        Assert.NotNull(snapshot);
        Assert.Equal(standardOutput.Length, snapshot.ObservedStandardOutputCharacters);
        Assert.Equal(standardError.Length, snapshot.ObservedStandardErrorCharacters);

        var tool = new CopilotReadShellCommandOutputTool(registry);
        foreach (var (stream, expected) in new[]
        {
            ("stdout", "trace-start|<redacted>|trace-end"),
            ("stderr", "error-start|<redacted>|error-end"),
        })
        {
            var output = new StringBuilder();
            var offset = 0;
            while (true)
            {
                var events = new List<CopilotAgentEvent>();
                var outcome = await new CopilotToolExecutor([]).ExecuteAsync(new CopilotToolInvocation
                {
                    CallId = $"call:shell-archive-{stream}-{offset}",
                    Round = 1,
                    Attempt = 1,
                    MaxAttempts = 1,
                    RuntimeName = "test",
                    Tool = tool,
                    AgentRequest = request,
                    ToolInput = new CopilotAgentToolInput
                    {
                        Arguments = new Dictionary<string, object?>
                        {
                            ["archiveId"] = snapshot.Id,
                            ["stream"] = stream,
                            ["offsetCharacters"] = offset,
                            ["maximumCharacters"] = 7,
                        },
                    },
                }, events.Add, CancellationToken.None);
                Assert.True(outcome.Result.Success, outcome.Result.ErrorMessage);
                var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
                CopilotAgentEventProtocol.Validate(terminal);
                Assert.Equal(outcome.FormattedModelResult, terminal.ModelToolResult);
                Assert.Null(outcome.ToolOutputArchive);
                var content = JObject.Parse(terminal.ModelToolResult)["content"]!.Value<string>()!
                    .Replace("\r\n", "\n", StringComparison.Ordinal);
                const string contentMarker = "content:\n";
                var bodyStart = content.IndexOf(contentMarker, StringComparison.Ordinal);
                Assert.True(bodyStart >= 0, content);
                var body = content[(bodyStart + contentMarker.Length)..];
                output.Append(body);
                var nextOffset = offset + body.Length;
                Assert.Contains($"offset_characters: {offset}\n", content, StringComparison.Ordinal);
                Assert.Contains($"returned_characters: {body.Length}\n", content, StringComparison.Ordinal);
                Assert.Contains($"next_offset_characters: {nextOffset}\n", content, StringComparison.Ordinal);
                Assert.Contains($"archived_characters: {expected.Length}\n", content, StringComparison.Ordinal);
                Assert.Contains("archive_truncated: false\n", content, StringComparison.Ordinal);
                if (content.Contains("end_of_output: true\n", StringComparison.Ordinal))
                    break;
                Assert.True(nextOffset > offset);
                offset = nextOffset;
            }
            Assert.Equal(expected, output.ToString());
        }
    }

    [Theory]
    [InlineData("password=alpha\"omega\"gamma; trace-visible", "trace-visible", new string[] { "alpha", "omega", "gamma" }, "")]
    [InlineData("\"password=alpha\" visible-trace", "visible-trace", new string[] { "alpha" }, "")]
    [InlineData("password=\"alpha\"gamma; trace-visible", "trace-visible", new string[] { "alpha", "gamma" }, "password=\"<redacted>\"")]
    public async Task ShellArchiveQuotedAssignmentsHideTheWholeValueAndPreservePublicTrace(
        string source, string publicTrace, string[] secrets, string expectedQuotedBoundary)
    {
        var request = CreateRequest();
        using var registry = new CopilotShellCommandOutputArchiveRegistry();
        using var capture = new CopilotShellCommandOutputCapture();
        foreach (var character in source)
            capture.AppendStandardOutput(character.ToString());
        capture.Complete();
        var snapshot = registry.Retain(request.ConversationId, capture, new CopilotShellProcessResult(
            ExitCode: 0,
            TimedOut: false,
            StandardOutput: source,
            StandardError: string.Empty,
            Duration: TimeSpan.Zero)
        {
            StandardOutputTruncated = true,
        });
        Assert.NotNull(snapshot);
        Assert.Equal(source.Length, snapshot.ObservedStandardOutputCharacters);
        var read = registry.Read(request.ConversationId, snapshot.Id, CopilotShellCommandOutputStream.StandardOutput,
            0, CopilotOutputArchiveLimits.MaximumReadCharacters, CancellationToken.None);
        Assert.True(read.Success, read.ErrorMessage);
        var canonical = Assert.IsType<CopilotRedactedOutputArchivePage>(read.Page);
        var tool = new CopilotReadShellCommandOutputTool(registry);
        var events = new List<CopilotAgentEvent>();
        var outcome = await new CopilotToolExecutor([]).ExecuteAsync(new CopilotToolInvocation
        {
            CallId = "call:shell-quoted-assignment",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "test",
            Tool = tool,
            AgentRequest = request,
            ToolInput = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?> { ["archiveId"] = snapshot.Id },
            },
        }, events.Add, CancellationToken.None);
        Assert.True(outcome.Result.Success, outcome.Result.ErrorMessage);
        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        CopilotAgentEventProtocol.Validate(terminal);
        Assert.Equal(outcome.FormattedModelResult, terminal.ModelToolResult);
        Assert.Null(outcome.ToolOutputArchive);
        var result = JObject.Parse(terminal.ModelToolResult);
        Assert.Null(result["content_archive"]);
        var content = result["content"]!.Value<string>()!.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string contentMarker = "\ncontent:\n";
        var bodyStart = content.IndexOf(contentMarker, StringComparison.Ordinal);
        Assert.True(bodyStart >= 0, content);
        var body = content[(bodyStart + contentMarker.Length)..];
        Assert.Equal(canonical.Content, body);
        Assert.Equal(body.Length, canonical.ReturnedCharacters);
        Assert.Equal(body.Length, canonical.NextOffsetCharacters);
        Assert.Equal(body.Length, snapshot.ArchivedStandardOutputCharacters);
        Assert.True(canonical.EndOfAvailableOutput);
        Assert.Contains($"returned_characters: {body.Length}\n", content, StringComparison.Ordinal);
        Assert.Contains($"next_offset_characters: {body.Length}\n", content, StringComparison.Ordinal);
        Assert.Contains($"archived_characters: {body.Length}\n", content, StringComparison.Ordinal);
        Assert.Contains("end_of_output: true\n", content, StringComparison.Ordinal);
        Assert.Contains(publicTrace, canonical.Content, StringComparison.Ordinal);
        Assert.Contains(publicTrace, body, StringComparison.Ordinal);
        if (expectedQuotedBoundary.Length > 0)
            Assert.Contains(expectedQuotedBoundary, canonical.Content, StringComparison.Ordinal);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, canonical.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, terminal.ModelToolResult, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ShellContainmentFailureUsesStableFailureCode()
    {
        Directory.CreateDirectory(_root);
        var shellPath = Path.Combine(_root, "powershell.exe");
        File.WriteAllText(shellPath, string.Empty);
        var service = new CopilotShellCommandService(
            new ContainmentFailureShellRunner(),
            _ => shellPath);

        var result = await service.ExecuteAsync(
            CreateRequest(),
            CreateShellInput("Get-Location"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Equal(
            CopilotShellCommandService.ProcessTreeContainmentUnavailableFailureCode,
            result.FailureCode);
        Assert.Contains("process-tree containment", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Windows Job Object", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForegroundServicesPublishOnlyFixedStructuredProcessOutcomes()
    {
        Directory.CreateDirectory(_root);
        var projectPath = Path.Combine(_root, "Evidence.csproj");
        var dotnetPath = Path.Combine(_root, "dotnet.exe");
        var shellPath = Path.Combine(_root, "powershell.exe");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(dotnetPath, string.Empty);
        File.WriteAllText(shellPath, string.Empty);
        var request = CreateRequest();

        var validationRunner = new SequenceValidationRunner(
            new CopilotWorkspaceValidationProcessResult(
                0,
                false,
                "private validation stdout",
                "private validation stderr",
                TimeSpan.FromMilliseconds(1)),
            new CopilotWorkspaceValidationProcessResult(
                0,
                true,
                "private timed-out validation stdout",
                "private timed-out validation stderr",
                TimeSpan.FromSeconds(10)));
        var validationService = new CopilotWorkspaceValidationService(
            validationRunner,
            () => dotnetPath);

        var buildResult = await validationService.ExecuteAsync(
            request,
            CreateValidationInput(projectPath, "build"),
            CancellationToken.None);
        var timedOutTestResult = await validationService.ExecuteAsync(
            request,
            CreateValidationInput(projectPath, "test"),
            CancellationToken.None);

        Assert.True(buildResult.Success);
        Assert.Equal("build", buildResult.ProcessOperation);
        Assert.Equal(0, buildResult.ProcessExitCode);
        Assert.False(buildResult.ProcessTimedOut);
        Assert.False(timedOutTestResult.Success);
        Assert.Equal("test", timedOutTestResult.ProcessOperation);
        Assert.Equal(0, timedOutTestResult.ProcessExitCode);
        Assert.True(timedOutTestResult.ProcessTimedOut);

        using var archiveRegistry = new CopilotShellCommandOutputArchiveRegistry();
        var shellRunner = new SequenceShellRunner(
            new CopilotShellProcessResult(
                23,
                false,
                "private shell stdout",
                "private shell stderr",
                TimeSpan.FromMilliseconds(1)),
            new CopilotShellProcessResult(
                0,
                true,
                "private timed-out shell stdout",
                "private timed-out shell stderr",
                TimeSpan.FromSeconds(10)));
        var shellService = new CopilotShellCommandService(
            shellRunner,
            _ => shellPath,
            archiveRegistry);

        var failedShellResult = await shellService.ExecuteAsync(
            request,
            CreateShellInput("private foreground command"),
            CancellationToken.None);
        var timedOutShellResult = await shellService.ExecuteAsync(
            request,
            CreateShellInput("private timed-out foreground command"),
            CancellationToken.None);

        Assert.False(failedShellResult.Success);
        Assert.Equal("shell", failedShellResult.ProcessOperation);
        Assert.Equal(23, failedShellResult.ProcessExitCode);
        Assert.False(failedShellResult.ProcessTimedOut);
        Assert.False(timedOutShellResult.Success);
        Assert.Equal("shell", timedOutShellResult.ProcessOperation);
        Assert.Equal(0, timedOutShellResult.ProcessExitCode);
        Assert.True(timedOutShellResult.ProcessTimedOut);

        var observation = CopilotToolObservation.FromResult(failedShellResult);
        Assert.Equal("shell", observation.ProcessOperation);
        Assert.Equal(23, observation.ProcessExitCode);
        Assert.False(observation.ProcessTimedOut);
        var contradictoryObservation = CopilotToolObservation.FromResult(new CopilotToolResult
        {
            ToolName = "RunShellCommand",
            Success = false,
            ProcessOperation = "shell",
            ProcessExitCode = 0,
        });
        Assert.Empty(contradictoryObservation.ProcessOperation);
        Assert.Null(contradictoryObservation.ProcessExitCode);
        Assert.False(contradictoryObservation.ProcessTimedOut);

        var trace = CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                ToolName = "RunShellCommand",
                Access = CopilotToolAccess.Write,
                State = CopilotToolExecutionState.Failed,
                ArgumentSummary = "private foreground command",
            },
            failedShellResult);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Shell validation finished.")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        assistantMessage.AgentTraceEntries.Add(trace);
        var evidence = CopilotGoalTurnEvidence.Capture(assistantMessage);
        var prompt = CopilotGoalCompletionEvaluator.BuildEvidencePrompt(
            CopilotConversationGoal.Create(
                "Verify the foreground shell outcome",
                new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero)),
            Array.Empty<CopilotRequestMessage>(),
            evidence);

        Assert.Contains(
            "process_operation=shell | process_state=exited | exit_code=23",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("private foreground command", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("private shell stdout", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("private shell stderr", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceRoundTripKeepsValidOutcomeAndDropsLegacyOrContradictoryMetadata()
    {
        var execution = new CopilotToolExecutionInfo
        {
            ToolName = "RunShellCommand",
            State = CopilotToolExecutionState.Failed,
        };
        var trace = CopilotAgentTraceEntry.FromResult(
            execution,
            new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = false,
                Summary = "private shell summary",
                FailureKind = CopilotToolFailureKind.Unspecified,
                FailureCode = CopilotShellCommandService.NonzeroExitFailureCode,
                ProcessOperation = "shell",
                ProcessExitCode = 23,
            });

        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, trace.SchemaVersion);
        Assert.Equal("shell", trace.ProcessOperation);
        Assert.Equal(23, trace.ProcessExitCode);
        Assert.False(trace.ProcessTimedOut);

        var serialized = JsonConvert.SerializeObject(trace);
        var restored = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(serialized);
        Assert.NotNull(restored);
        Assert.False(restored.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal("shell", restored.ProcessOperation);
        Assert.Equal(23, restored.ProcessExitCode);

        var timedOutTrace = CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                ToolName = "RunShellCommand",
                State = CopilotToolExecutionState.Failed,
            },
            new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = false,
                FailureKind = CopilotToolFailureKind.Transient,
                FailureCode = CopilotShellCommandService.TimedOutFailureCode,
                ProcessOperation = "shell",
                ProcessExitCode = 0,
                ProcessTimedOut = true,
            });
        Assert.Equal(0, timedOutTrace.ProcessExitCode);
        Assert.True(timedOutTrace.ProcessTimedOut);
        var restoredTimedOut = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(
            JsonConvert.SerializeObject(timedOutTrace));
        Assert.NotNull(restoredTimedOut);
        Assert.False(restoredTimedOut.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(0, restoredTimedOut.ProcessExitCode);
        Assert.True(restoredTimedOut.ProcessTimedOut);

        var legacyDocument = JObject.Parse(serialized);
        legacyDocument[nameof(CopilotAgentTraceEntry.SchemaVersion)] =
            CopilotAgentTraceEntry.CurrentSchemaVersion - 1;
        legacyDocument.Remove(nameof(CopilotAgentTraceEntry.ProcessOperation));
        legacyDocument.Remove(nameof(CopilotAgentTraceEntry.ProcessExitCode));
        legacyDocument.Remove(nameof(CopilotAgentTraceEntry.ProcessTimedOut));
        var legacy = legacyDocument.ToObject<CopilotAgentTraceEntry>();
        Assert.NotNull(legacy);
        Assert.True(legacy.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, legacy.SchemaVersion);
        Assert.Empty(legacy.ProcessOperation);
        Assert.Null(legacy.ProcessExitCode);
        Assert.False(legacy.ProcessTimedOut);

        var contradictory = new CopilotAgentTraceEntry
        {
            ToolName = "RunShellCommand",
            State = CopilotToolExecutionState.Completed,
            ProcessOperation = "shell",
            ProcessExitCode = 23,
        };
        Assert.True(contradictory.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Empty(contradictory.ProcessOperation);
        Assert.Null(contradictory.ProcessExitCode);
        Assert.False(contradictory.ProcessTimedOut);

        var foreignTool = new CopilotAgentTraceEntry
        {
            ToolName = "ReadLocalFile",
            State = CopilotToolExecutionState.Completed,
            ProcessOperation = "shell",
            ProcessExitCode = 0,
        };
        Assert.True(foreignTool.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Empty(foreignTool.ProcessOperation);
        Assert.Null(foreignTool.ProcessExitCode);
        Assert.False(foreignTool.ProcessTimedOut);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private CopilotAgentRequest CreateRequest(int? toolOutputTokenLimit = null) => new()
    {
        ConversationId = "foreground-process-evidence",
        WritableLocalRootPaths = [_root],
        SearchRootPaths = [_root],
        PreferredShell = CopilotShellKind.PowerShell,
        ToolOutputTokenLimitOverride = toolOutputTokenLimit,
    };

    private static CopilotAgentToolInput CreateValidationInput(string projectPath, string task) => new()
    {
        Path = projectPath,
        Arguments = new Dictionary<string, object?>
        {
            ["task"] = task,
            ["timeoutSeconds"] = 10,
        },
    };

    private CopilotAgentToolInput CreateShellInput(string command) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["workingDirectory"] = _root,
            ["shell"] = "powershell",
            ["timeoutSeconds"] = 10,
        },
    };

    private sealed class SequenceValidationRunner : ICopilotWorkspaceValidationRunner
    {
        private readonly Queue<CopilotWorkspaceValidationProcessResult> _results;

        public SequenceValidationRunner(params CopilotWorkspaceValidationProcessResult[] results)
        {
            _results = new Queue<CopilotWorkspaceValidationProcessResult>(results);
        }

        public Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class SequenceShellRunner : ICopilotShellProcessRunner
    {
        private readonly Queue<CopilotShellProcessResult> _results;

        public SequenceShellRunner(params CopilotShellProcessResult[] results)
        {
            _results = new Queue<CopilotShellProcessResult>(results);
        }

        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class ContainmentFailureShellRunner : ICopilotShellProcessRunner
    {
        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new CopilotProcessTreeContainmentException(
                "The process could not be assigned to a Windows Job Object.");
        }
    }
}
