using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Runtime;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CommandLineScriptNodeTests
{
    private const string SerialNumber = "script-stop-test";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScriptDoesNotStartWhenFlowStopsBeforeWorkerDispatch(bool stopBeforeDispatch)
    {
        DirectoryInfo workDirectory = Directory.CreateTempSubdirectory("ColorVision-script-node-");
        string marker = Path.Combine(workDirectory.FullName, "script-started.txt");
        try
        {
            var script = new CommandLineScriptNode
            {
                ScriptType = CommandLineScriptType.Cmd,
                CommandText = "echo started>script-started.txt",
                WorkingDirectory = workDirectory.FullName,
            };
            using var graph = new ScriptFlow(script);
            if (stopBeforeDispatch)
            {
                // The run event is synchronous and precedes queuing the script worker.
                script.nodeRunEvent += (_, _) => graph.Start.Stop(SerialNumber);
            }

            Assert.True(graph.Start.TryStart(SerialNumber));
            var result = await graph.ScriptFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var outcome = await graph.FlowFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(!stopBeforeDispatch, File.Exists(marker));
            Assert.Equal(stopBeforeDispatch ? StatusTypeEnum.Canceled : StatusTypeEnum.Completed, outcome);
            Assert.Equal(stopBeforeDispatch ? FlowFailureKind.Canceled : null, result.FailureKind);
        }
        finally
        {
            File.Delete(marker);
            workDirectory.Delete();
        }
    }

    [Fact]
    public async Task ScriptResultIsDiscardedWhenTheProcessExitsAfterStop()
    {
        string gateName = $@"Local\ColorVision-ScriptNode-{Guid.NewGuid():N}";
        using var entered = new EventWaitHandle(false, EventResetMode.ManualReset, gateName + "-Entered");
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset, gateName + "-Release");
        DirectoryInfo workDirectory = Directory.CreateTempSubdirectory("ColorVision-script-node-");
        string scriptFile = Path.Combine(workDirectory.FullName, "hold.ps1");
        try
        {
            File.WriteAllText(scriptFile, $$"""
                $entered = [System.Threading.EventWaitHandle]::OpenExisting('{{gateName}}-Entered')
                $release = [System.Threading.EventWaitHandle]::OpenExisting('{{gateName}}-Release')
                $null = $entered.Set()
                if (-not $release.WaitOne(15000)) { exit 3 }
                Write-Output 'finished'
                """);
            var script = new ObservedScriptNode { ScriptType = CommandLineScriptType.PowerShell, ScriptFile = scriptFile };
            using var graph = new ScriptFlow(script);
            Assert.True(graph.Start.TryStart(SerialNumber));
            Assert.True(await Task.Run(() => entered.WaitOne(TimeSpan.FromSeconds(10))));
            CVStartCFC action = graph.Start.GetCFC(SerialNumber);

            graph.Start.Stop(SerialNumber);
            release.Set();
            var result = await graph.ScriptFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(SpinWait.SpinUntil(() => !script.HasPendingRun(SerialNumber), TimeSpan.FromSeconds(10)));

            Assert.Equal(StatusTypeEnum.Canceled, await graph.FlowFinished.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.DoesNotContain(action.Data.Keys, key => key.EndsWith(".Result", StringComparison.Ordinal));
            Assert.Equal(1, graph.DownstreamTransfers); // Only the stop notification reaches downstream.
            Assert.Equal(FlowFailureKind.Canceled, result.FailureKind);
        }
        finally
        {
            release.Set();
            File.Delete(scriptFile);
            workDirectory.Delete();
        }
    }

    [Fact]
    public async Task StopFromScriptCompletionObserverPreventsDownstreamTransfer()
    {
        var script = new ObservedScriptNode { ScriptType = CommandLineScriptType.Cmd, CommandText = "exit /b 0" };
        using var graph = new ScriptFlow(script);
        script.nodeEndEvent += (_, _) => graph.Start.Stop(SerialNumber);

        Assert.True(graph.Start.TryStart(SerialNumber));
        var result = await graph.ScriptFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(StatusTypeEnum.Canceled, await graph.FlowFinished.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(SpinWait.SpinUntil(() => !script.HasPendingRun(SerialNumber), TimeSpan.FromSeconds(10)));

        Assert.Equal(0, result.RecvStatusCode);
        Assert.Equal(1, graph.DownstreamTransfers);
    }

    private sealed class ObservedScriptNode : CommandLineScriptNode
    {
        public bool HasPendingRun(string serialNumber) => m_trans_action.ContainsKey(serialNumber);
    }

    private sealed class ScriptFlow : IDisposable
    {
        private readonly CVNodeContainer graph = new();
        private int downstreamTransfers;
        public HeadlessTestStartNode Start { get; } = new();
        public int DownstreamTransfers => Volatile.Read(ref downstreamTransfers);
        public TaskCompletionSource<StatusTypeEnum> FlowFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<FlowEngineNodeEndEventArgs> ScriptFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ScriptFlow(CommandLineScriptNode script)
        {
            var end = new CVEndNode();
            foreach (STNode node in new STNode[] { Start, script, end })
            {
                node.Create();
                graph.Nodes.Add(node);
            }
            Assert.Equal(ConnectionStatus.Connected, Start.m_op_start.ConnectOption(script.GetAllInputOptions()[0], isOwnerOfOwner: false));
            Assert.Equal(ConnectionStatus.Connected, script.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, isOwnerOfOwner: false));
            end.m_in_start.DataTransfer += (_, args) =>
            {
                if (args.TargetOption.Data is CVStartCFC)
                    Interlocked.Increment(ref downstreamTransfers);
            };
            Start.Finished += (_, args) => FlowFinished.TrySetResult(args.Status);
            script.nodeEndEvent += (_, args) => ScriptFinished.TrySetResult(args);
        }

        public void Dispose() => graph.Dispose();
    }
}
