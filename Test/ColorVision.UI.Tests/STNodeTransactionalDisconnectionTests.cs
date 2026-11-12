using ST.Library.UI.NodeEditor;
using System.Collections.Specialized;

namespace ColorVision.UI.Tests;

public sealed class STNodeTransactionalDisconnectionTests
{
    [Theory]
    [InlineData("editor-pre")]
    [InlineData("option-pre")]
    [InlineData("input-data")]
    [InlineData("input-post")]
    [InlineData("output-post")]
    [InlineData("editor-post")]
    [InlineData("history")]
    [InlineData("undo-notification")]
    [InlineData("redo-notification")]
    [InlineData("after-capture")]
    public void CallbackAndCommitFailuresRestoreTheExactEdgeAndHistoryWithoutReloadingNodes(string boundary)
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph(retainHistory: true);
            var editor = graph.Editor;
            var undo = editor.UndoHistory.ToArray();
            var redo = editor.RedoHistory.ToArray();
            var modified = editor.IsModified;
            var nodes = editor.Nodes.Cast<STNode>().ToArray();
            int callbacks = 0;
            var notifications = new List<NotifyCollectionChangedAction>();
            void Fail() { callbacks++; throw new ControlledFailureException(); }
            STNodeEditorOptionEventHandler editorHandler = (_, _) => Fail();
            STNodeOptionEventHandler optionHandler = (_, _) => Fail();
            EventHandler historyHandler = (_, _) => Fail();
            NotifyCollectionChangedEventHandler collectionHandler = (_, args) => { notifications.Add(args.Action); Fail(); };
            switch (boundary)
            {
                case "editor-pre": editor.OptionDisConnecting += editorHandler; break;
                case "option-pre": graph.Source.Output.DisConnecting += optionHandler; break;
                case "input-data": graph.Target.Input.DataTransfer += optionHandler; break;
                case "input-post": graph.Target.Input.DisConnected += optionHandler; break;
                case "output-post": graph.Source.Output.DisConnected += optionHandler; break;
                case "editor-post": editor.OptionDisConnected += editorHandler; break;
                case "history": editor.HistoryChanged += historyHandler; break;
                case "undo-notification": ((INotifyCollectionChanged)editor.UndoHistory).CollectionChanged += collectionHandler; break;
                case "redo-notification": ((INotifyCollectionChanged)editor.RedoHistory).CollectionChanged += collectionHandler; break;
            }
            try
            {
                Assert.Throws<ControlledFailureException>(() => graph.Source.Output.DisConnectOptionTransactional(graph.Target.Input,
                    afterDisconnection: boundary == "after-capture" ? Fail : null));
                if (boundary == "undo-notification")
                    Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Reset }, notifications);
                else if (boundary == "redo-notification")
                    Assert.Equal(new[] { NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Reset }, notifications);
                else
                    Assert.Equal(1, callbacks); // Node and HistoryChanged business observers are not replayed.
            }
            finally
            {
                editor.OptionDisConnecting -= editorHandler;
                graph.Source.Output.DisConnecting -= optionHandler;
                graph.Target.Input.DataTransfer -= optionHandler;
                graph.Target.Input.DisConnected -= optionHandler;
                graph.Source.Output.DisConnected -= optionHandler;
                editor.OptionDisConnected -= editorHandler;
                editor.HistoryChanged -= historyHandler;
                ((INotifyCollectionChanged)editor.UndoHistory).CollectionChanged -= collectionHandler;
                ((INotifyCollectionChanged)editor.RedoHistory).CollectionChanged -= collectionHandler;
            }

            graph.AssertOriginalTopology();
            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
            Assert.Equal(undo, editor.UndoHistory.ToArray());
            Assert.Equal(redo, editor.RedoHistory.ToArray());
            Assert.Equal(modified, editor.IsModified);
            Assert.True(editor.CanUndo);
            Assert.True(editor.CanRedo);
            Assert.All(new[] { graph.Source, graph.Target, graph.Other }, node => Assert.Equal(0, node.LoadCalls));

            // The failed edit must release pending history and allow the next exact edit to commit and undo normally.
            Assert.Equal(ConnectionStatus.DisConnected, graph.Source.Output.DisConnectOptionTransactional(graph.Target.Input));
            Assert.DoesNotContain(graph.Target.Input, graph.Source.Output.ConnectedOption);
            Assert.Contains(graph.Other.Input, graph.Source.Output.ConnectedOption);
            Assert.Equal(undo.Length + 1, editor.UndoHistory.Count);
            Assert.Empty(editor.RedoHistory);
            editor.Undo();
            graph.AssertOriginalTopology();
        });
    }

    [Fact]
    public void AHistoryObserverFailurePublishesRollbackResetAfterRestoringFactsAndBeforeReleasingTheScope()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph(retainHistory: true);
            var editor = graph.Editor;
            var original = editor.UndoHistory.ToArray();
            var cached = original.ToList();
            var notifications = new List<NotifyCollectionChangedAction>();
            int historyCalls = 0;
            NotifyCollectionChangedEventHandler cacheObserver = (_, args) =>
            {
                notifications.Add(args.Action);
                if (args.Action == NotifyCollectionChangedAction.Add)
                    cached.InsertRange(args.NewStartingIndex, args.NewItems!.Cast<STNodeEditHistoryEntry>());
                else if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    Assert.False(editor.CanUndo);
                    Assert.False(editor.CanRedo);
                    Assert.Throws<InvalidOperationException>(() => editor.ClearHistory());
                    graph.AssertOriginalTopology();
                    cached.Clear();
                    cached.AddRange(editor.UndoHistory);
                }
            };
            EventHandler failAfterCommit = (_, _) => { historyCalls++; throw new ControlledFailureException(); };
            ((INotifyCollectionChanged)editor.UndoHistory).CollectionChanged += cacheObserver;
            editor.HistoryChanged += failAfterCommit;
            try { Assert.Throws<ControlledFailureException>(() => graph.Source.Output.DisConnectOptionTransactional(graph.Target.Input)); }
            finally
            {
                ((INotifyCollectionChanged)editor.UndoHistory).CollectionChanged -= cacheObserver;
                editor.HistoryChanged -= failAfterCommit;
            }
            Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Reset }, notifications);
            Assert.Equal(original, cached);
            Assert.Equal(original, editor.UndoHistory.ToArray());
            Assert.Equal(1, historyCalls);
            Assert.True(editor.CanUndo);
            Assert.True(editor.CanRedo);
            graph.AssertOriginalTopology();
        });
    }

    [Fact]
    public void VetoKeepsTargetDataAndOtherEdgesAndDoesNotPublishAnUndoEntry()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph(retainHistory: false);
            graph.Source.Output.Data = "Retained upstream data";
            graph.Target.Input.Data = "Retained upstream data";
            int disconnected = 0;
            STNodeEditorOptionEventHandler veto = (_, args) => args.Continue = false;
            graph.Editor.OptionDisConnecting += veto;
            graph.Target.Input.DisConnected += (_, _) => disconnected++;
            try
            {
                Assert.Equal(ConnectionStatus.Reject, graph.Source.Output.DisConnectOptionTransactional(graph.Target.Input));
                graph.AssertOriginalTopology();
                Assert.Equal("Retained upstream data", graph.Target.Input.Data);
                Assert.Equal(0, disconnected);
                Assert.Empty(graph.Editor.UndoHistory);
                Assert.Empty(graph.Editor.RedoHistory);
                Assert.False(graph.Editor.IsModified);
            }
            finally { graph.Editor.OptionDisConnecting -= veto; }
            Assert.Equal(ConnectionStatus.DisConnected, graph.Source.Output.DisConnectOptionTransactional(graph.Target.Input));
            Assert.Equal(1, disconnected);
            Assert.Single(graph.Editor.UndoHistory);
        });
    }

    [Fact]
    public void ARejectedNestedAtomicEditDoesNotClobberTheLegacyConnectionsPendingUndo()
    {
        WpfTestHost.Invoke(() =>
        {
            using var graph = new Graph(retainHistory: false);
            int nestedRejections = 0;
            STNodeEditorOptionEventHandler nested = (_, _) =>
            {
                Assert.Throws<InvalidOperationException>(() => graph.Source.Output.DisConnectOptionTransactional(graph.Other.Input));
                nestedRejections++;
            };
            graph.Editor.OptionDisConnecting += nested;
            try { Assert.Equal(ConnectionStatus.DisConnected, graph.Source.Output.DisConnectOption(graph.Target.Input)); }
            finally { graph.Editor.OptionDisConnecting -= nested; }
            Assert.Equal(1, nestedRejections);
            Assert.DoesNotContain(graph.Target.Input, graph.Source.Output.ConnectedOption);
            Assert.Contains(graph.Other.Input, graph.Source.Output.ConnectedOption);
            Assert.Single(graph.Editor.UndoHistory);
            graph.Editor.Undo();
            graph.AssertOriginalTopology();
        });
    }

    private sealed class ControlledFailureException : InvalidOperationException { }

    private sealed class Graph : IDisposable
    {
        public STNodeEditor Editor { get; } = new() { EnableHistory = true };
        public ProbeNode Source { get; } = new();
        public ProbeNode Target { get; } = new();
        public ProbeNode Other { get; } = new();

        public Graph(bool retainHistory)
        {
            foreach (var node in new[] { Source, Target, Other }) { node.Create(); Editor.Nodes.Add(node); }
            Assert.Equal(ConnectionStatus.Connected, Source.Output.ConnectOption(Target.Input));
            Assert.Equal(ConnectionStatus.Connected, Source.Output.ConnectOption(Other.Input));
            Editor.ClearHistory();
            if (retainHistory)
            {
                Editor.ExecuteEditTransaction("Retain one undo", () => Source.Left += 17);
                Editor.ExecuteEditTransaction("Retain one redo", () => Other.Top += 23);
                Editor.Undo();
                Assert.Single(Editor.UndoHistory);
                Assert.Single(Editor.RedoHistory);
            }
        }

        public void AssertOriginalTopology()
        {
            Assert.Equal(2, Source.Output.ConnectionCount);
            Assert.Contains(Target.Input, Source.Output.ConnectedOption);
            Assert.Contains(Other.Input, Source.Output.ConnectedOption);
            Assert.Same(Source.Output, Assert.Single(Target.Input.ConnectedOption));
            Assert.Same(Source.Output, Assert.Single(Other.Input.ConnectedOption));
        }

        public void Dispose() => Editor.Dispose();
    }

    private sealed class ProbeNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        public int LoadCalls { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(string), false);
            Output = OutputOptions.Add("OUT", typeof(string), false);
        }
        public override void OnLoadNode(Dictionary<string, byte[]> data) { LoadCalls++; base.OnLoadNode(data); }
    }
}
