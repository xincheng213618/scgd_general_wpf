using System;
using System.Collections.Generic;
using System.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Runtime;

/// <summary>
/// Minimal graph surface required by the execution engine. It deliberately
/// excludes selection, zoom, drawing, commands, and other editor concerns.
/// </summary>
internal interface IFlowGraphHost
{
    IEnumerable<STNode> Nodes { get; }

    bool IsReplayingChanges { get; }

    bool IsEventSource(object source);

    event STNodeEditorEventHandler NodeAdded;

    event STNodeEditorEventHandler NodeRemoved;

    event STNodeEditorOptionEventHandler OptionConnected;

    event STNodeEditorOptionEventHandler OptionDisconnected;

    event EventHandler NodeLocationChanged;

    event EventHandler HistoryChanged;

    void LoadCanvas(byte[] data);

    void Clear();

    void ClearHistory();
}

internal sealed class EditorFlowGraphHost : IFlowGraphHost
{
    public EditorFlowGraphHost(STNodeEditor editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public STNodeEditor Editor { get; }

    public IEnumerable<STNode> Nodes => Editor.Nodes.Cast<STNode>();

    public bool IsReplayingChanges => Editor.IsReplayingHistory;

    public bool IsEventSource(object source)
    {
        return ReferenceEquals(source, Editor);
    }

    public event STNodeEditorEventHandler NodeAdded
    {
        add => Editor.NodeAdded += value;
        remove => Editor.NodeAdded -= value;
    }

    public event STNodeEditorEventHandler NodeRemoved
    {
        add => Editor.NodeRemoved += value;
        remove => Editor.NodeRemoved -= value;
    }

    public event STNodeEditorOptionEventHandler OptionConnected
    {
        add => Editor.OptionConnected += value;
        remove => Editor.OptionConnected -= value;
    }

    public event STNodeEditorOptionEventHandler OptionDisconnected
    {
        add => Editor.OptionDisConnected += value;
        remove => Editor.OptionDisConnected -= value;
    }

    public event EventHandler NodeLocationChanged
    {
        add => Editor.NodeLocationChanged += value;
        remove => Editor.NodeLocationChanged -= value;
    }

    public event EventHandler HistoryChanged
    {
        add => Editor.HistoryChanged += value;
        remove => Editor.HistoryChanged -= value;
    }

    public void LoadCanvas(byte[] data)
    {
        Editor.LoadCanvas(data);
    }

    public void Clear()
    {
        Editor.Nodes.Clear();
    }

    public void ClearHistory()
    {
        Editor.ClearHistory();
    }
}

internal sealed class HeadlessFlowGraphHost : IFlowGraphHost
{
    public HeadlessFlowGraphHost(CVNodeContainer container)
    {
        Container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public CVNodeContainer Container { get; }

    public IEnumerable<STNode> Nodes => Container.Nodes.Cast<STNode>();

    public bool IsReplayingChanges => false;

    public bool IsEventSource(object source)
    {
        return ReferenceEquals(source, Container);
    }

    public event STNodeEditorEventHandler NodeAdded
    {
        add => Container.NodeAdded += value;
        remove => Container.NodeAdded -= value;
    }

    public event STNodeEditorEventHandler NodeRemoved
    {
        add => Container.NodeRemoved += value;
        remove => Container.NodeRemoved -= value;
    }

    public event STNodeEditorOptionEventHandler OptionConnected
    {
        add { }
        remove { }
    }

    public event STNodeEditorOptionEventHandler OptionDisconnected
    {
        add { }
        remove { }
    }

    public event EventHandler NodeLocationChanged
    {
        add { }
        remove { }
    }

    public event EventHandler HistoryChanged
    {
        add { }
        remove { }
    }

    public void LoadCanvas(byte[] data)
    {
        Container.LoadCanvas(data);
    }

    public void Clear()
    {
        Container.Nodes.Clear();
    }

    public void ClearHistory()
    {
    }
}
