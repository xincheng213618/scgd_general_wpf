#pragma warning disable CA1707
using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public class STNodeEditorEditingTests
{
    [Fact]
    public void AddUndoRedo_PreservesNodeObjectAndIdentity()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var node = CreateNode<EditorHistorySourceNode>();
            Guid nodeId = node.Guid;

            editor.Nodes.Add(node);

            Assert.True(editor.CanUndo);
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Empty(editor.Nodes.Cast<STNode>());
            Assert.True(editor.CanRedo);

            editor.Redo();
            Assert.Single(editor.Nodes.Cast<STNode>());
            Assert.Same(node, editor.Nodes[0]);
            Assert.Equal(nodeId, editor.Nodes[0].Guid);
        });
    }

    [Fact]
    public void CompoundMove_IsOneHistoryEntryAndReturnsToSavePoint()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var first = CreateNode<EditorHistorySourceNode>();
            var second = CreateNode<EditorHistorySinkNode>();
            first.Location = new System.Drawing.Point(20, 30);
            second.Location = new System.Drawing.Point(80, 90);
            editor.Nodes.AddRange(new STNode[] { first, second });
            editor.AddSelectedNode(first);
            editor.AddSelectedNode(second);
            editor.ClearHistory();
            editor.MarkSaved();

            Assert.True(editor.MoveSelectedNodes(40, -10));

            Assert.Single(editor.UndoHistory);
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Equal(new System.Drawing.Point(20, 30), first.Location);
            Assert.Equal(new System.Drawing.Point(80, 90), second.Location);
            Assert.False(editor.IsModified);

            editor.Redo();
            Assert.Equal(new System.Drawing.Point(60, 20), first.Location);
            Assert.Equal(new System.Drawing.Point(120, 80), second.Location);
            Assert.True(editor.IsModified);
        });
    }

    [Fact]
    public void ConnectionUndoRedo_UsesModelConnectionsBeforeRendering()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var sink = CreateNode<EditorHistorySinkNode>();
            editor.Nodes.AddRange(new STNode[] { source, sink });
            editor.ClearHistory();

            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));

            Assert.Single(editor.GetConnections());
            Assert.Single(editor.UndoHistory);
            editor.Undo();
            Assert.Empty(editor.GetConnections());
            editor.Redo();
            Assert.Single(editor.GetConnections());
        });
    }

    [Fact]
    public void DynamicHubDisconnect_UndoRedoRestoresPortTopologyAndEdge()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var hub = CreateNode<STNodeInHub>();
            editor.Nodes.AddRange(new STNode[] { source, hub });
            STNodeOption hubInput = Assert.Single(hub.GetAllInputOptions());
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(hubInput));
            Assert.Equal(2, hub.GetAllInputOptions().Length);
            editor.ClearHistory();

            Assert.Equal(ConnectionStatus.DisConnected, source.Output.DisConnectOption(hubInput));

            Assert.Empty(editor.GetConnections());
            Assert.Single(hub.GetAllInputOptions());
            Assert.Single(editor.UndoHistory);

            editor.Undo();
            Assert.Single(editor.GetConnections());
            Assert.Equal(2, hub.GetAllInputOptions().Length);
            Assert.Equal(typeof(string), editor.GetConnections()[0].Input.DataType);

            editor.Redo();
            Assert.Empty(editor.GetConnections());
            Assert.Single(hub.GetAllInputOptions());
        });
    }

    [Fact]
    public void DynamicHubDisconnect_FirstOfTwoEdges_UndoRestoresOriginalPorts()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var first = CreateNode<EditorHistorySourceNode>();
            var second = CreateNode<EditorHistorySourceNode>();
            var hub = CreateNode<STNodeInHub>();
            editor.Nodes.AddRange(new STNode[] { first, second, hub });
            STNodeOption firstInput = hub.GetAllInputOptions()[0];
            Assert.Equal(ConnectionStatus.Connected, first.Output.ConnectOption(firstInput));
            STNodeOption secondInput = hub.GetAllInputOptions()[1];
            Assert.Equal(ConnectionStatus.Connected, second.Output.ConnectOption(secondInput));
            Assert.Equal(3, hub.GetAllInputOptions().Length);
            editor.ClearHistory();

            Assert.Equal(ConnectionStatus.DisConnected, first.Output.DisConnectOption(firstInput));
            Assert.Equal(2, hub.GetAllInputOptions().Length);

            editor.Undo();

            ConnectionInfo[] restored = editor.GetConnections();
            Assert.Equal(2, restored.Length);
            Assert.Equal(3, hub.GetAllInputOptions().Length);
            Assert.Same(firstInput, Assert.Single(restored, connection => ReferenceEquals(connection.Output, first.Output)).Input);
            Assert.Same(secondInput, Assert.Single(restored, connection => ReferenceEquals(connection.Output, second.Output)).Input);
            Assert.NotSame(restored[0].Input, restored[1].Input);

            editor.Redo();
            Assert.Single(editor.GetConnections());
            Assert.Same(secondInput, editor.GetConnections()[0].Input);
            Assert.Equal(2, hub.GetAllInputOptions().Length);
        });
    }

    [Fact]
    public void DeleteMultiConnectedDynamicHub_UndoRestoresOriginalPortsAndEdges()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var first = CreateNode<EditorHistorySourceNode>();
            var second = CreateNode<EditorHistorySourceNode>();
            var hub = CreateNode<STNodeInHub>();
            editor.Nodes.AddRange(new STNode[] { first, second, hub });
            STNodeOption firstInput = hub.GetAllInputOptions()[0];
            Assert.Equal(ConnectionStatus.Connected, first.Output.ConnectOption(firstInput));
            STNodeOption secondInput = hub.GetAllInputOptions()[1];
            Assert.Equal(ConnectionStatus.Connected, second.Output.ConnectOption(secondInput));
            editor.ClearHistory();
            editor.AddSelectedNode(hub);

            Assert.True(editor.DeleteSelectedNodes());
            Assert.Equal(2, editor.Nodes.Count);
            Assert.Empty(editor.GetConnections());

            editor.Undo();

            Assert.Equal(3, editor.Nodes.Count);
            Assert.Same(hub, editor.Nodes[2]);
            Assert.Equal(3, hub.GetAllInputOptions().Length);
            ConnectionInfo[] restored = editor.GetConnections();
            Assert.Equal(2, restored.Length);
            Assert.Same(firstInput, Assert.Single(restored, connection => ReferenceEquals(connection.Output, first.Output)).Input);
            Assert.Same(secondInput, Assert.Single(restored, connection => ReferenceEquals(connection.Output, second.Output)).Input);

            editor.Redo();

            Assert.Equal(2, editor.Nodes.Count);
            Assert.Empty(editor.GetConnections());

            editor.Undo();

            Assert.Equal(3, editor.Nodes.Count);
            Assert.Equal(2, editor.GetConnections().Length);
            Assert.Same(firstInput, Assert.Single(editor.GetConnections(), connection => ReferenceEquals(connection.Output, first.Output)).Input);
            Assert.Same(secondInput, Assert.Single(editor.GetConnections(), connection => ReferenceEquals(connection.Output, second.Output)).Input);
        });
    }

    [Fact]
    public void DeleteConnectedNode_UndoRestoresNodeOrderIdentityAndEdge()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var sink = CreateNode<EditorHistorySinkNode>();
            editor.Nodes.AddRange(new STNode[] { source, sink });
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));
            editor.ClearHistory();
            editor.AddSelectedNode(source);
            Guid sourceId = source.Guid;

            Assert.True(editor.DeleteSelectedNodes());

            Assert.Single(editor.Nodes.Cast<STNode>());
            Assert.Empty(editor.GetConnections());
            Assert.Single(editor.UndoHistory);

            editor.Undo();
            Assert.Equal(2, editor.Nodes.Count);
            Assert.Same(source, editor.Nodes[0]);
            Assert.Equal(sourceId, source.Guid);
            Assert.Single(editor.GetConnections());

            editor.Redo();
            Assert.Single(editor.Nodes.Cast<STNode>());
            Assert.Empty(editor.GetConnections());
        });
    }

    [Fact]
    public void DeleteLockedConnectedNode_DoesNotLeaveDanglingEdge()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var sink = CreateNode<EditorHistorySinkNode>();
            editor.Nodes.AddRange(new STNode[] { source, sink });
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));
            source.LockOption = true;
            sink.LockOption = true;
            editor.ClearHistory();
            editor.AddSelectedNode(source);

            editor.DeleteSelectedNodes();

            Assert.Empty(editor.GetConnections());
            Assert.Equal(0, source.Output.ConnectionCount);
            Assert.Equal(0, sink.Input.ConnectionCount);
            editor.Undo();
            Assert.Single(editor.GetConnections());
        });
    }

    [Fact]
    public void ImportSelection_RegeneratesIdentityAndUndoRedoKeepsIt()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var sink = CreateNode<EditorHistorySinkNode>();
            source.Location = new System.Drawing.Point(10, 20);
            sink.Location = new System.Drawing.Point(200, 20);
            editor.Nodes.AddRange(new STNode[] { source, sink });
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));
            editor.AddSelectedNode(source);
            editor.AddSelectedNode(sink);
            byte[] data = editor.GetSelectedNodesData();
            editor.ClearHistory();

            IReadOnlyList<STNode> imported = editor.ImportSelectionData(data, new System.Drawing.Point(400, 300));
            Guid[] importedIds = imported.Select(node => node.Guid).ToArray();

            Assert.Equal(2, imported.Count);
            Assert.Equal(4, editor.Nodes.Count);
            Assert.DoesNotContain(source.Guid, importedIds);
            Assert.DoesNotContain(sink.Guid, importedIds);
            Assert.Equal(importedIds.Length, importedIds.Distinct().Count());
            Assert.Equal(2, editor.GetConnections().Length);
            Assert.Single(editor.UndoHistory);

            editor.Undo();
            Assert.Equal(2, editor.Nodes.Count);
            Assert.Single(editor.GetConnections());
            editor.Redo();
            Assert.Equal(4, editor.Nodes.Count);
            Assert.Equal(importedIds, imported.Select(node => node.Guid).ToArray());
            Assert.Equal(2, editor.GetConnections().Length);
        });
    }

    [Fact]
    public void ImportCanvasAsModule_IsOneAtomicEditWithFreshIdentity()
    {
        RunInSta(() =>
        {
            using var sourceEditor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var sink = CreateNode<EditorHistorySinkNode>();
            source.Location = new System.Drawing.Point(20, 40);
            sink.Location = new System.Drawing.Point(220, 140);
            sourceEditor.Nodes.AddRange(new STNode[] { source, sink });
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));
            byte[] canvas = sourceEditor.GetCanvasData();

            using var targetEditor = CreateEditor();
            IReadOnlyList<STNode> imported = targetEditor.ImportCanvasAsModule(
                canvas,
                new System.Drawing.Point(500, 300));
            Guid[] importedIds = imported.Select(node => node.Guid).ToArray();

            Assert.Equal(2, imported.Count);
            Assert.Equal(500, imported.Min(node => node.Left));
            Assert.Equal(300, imported.Min(node => node.Top));
            Assert.DoesNotContain(source.Guid, importedIds);
            Assert.DoesNotContain(sink.Guid, importedIds);
            Assert.Single(targetEditor.GetConnections());
            Assert.Single(targetEditor.UndoHistory);

            targetEditor.Undo();
            Assert.Empty(targetEditor.Nodes.Cast<STNode>());
            Assert.Empty(targetEditor.GetConnections());

            targetEditor.Redo();
            Assert.Equal(2, targetEditor.Nodes.Count);
            Assert.Equal(importedIds, imported.Select(node => node.Guid).ToArray());
            Assert.Single(targetEditor.GetConnections());
        });
    }

    [Fact]
    public void ImportSelection_CorruptDataIsAtomic()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(source);
            editor.AddSelectedNode(source);
            byte[] data = editor.GetSelectedNodesData();
            byte[] truncated = data.Take(data.Length - 3).ToArray();
            editor.ClearHistory();

            Assert.ThrowsAny<InvalidDataException>(() =>
                editor.ImportSelectionData(truncated, new System.Drawing.Point(100, 100)));

            Assert.Single(editor.Nodes.Cast<STNode>());
            Assert.Same(source, editor.Nodes[0]);
            Assert.Null(editor.ActiveNode);
            Assert.Contains(source, editor.GetSelectedNode());
            Assert.False(editor.CanUndo);
        });
    }

    [Fact]
    public void ImportSelection_InvalidNodeOrConnectionPayloadIsAtomic()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(source);
            editor.AddSelectedNode(source);
            editor.SetActiveNode(source);
            editor.ClearHistory();

            byte[] malformedNode = CreateSelectionPayload(new[] { new byte[] { 1, (byte)'X' } });
            byte[] invalidConnection = CreateSelectionPayload(
                new[] { source.GetSaveData() },
                new[] { 10L });

            foreach (byte[] payload in new[] { malformedNode, invalidConnection })
            {
                Assert.ThrowsAny<InvalidDataException>(() =>
                    editor.ImportSelectionData(payload, new System.Drawing.Point(100, 100)));
                Assert.Single(editor.Nodes.Cast<STNode>());
                Assert.Same(source, editor.Nodes[0]);
                Assert.Same(source, editor.ActiveNode);
                Assert.Contains(source, editor.GetSelectedNode());
                Assert.False(editor.CanUndo);
            }
        });
    }

    [Fact]
    public void SaveLoadBeforeRender_PreservesConnectionAndInstanceGuids()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var source = CreateNode<EditorHistorySourceNode>();
            var sink = CreateNode<EditorHistorySinkNode>();
            editor.Nodes.AddRange(new STNode[] { source, sink });
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(sink.Input));

            byte[] canvas = editor.GetCanvasData();
            using var restored = CreateEditor();
            restored.LoadCanvas(canvas);

            Assert.Equal(2, restored.Nodes.Count);
            Assert.Single(restored.GetConnections());
            Assert.Equal(new[] { source.Guid, sink.Guid }, restored.Nodes.Cast<STNode>().Select(node => node.Guid).ToArray());
            Assert.False(restored.CanUndo);
        });
    }

    [Fact]
    public void LoadCanvas_LateCorruptionKeepsCurrentEditorGraphAndSelection()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var existing = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(existing);
            editor.AddSelectedNode(existing);
            editor.SetActiveNode(existing);
            editor.ClearHistory();

            using var replacementEditor = CreateEditor();
            replacementEditor.Nodes.Add(
                CreateNode<EditorHistorySinkNode>());
            byte[] corruptCanvas = replacementEditor.GetCanvasData();
            Array.Resize(ref corruptCanvas, corruptCanvas.Length - 3);

            Assert.Throws<InvalidDataException>(() =>
                editor.LoadCanvas(corruptCanvas));

            Assert.Single(editor.Nodes.Cast<STNode>());
            Assert.Same(existing, editor.Nodes[0]);
            Assert.Same(existing, editor.ActiveNode);
            Assert.Contains(existing, editor.GetSelectedNode());
            Assert.False(editor.CanUndo);
        });
    }

    [Fact]
    public void NodePropertyChange_IsUndoableAndCoalescesTyping()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var node = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(node);
            editor.ClearHistory();
            editor.MarkSaved();
            string originalTitle = node.Title;

            node.Title = "A";
            node.Title = "AB";
            node.Title = "ABC";

            Assert.Single(editor.UndoHistory);
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Equal(originalTitle, node.Title);
            Assert.False(editor.IsModified);
            editor.Redo();
            Assert.Equal("ABC", node.Title);
        });
    }

    [Fact]
    public void PropertyEditAfterSave_DoesNotMergeAcrossSavePoint()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var node = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(node);
            editor.ClearHistory();

            node.Title = "Saved title";
            editor.MarkSaved();
            node.Title = "Changed title";

            Assert.Equal(2, editor.UndoHistory.Count);
            Assert.True(editor.IsModified);
            editor.Undo();
            Assert.Equal("Saved title", node.Title);
            Assert.False(editor.IsModified);
        });
    }

    [Fact]
    public void ExecuteEditTransaction_ExceptionRollsBackStateAndKeepsSavePoint()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var node = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(node);
            editor.ClearHistory();
            editor.MarkSaved();
            string originalTitle = node.Title;
            System.Drawing.Point originalLocation = node.Location;

            Assert.Throws<InvalidOperationException>(() =>
                editor.ExecuteEditTransaction("失败编辑", () =>
                {
                    node.Title = "Changed";
                    node.Location = new System.Drawing.Point(300, 200);
                    throw new InvalidOperationException("expected");
                }));

            Assert.Equal(originalTitle, node.Title);
            Assert.Equal(originalLocation, node.Location);
            Assert.False(editor.IsModified);
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);
        });
    }

    [Fact]
    public void ExecuteEditTransaction_ExceptionRollsBackNodesAndConnections()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var existing = CreateNode<EditorHistorySourceNode>();
            editor.Nodes.Add(existing);
            editor.ClearHistory();
            editor.MarkSaved();
            var added = CreateNode<EditorHistorySinkNode>();

            Assert.Throws<InvalidOperationException>(() =>
                editor.ExecuteEditTransaction("失败结构编辑", () =>
                {
                    editor.Nodes.Add(added);
                    Assert.Equal(ConnectionStatus.Connected, existing.Output.ConnectOption(added.Input));
                    throw new InvalidOperationException("expected");
                }));

            Assert.Single(editor.Nodes.Cast<STNode>());
            Assert.Same(existing, editor.Nodes[0]);
            Assert.Empty(editor.GetConnections());
            Assert.Equal(0, existing.Output.ConnectionCount);
            Assert.Equal(0, added.Input.ConnectionCount);
            Assert.False(editor.IsModified);
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);
        });
    }

    [Fact]
    public void ManualConfirmImport_KeepsNodeIdSynchronizedWithNewGuid()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var node = CreateNode<ManualConfirmNode>();
            editor.Nodes.Add(node);
            editor.AddSelectedNode(node);
            byte[] data = editor.GetSelectedNodesData();

            ManualConfirmNode imported = Assert.IsType<ManualConfirmNode>(
                Assert.Single(editor.ImportSelectionData(data, new System.Drawing.Point(300, 200))));

            Assert.NotEqual(node.Guid, imported.Guid);
            Assert.Equal(imported.Guid.ToString(), imported.NodeID);
        });
    }

    [Fact]
    public void EditorOwnsStandardReusableEditCommandBindings()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var commands = editor.CommandBindings
                .Cast<System.Windows.Input.CommandBinding>()
                .Select(binding => binding.Command)
                .ToArray();

            Assert.Contains(System.Windows.Input.ApplicationCommands.Undo, commands);
            Assert.Contains(System.Windows.Input.ApplicationCommands.Redo, commands);
            Assert.Contains(System.Windows.Input.ApplicationCommands.Cut, commands);
            Assert.Contains(System.Windows.Input.ApplicationCommands.Copy, commands);
            Assert.Contains(System.Windows.Input.ApplicationCommands.Paste, commands);
            Assert.Contains(System.Windows.Input.ApplicationCommands.Delete, commands);
            Assert.Contains(System.Windows.Input.ApplicationCommands.SelectAll, commands);
        });
    }

    private static STNodeEditor CreateEditor()
    {
        return new STNodeEditor
        {
            ClientSize = new System.Drawing.Size(800, 600),
            EnableHistory = true
        };
    }

    private static T CreateNode<T>() where T : STNode, new()
    {
        var node = new T();
        node.Create();
        return node;
    }

    private static byte[] CreateSelectionPayload(
        IReadOnlyList<byte[]> nodeData,
        IReadOnlyList<long>? connections = null)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        {
            WriteInt32(gzip, nodeData.Count);
            WriteInt32(gzip, 0);
            WriteInt32(gzip, 0);
            foreach (byte[] data in nodeData)
            {
                WriteInt32(gzip, data.Length);
                gzip.Write(data, 0, data.Length);
            }
            WriteInt32(gzip, connections?.Count ?? 0);
            if (connections != null)
            {
                foreach (long connection in connections)
                {
                    byte[] bytes = BitConverter.GetBytes(connection);
                    gzip.Write(bytes, 0, bytes.Length);
                }
            }
        }
        return stream.ToArray();
    }

    private static void WriteInt32(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}

public sealed class EditorHistorySourceNode : STNode
{
    public STNodeOption Output { get; private set; } = STNodeOption.Empty;

    protected override void OnCreate()
    {
        base.OnCreate();
        Output = OutputOptions.Add("Output", typeof(string), bSingle: false);
    }
}

public sealed class EditorHistorySinkNode : STNode
{
    public STNodeOption Input { get; private set; } = STNodeOption.Empty;

    protected override void OnCreate()
    {
        base.OnCreate();
        Input = InputOptions.Add("Input", typeof(string), bSingle: true);
    }
}
