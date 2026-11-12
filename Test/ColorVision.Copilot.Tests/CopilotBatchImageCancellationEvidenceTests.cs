using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System.Collections.Concurrent;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBatchImageCancellationEvidenceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecutionBoundaryKeepsOutputDirectoryWithoutClaimingLateWritesCompleted(bool timeout)
    {
        using var fixture = new Fixture();
        using var loader = new HeldImageLoader(Path.Combine(fixture.OutputDirectory, "first.tiff"));
        var inner = new CopilotConvertBatchImagesTool(new BatchImageProcessor([loader]));
        var tool = new HeldBatchTool(inner, timeout ? TimeSpan.FromSeconds(1) : TestTimeout);
        var input = Input(inner, [fixture.FirstSource, fixture.SecondSource], fixture.OutputDirectory);
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        using var cancellation = new CancellationTokenSource();
        var executor = new CopilotToolExecutor(hooks: []);
        var execution = executor.ExecuteAsync(new()
        {
            CallId = "batch-output-boundary", Tool = tool, AgentRequest = fixture.Request,
            ToolInput = input, FrameworkApprovalGranted = true,
        }, events.Enqueue, cancellation.Token);

        try
        {
            await loader.SecondImageEntered.Task.WaitAsync(TestTimeout);
            string firstOutput = Path.Combine(fixture.OutputDirectory, "first_2.tiff");
            Assert.True(File.Exists(firstOutput));
            CopilotToolExecutionOutcome outcome;
            if (timeout) outcome = await execution.WaitAsync(TestTimeout);
            else
            {
                cancellation.Cancel();
                var exception = await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(() => execution.WaitAsync(TestTimeout));
                outcome = exception.Outcome;
            }

            Assert.Equal(timeout ? CopilotToolExecutionState.TimedOut : CopilotToolExecutionState.Interrupted, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
            Assert.False(outcome.Execution.RetryEligible);
            Assert.Equal([fixture.OutputDirectory], outcome.Result.WorkspaceRecheckPaths);
            Assert.Contains(fixture.OutputDirectory, outcome.Result.Content, StringComparison.Ordinal);
            Assert.Contains("not proof of completed writes", outcome.Result.Content, StringComparison.Ordinal);
            Assert.False(tool.Settled.Task.IsCompleted);
            var terminal = Assert.Single(events, e => e.Type == CopilotAgentEventType.ToolResult);
            var accumulator = new CopilotTurnWorkspaceDiffAccumulator(fixture.Workspace);
            Assert.True(accumulator.Observe(terminal, out var snapshot));
            Assert.Empty(snapshot.Diff);
            Assert.Contains("1 个路径", snapshot.VerificationWarning, StringComparison.Ordinal);

            loader.Release.Set();
            var lateResult = await tool.Settled.Task.WaitAsync(TestTimeout);
            Assert.True(lateResult.Success, lateResult.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "second.tiff")));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(fixture.OutputDirectory, "first.tiff")));
            Assert.Single(events, e => e.Type == CopilotAgentEventType.ToolResult);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, terminal.ToolResult!.FailureKind);
            Assert.Equal(1, tool.Calls);
            Assert.Equal(1, tool.MetadataCalls);
        }
        finally
        {
            loader.Release.Set();
            if (tool.Calls > 0) await tool.Settled.Task.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public void EvidenceChecksAllSourceAndOutputScopesWithoutEnumeratingOrLoadingImages()
    {
        using var fixture = new Fixture();
        using var loader = new HeldImageLoader(Path.Combine(fixture.OutputDirectory, "first.tiff"));
        var tool = new CopilotConvertBatchImagesTool(new BatchImageProcessor([loader]));
        var evidence = (ICopilotWorkspaceMutationEvidenceSource)tool;
        string nestedOutput = Path.Combine(fixture.OutputDirectory, "not-created");
        Assert.Equal([nestedOutput], evidence.GetWorkspaceRecheckPaths(fixture.Request, Input(tool, [fixture.Workspace], nestedOutput)));
        Assert.Equal([fixture.Workspace], evidence.GetWorkspaceRecheckPaths(fixture.Request, Input(tool, [fixture.FirstSource, fixture.Workspace], null)));

        string outsideSource = Path.Combine(fixture.OutsideDirectory, "outside.png");
        File.WriteAllBytes(outsideSource, [0]);
        Assert.Empty(evidence.GetWorkspaceRecheckPaths(fixture.Request, Input(tool, [fixture.FirstSource, outsideSource], fixture.OutputDirectory)));
        Assert.Empty(evidence.GetWorkspaceRecheckPaths(fixture.Request, Input(tool, [fixture.FirstSource], fixture.OutsideDirectory)));
        Assert.Equal(0, loader.ExtensionsReads);
        Assert.Equal(0, loader.LoadCalls);
        Assert.False(Directory.Exists(nestedOutput));
    }

    private static CopilotAgentToolInput Input(CopilotConvertBatchImagesTool tool, string[] sources, string? outputDirectory)
    {
        var arguments = new Dictionary<string, object?> { ["sources"] = sources, ["format"] = "tiff" };
        if (outputDirectory != null) arguments["outputDirectory"] = outputDirectory;
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        return input;
    }

    private sealed class HeldBatchTool(CopilotConvertBatchImagesTool inner, TimeSpan timeout)
        : ICopilotFrameworkApprovedTool, ICopilotWorkspaceMutationEvidenceSource
    {
        public TaskCompletionSource<CopilotToolResult> Settled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public int MetadataCalls { get; private set; }
        public string Name => inner.Name;
        public string Description => inner.Description;
        public CopilotToolCapabilityDescriptor Capability => inner.Capability with { ExecutionTimeout = timeout };
        public CopilotToolInputSchema InputSchema => inner.InputSchema;
        public bool CanHandle(CopilotAgentRequest request) => true;
        public IReadOnlyList<string> GetWorkspaceRecheckPaths(CopilotAgentRequest request, CopilotAgentToolInput input)
        {
            MetadataCalls++;
            return ((ICopilotWorkspaceMutationEvidenceSource)inner).GetWorkspaceRecheckPaths(request, input);
        }
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(request, input, cancellationToken);
        public async Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            Calls++;
            try
            {
                // Model an operation whose native work outlives the caller boundary.
                var result = await ((ICopilotFrameworkApprovedTool)inner).ExecuteApprovedAsync(request, input, CancellationToken.None);
                Settled.TrySetResult(result);
                return result;
            }
            catch (Exception ex)
            {
                Settled.TrySetException(ex);
                throw;
            }
        }
    }

    private sealed class HeldImageLoader(string contestedOutput) : IBatchImageLoader, IDisposable
    {
        public TaskCompletionSource SecondImageEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public int ExtensionsReads { get; private set; }
        public int LoadCalls { get; private set; }
        public IReadOnlyCollection<string> Extensions
        {
            get { ExtensionsReads++; return [".png"]; }
        }
        public Mat Load(string filePath)
        {
            if (++LoadCalls == 1) File.WriteAllBytes(contestedOutput, [1, 2, 3]);
            else
            {
                SecondImageEntered.TrySetResult();
                Release.Wait();
            }
            return new Mat(2, 2, MatType.CV_8UC1, Scalar.All(42));
        }
        public void Dispose() => Release.Dispose();
    }

    private sealed class Fixture : IDisposable
    {
        private const string TemporaryPrefix = "CopilotBatchCancellation-";
        private readonly string _root = Directory.CreateTempSubdirectory(TemporaryPrefix).FullName;
        public string Workspace => Path.Combine(_root, "workspace");
        public string OutsideDirectory => Path.Combine(_root, "outside");
        public string OutputDirectory => Path.Combine(Workspace, "output");
        public string FirstSource => Path.Combine(Workspace, "first.png");
        public string SecondSource => Path.Combine(Workspace, "second.png");
        public CopilotAgentRequest Request => new()
        {
            UserText = "批量转换图像为TIFF", Mode = CopilotAgentMode.Auto,
            SearchRootPaths = [Workspace], ReadableLocalDirectoryPaths = [Workspace], WritableLocalRootPaths = [Workspace],
        };
        public Fixture()
        {
            Directory.CreateDirectory(OutputDirectory);
            Directory.CreateDirectory(OutsideDirectory);
            File.WriteAllBytes(FirstSource, [0]);
            File.WriteAllBytes(SecondSource, [0]);
            using Mat warmup = new(2, 2, MatType.CV_8UC1, Scalar.All(42));
        }
        public void Dispose()
        {
            string fullPath = Path.GetFullPath(_root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(fullPath), ignoreCase: true);
            Assert.StartsWith(TemporaryPrefix, Path.GetFileName(fullPath), StringComparison.Ordinal);
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
