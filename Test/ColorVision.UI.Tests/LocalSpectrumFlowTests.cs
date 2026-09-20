using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.Spectrum;
using FlowEngineLib.Start;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class LocalSpectrumFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpectrumForwardingCompletesThroughLocalExecutionWithoutPublishing(bool fail)
    {
        StaTest.Run(() =>
        {
            var previousPredicate = FlowLocalExecution.CanExecuteLocally;
            var previousFactory = FlowLocalExecution.CreateForNode;
            try
            {
                FlowLocalExecution.CanExecuteLocally = node => node is SpectrumNode;
                int completed = 0, disposed = 0;
                FlowLocalExecution.CreateForNode = (_, request) =>
                {
                    var parameters = Assert.IsType<SpectrumParamData>(request.Data);
                    Assert.Equal(42, parameters.IntegralTime);
                    Assert.Equal(3, parameters.NumberOfAverage);
                    return new FakeExecution(fail, () => completed++, () => disposed++);
                };
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                var spectrum = new SpectrumNode();
                var end = new CVEndNode();
                foreach (var node in new STNode[] { start, spectrum, end }) { node.Create(); container.Nodes.Add(node); }
                spectrum.Temp = 42;
                spectrum.AveNum = 3;
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(spectrum.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, spectrum.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                using var control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.False(spectrum.RequiresRemoteService);
                Assert.True(control.TryStartNode(start.NodeName, "local-spectrum-no-db"));
                var result = completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.Equal(fail ? StatusTypeEnum.Failed : StatusTypeEnum.Completed, result.Status);
                Assert.Equal(0, start.PublishCount);
                Assert.Equal(fail ? 0 : 1, completed);
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref disposed) == 1, TimeSpan.FromSeconds(5)));
            }
            finally { FlowLocalExecution.CanExecuteLocally = previousPredicate; FlowLocalExecution.CreateForNode = previousFactory; }
        });
    }

    [Fact]
    public void DisconnectedMqttStartAcceptsForwardedSpectrumAndRejectsRemoteSpectrum()
    {
        StaTest.Run(() =>
        {
            var previous = FlowLocalExecution.CanExecuteLocally;
            try
            {
                bool local = true;
                FlowLocalExecution.CanExecuteLocally = node => node is SpectrumNode && local;
                using var editor = new STNodeEditor();
                using var control = new FlowEngineControl(editor, false, new FlowNodeManager());
                var start = new OfflineStart { NodeName = "offline-spectrum" };
                var node = new SpectrumNode();
                var end = new CVEndNode();
                start.Create(); node.Create(); end.Create();
                editor.Nodes.Add(start); editor.Nodes.Add(node); editor.Nodes.Add(end);
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0]));
                Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start));
                Assert.True(control.CanStartNode(start.NodeName));
                local = false;
                Assert.False(control.CanStartNode(start.NodeName));
            }
            finally { FlowLocalExecution.CanExecuteLocally = previous; }
        });
    }

    [Fact]
    public void LocalSpectrumNodeHasFlowPortsAndStoresDeviceAndParameters()
    {
        StaTest.Run(() =>
        {
            var node = new LocalSpectrumNode { DeviceCode = "LOCAL.SP", IntegralTime = 250, NumberOfAverage = 5, AutoConnect = false };
            node.Create();
            Assert.Single(node.GetAllInputOptions());
            Assert.Single(node.GetAllOutputOptions());
            string saved = System.Text.Encoding.UTF8.GetString(node.GetSaveData());
            Assert.Contains("LOCAL.SP", saved);
            Assert.Contains(nameof(LocalSpectrumNode.IntegralTime), saved);
            Assert.Contains(nameof(LocalSpectrumNode.AutoConnect), saved);
        });
    }

    private sealed class OfflineStart : MQTTStartNode
    {
        public override Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override void DoPublishStatus(string message) { }
    }
    private sealed class FakeExecution(bool fail, Action complete, Action dispose) : FlowLocalExecution
    {
        public override void Execute() { if (fail) throw new InvalidOperationException("synthetic native error"); }
        public override object Complete(CVStartCFC action) { Assert.False(action.PersistResults); complete(); return new { MasterId = 0, MasterResultType = 300 }; }
        public override void Dispose() => dispose();
    }
}
