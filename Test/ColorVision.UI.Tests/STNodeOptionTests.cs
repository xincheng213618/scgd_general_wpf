using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class STNodeOptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConnectedOptionQueriesReturnOppositeEndpointsInEitherHost(bool hosted)
    {
        StaTest.Run(() =>
        {
            using var editor = hosted ? new STNodeEditor() : null;
            using var container = new CVNodeContainer();
            var nodes = Enumerable.Range(0, 4).Select(_ => new OptionNode()).ToArray();
            foreach (OptionNode node in nodes)
            {
                node.Create();
                if (hosted) editor!.Nodes.Add(node);
                else container.Nodes.Add(node);
            }
            Assert.Equal(ConnectionStatus.Connected, nodes[0].Output.ConnectOption(nodes[2].Input, hosted));
            Assert.Equal(ConnectionStatus.Connected, nodes[1].Output.ConnectOption(nodes[2].Input, hosted));
            Assert.Equal(ConnectionStatus.Connected, nodes[0].Output.ConnectOption(nodes[3].Input, hosted));

            Assert.Equal(2, nodes[2].Input.GetConnectedOption().Count);
            Assert.True(nodes[2].Input.GetConnectedOption().ToHashSet().SetEquals([nodes[0].Output, nodes[1].Output]));
            Assert.Equal(2, nodes[0].Output.GetConnectedOption().Count);
            Assert.True(nodes[0].Output.GetConnectedOption().ToHashSet().SetEquals([nodes[2].Input, nodes[3].Input]));
            Assert.Empty(nodes[3].Output.GetConnectedOption());
        });
    }

    [Fact]
    public void CanceledDisconnectKeepsHubDataAndSuccessfulDisconnectReportsCorrectStatus()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var source = new OptionNode();
            var hub = new STNodeHub();
            var target = new OptionNode();
            foreach (STNode node in new STNode[] { source, hub, target }) { node.Create(); editor.Nodes.Add(node); }
            STNodeOption input = hub.GetAllInputOptions()[0];
            STNodeOption output = hub.GetAllOutputOptions()[0];
            source.Output.Data = "keep this result";
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(input));
            Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(target.Input));
            editor.ClearHistory();
            var sourceEvents = new List<ConnectionStatus>();
            var inputEvents = new List<ConnectionStatus>();
            var received = new List<object?>();
            source.Output.DisConnected += (_, args) => sourceEvents.Add(args.Status);
            input.DisConnected += (_, args) => inputEvents.Add(args.Status);
            target.Input.DataTransfer += (_, args) => received.Add(args.TargetOption.Data);
            bool cancel = true;
            editor.OptionDisConnecting += (_, args) => args.Continue = !cancel;

            Assert.Equal(ConnectionStatus.Reject, source.Output.DisConnectOption(input));
            Assert.Contains(input, source.Output.ConnectedOption);
            Assert.Contains(source.Output, input.ConnectedOption);
            Assert.Equal("keep this result", output.Data);
            Assert.Empty(sourceEvents);
            Assert.Empty(inputEvents);
            Assert.Empty(received);
            Assert.False(editor.CanUndo);

            cancel = false;
            Assert.Equal(ConnectionStatus.DisConnected, source.Output.DisConnectOption(input));
            Assert.Empty(source.Output.ConnectedOption);
            Assert.Empty(input.ConnectedOption);
            Assert.Equal(new[] { ConnectionStatus.DisConnected }, sourceEvents);
            Assert.Equal(new[] { ConnectionStatus.DisConnected }, inputEvents);
            Assert.Null(output.Data);
            Assert.Null(Assert.Single(received));
            Assert.True(editor.CanUndo);
        });
    }

    private sealed class OptionNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(string), false);
            Output = OutputOptions.Add("OUT", typeof(string), false);
        }
    }
}
